using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Agents;
using OxenteGames.JiraCommunication.Settings;
using OxenteGames.JiraCommunication.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using L = OxenteGames.JiraCommunication.Localization.JiraLoc;

namespace OxenteGames.JiraCommunication.UI
{
    /// <summary>
    /// The agent tab: launch a task, watch it work, read its result.
    /// </summary>
    /// <remarks>
    /// Kept out of <c>JiraWindow</c> on purpose — that file is already past ten
    /// thousand lines, and this view owns event subscriptions that must be released
    /// when the window rebuilds. <see cref="Dispose"/> exists for that: the window
    /// recreates its whole UI on language change and on reconnect, and a view that
    /// stayed subscribed would keep updating orphaned elements.
    /// </remarks>
    internal sealed class AgentConsoleView
    {
        private readonly Action _repaint;

        private VisualElement _root;

        private Label _cliStatus;
        private TextField _cliPathField;
        private VisualElement _cliActions;

        private Label _skillStatus;

        private TextField _taskField;
        private Label _issueLabel;
        private DropdownField _permissionDropdown;
        private Button _runButton;
        private Button _terminalButton;
        private Label _taskStatus;
        private Label _workingDirLabel;

        private VisualElement _runList;
        private VisualElement _transcriptCard;
        private ScrollView _transcript;
        private Label _transcriptEmpty;
        private Label _runHeader;
        private Label _runMeta;
        private Label _resultLabel;
        private Button _cancelButton;

        private string _selectedRunId = string.Empty;
        private int _renderedEventCount;
        private bool _subscribed;
        private bool _cliReady;
        private string _workingDirectory = string.Empty;

        private string _issueKey = string.Empty;
        private string _issueSummary = string.Empty;
        private string _issueDescription = string.Empty;
        private string _issueBranch = string.Empty;

        public AgentConsoleView(Action repaint)
        {
            _repaint = repaint;
        }

        private static string Provider => AgentProvider.FromAiProvider(JiraPreferences.AiProvider);

        private static bool IsPortuguese => L.Current != L.En;

        // --- Lifecycle -------------------------------------------------------

        public VisualElement Build()
        {
            _root = new VisualElement();

            _root.Add(BuildCliCard());
            _root.Add(BuildSkillCard());
            _root.Add(BuildTaskCard());
            _root.Add(BuildRunsCard());
            _root.Add(BuildTranscriptCard());

            Subscribe();
            RefreshRunList();
            SelectMostRelevantRun();

            // Both are async and independent; neither blocks the panel appearing.
            _ = ProbeCliAsync(false);
            _ = ResolveWorkingDirectoryAsync();

            return _root;
        }

        public void Dispose()
        {
            Unsubscribe();
            _root = null;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            AgentService.RunsChanged += OnRunsChanged;
            AgentService.RunUpdated += OnRunUpdated;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            AgentService.RunsChanged -= OnRunsChanged;
            AgentService.RunUpdated -= OnRunUpdated;
            _subscribed = false;
        }

        /// <summary>Called when the tab becomes visible.</summary>
        public void OnShow()
        {
            RefreshSkillStatus();
            RefreshRunList();
            UpdateRunButtonState();
        }

        /// <summary>
        /// Seeds the form from a Jira issue. Called when the user sends an issue over
        /// from the Resolve tab.
        /// </summary>
        public void SetIssueContext(string issueKey, string summary, string description, string branchName)
        {
            _issueKey = issueKey ?? string.Empty;
            _issueSummary = summary ?? string.Empty;
            _issueDescription = description ?? string.Empty;
            _issueBranch = branchName ?? string.Empty;

            if (_issueLabel != null)
            {
                _issueLabel.text = string.IsNullOrWhiteSpace(_issueKey)
                    ? L.Tr(L.K.AgentNoIssue)
                    : _issueKey + (string.IsNullOrWhiteSpace(_issueSummary) ? string.Empty : " — " + _issueSummary);
            }

            if (_taskField != null)
                _taskField.Focus();
        }

        // --- CLI card --------------------------------------------------------

        private VisualElement BuildCliCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentCliTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _cliStatus = new Label(L.Tr(L.K.AgentCliChecking));
            JiraStyles.ApplyMuted(_cliStatus);
            card.Add(_cliStatus);

            _cliPathField = new TextField(L.Tr(L.K.AgentCliPathLabel))
            {
                value = JiraPreferences.GetAgentCliPath(Provider)
            };
            JiraStyles.ApplyField(_cliPathField);
            _cliPathField.RegisterCallback<FocusOutEvent>(focusOut =>
            {
                JiraPreferences.SetAgentCliPath(Provider, _cliPathField.value);

                // The override changes what discovery would return, so the cached
                // probe result is no longer valid.
                AgentCliLocator.InvalidateCache();
                _ = ProbeCliAsync(true);
            });
            card.Add(_cliPathField);

            var hint = new Label(L.Tr(L.K.AgentCliPathHint));
            JiraStyles.ApplyFieldHint(hint);
            card.Add(hint);

            _cliActions = new VisualElement();
            JiraStyles.ApplyButtonRow(_cliActions);

            var check = new Button(() => _ = ProbeCliAsync(true)) { text = L.Tr(L.K.BtnAgentCheckCli) };
            JiraStyles.ApplyCompactButton(check, false);
            _cliActions.Add(check);

            var install = new Button(() => Application.OpenURL(AgentCliLocator.InstallUrl(Provider)))
            {
                text = L.Tr(L.K.BtnAgentInstallCli)
            };
            JiraStyles.ApplyCompactButton(install, false);
            _cliActions.Add(install);

            var copyInstall = new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = AgentCliLocator.InstallCommand(Provider);
                SetTaskStatus(L.Tr(L.K.MsgAgentInstallCopied), true);
            })
            {
                text = L.Tr(L.K.BtnAgentCopyInstall)
            };
            JiraStyles.ApplyCompactButton(copyInstall, false);
            _cliActions.Add(copyInstall);

            card.Add(_cliActions);
            return card;
        }

        private async Task ProbeCliAsync(bool forceRefresh)
        {
            if (_cliStatus == null)
                return;

            _cliStatus.text = L.Tr(L.K.AgentCliChecking);
            JiraStyles.ApplyMuted(_cliStatus);

            string provider = Provider;
            AgentCliInfo info = await AgentCliLocator.LocateAsync(provider, forceRefresh);

            // The window may have been rebuilt while the probe was in flight.
            if (_cliStatus == null)
                return;

            _cliReady = info.Found;
            string display = AgentProvider.DisplayName(provider);

            if (info.Found)
            {
                string version = string.IsNullOrWhiteSpace(info.Version) ? info.Path : info.Version;
                _cliStatus.text = L.Tr(L.K.AgentCliFound, display, version);
                JiraStyles.ApplyInlineStatus(_cliStatus, true);
            }
            else if (info.Error == "override-missing")
            {
                _cliStatus.text = L.Tr(L.K.AgentCliOverrideMissing, info.Path);
                JiraStyles.ApplyInlineStatus(_cliStatus, false);
            }
            else
            {
                _cliStatus.text = L.Tr(L.K.AgentCliMissing, display);
                JiraStyles.ApplyInlineStatus(_cliStatus, false);
            }

            UpdateRunButtonState();
            _repaint?.Invoke();
        }

        // --- Skill card ------------------------------------------------------

        private VisualElement BuildSkillCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentSkillTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var note = new Label(L.Tr(L.K.AgentSkillNote));
            JiraStyles.ApplyMuted(note);
            card.Add(note);

            _skillStatus = new Label();
            JiraStyles.ApplyFieldHint(_skillStatus);
            card.Add(_skillStatus);

            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

            var install = new Button(InstallSkill) { text = L.Tr(L.K.BtnAgentInstallSkill) };
            JiraStyles.ApplyCompactButton(install, false);
            row.Add(install);

            var preview = new Button(PreviewSkill) { text = L.Tr(L.K.BtnAgentPreviewSkill) };
            JiraStyles.ApplyCompactButton(preview, false);
            row.Add(preview);

            card.Add(row);
            RefreshSkillStatus();
            return card;
        }

        private void RefreshSkillStatus()
        {
            if (_skillStatus == null)
                return;

            if (string.IsNullOrWhiteSpace(_workingDirectory))
            {
                _skillStatus.text = string.Empty;
                return;
            }

            _skillStatus.text = SkillInstaller.IsInstalled(Provider, _workingDirectory)
                ? L.Tr(L.K.MsgAgentSkillPresent, SkillInstaller.WriterFor(Provider).RelativePath)
                : L.Tr(L.K.MsgAgentSkillAbsent);
        }

        private void InstallSkill()
        {
            SkillInstallResult result = SkillInstaller.Install(Provider, _workingDirectory);

            if (result.Success)
            {
                SetTaskStatus(L.Tr(L.K.MsgAgentSkillInstalled, result.Path), true);
                AssetDatabase.Refresh();
            }
            else
            {
                SetTaskStatus(L.Tr(L.K.MsgAgentSkillFailed, result.Error), false);
            }

            RefreshSkillStatus();
        }

        private void PreviewSkill()
        {
            // A read-only scratch buffer is enough here, and it avoids writing a file
            // just so the user can look at what would be written.
            string body = SkillInstaller.BuildBody(Provider);
            EditorGUIUtility.systemCopyBuffer = body;
            SetTaskStatus(L.Tr(L.K.MsgAgentCopied), true);
            Debug.Log("[Jira] " + SkillInstaller.WriterFor(Provider).RelativePath + "\n\n" + body);
        }

        // --- Task card -------------------------------------------------------

        private VisualElement BuildTaskCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentSectionTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var intro = new Label(L.Tr(L.K.AgentIntro));
            JiraStyles.ApplyMuted(intro);
            card.Add(intro);

            var issueCaption = new Label(L.Tr(L.K.AgentIssueLabel));
            JiraStyles.ApplyDynamicFieldLabel(issueCaption);
            card.Add(issueCaption);

            _issueLabel = new Label(L.Tr(L.K.AgentNoIssue));
            JiraStyles.ApplyFieldHint(_issueLabel);
            card.Add(_issueLabel);

            _taskField = new TextField(L.Tr(L.K.AgentTaskLabel))
            {
                multiline = true
            };
            JiraStyles.ApplyField(_taskField);
            JiraStyles.ApplyMultiline(_taskField);
            card.Add(_taskField);

            var placeholder = new Label(L.Tr(L.K.AgentTaskPlaceholder));
            JiraStyles.ApplyFieldHint(placeholder);
            card.Add(placeholder);

            _permissionDropdown = new DropdownField(L.Tr(L.K.AgentPermissionLabel))
            {
                choices = new List<string>
                {
                    L.Tr(L.K.AgentPermissionPlan),
                    L.Tr(L.K.AgentPermissionDefault),
                    L.Tr(L.K.AgentPermissionAcceptEdits)
                }
            };
            _permissionDropdown.index = PermissionToIndex(JiraPreferences.AgentPermission);
            JiraStyles.ApplyDropdown(_permissionDropdown);
            _permissionDropdown.RegisterValueChangedCallback(_ =>
                JiraPreferences.AgentPermission = IndexToPermission(_permissionDropdown.index));
            card.Add(_permissionDropdown);

            var permissionNote = new Label(L.Tr(L.K.AgentPermissionNote));
            JiraStyles.ApplyNote(permissionNote);
            card.Add(permissionNote);

            _workingDirLabel = new Label();
            JiraStyles.ApplyFieldHint(_workingDirLabel);
            card.Add(_workingDirLabel);

            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

            _runButton = new Button(() => _ = StartRunAsync()) { text = L.Tr(L.K.BtnAgentRun) };
            JiraStyles.ApplyPrimaryButton(_runButton);
            _runButton.style.marginRight = 8;
            row.Add(_runButton);

            _terminalButton = new Button(OpenInTerminal) { text = L.Tr(L.K.BtnAgentTerminal) };
            JiraStyles.ApplyCompactButton(_terminalButton, false);
            row.Add(_terminalButton);

            card.Add(row);

            _taskStatus = new Label();
            JiraStyles.ApplyMuted(_taskStatus);
            card.Add(_taskStatus);

            UpdateRunButtonState();
            return card;
        }

        private static int PermissionToIndex(string permission)
        {
            switch (permission)
            {
                case AgentPermission.Default: return 1;
                case AgentPermission.AcceptEdits: return 2;
                default: return 0;
            }
        }

        private static string IndexToPermission(int index)
        {
            switch (index)
            {
                case 1: return AgentPermission.Default;
                case 2: return AgentPermission.AcceptEdits;
                default: return AgentPermission.Plan;
            }
        }

        private void UpdateRunButtonState()
        {
            if (_runButton == null)
                return;

            _runButton.SetEnabled(_cliReady);
            _terminalButton?.SetEnabled(_cliReady);
        }

        private void SetTaskStatus(string message, bool success)
        {
            if (_taskStatus == null)
                return;

            _taskStatus.text = message ?? string.Empty;
            JiraStyles.ApplyInlineStatus(_taskStatus, success);
            _repaint?.Invoke();
        }

        private async Task ResolveWorkingDirectoryAsync()
        {
            _workingDirectory = await AgentService.ResolveWorkingDirectoryAsync();

            if (_workingDirLabel != null)
                _workingDirLabel.text = L.Tr(L.K.MsgAgentWorkingDir, _workingDirectory);

            RefreshSkillStatus();
            _repaint?.Invoke();
        }

        private string BuildPrompt()
        {
            string instruction = _taskField?.value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_issueKey))
                return AgentPrompt.BuildFreeTask(instruction, IsPortuguese);

            return AgentPrompt.BuildIssueTask(
                _issueKey, _issueSummary, _issueDescription, instruction, _issueBranch, IsPortuguese);
        }

        private async Task StartRunAsync()
        {
            string instruction = _taskField?.value ?? string.Empty;

            // With no issue attached the instruction is the entire task, so an empty
            // field would send the agent nothing but boilerplate.
            if (string.IsNullOrWhiteSpace(instruction) && string.IsNullOrWhiteSpace(_issueKey))
            {
                SetTaskStatus(L.Tr(L.K.MsgAgentNoTask), false);
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingDirectory))
                await ResolveWorkingDirectoryAsync();

            var request = new AgentRequest
            {
                Prompt = BuildPrompt(),
                Provider = Provider,
                WorkingDirectory = _workingDirectory,
                IssueKey = _issueKey,
                Title = AgentPrompt.BuildTitle(_issueKey, instruction),
                PermissionMode = JiraPreferences.AgentPermission
            };

            _runButton?.SetEnabled(false);
            if (_runButton != null)
                _runButton.text = L.Tr(L.K.BtnAgentRunning);

            string failure = null;
            AgentRunInfo run = await AgentService.StartAsync(request, error => failure = error);

            if (_runButton != null)
            {
                _runButton.text = L.Tr(L.K.BtnAgentRun);
                _runButton.SetEnabled(_cliReady);
            }

            if (run == null)
            {
                SetTaskStatus(L.Tr(L.K.MsgAgentStartFailed, failure ?? "?"), false);
                return;
            }

            SetTaskStatus(string.Empty, true);
            RefreshRunList();
            SelectRun(run.RunId);
        }

        private void OpenInTerminal()
        {
            AgentCliInfo? cached = AgentCliLocator.Cached(Provider);
            var request = new AgentRequest
            {
                Provider = Provider,
                ExecutablePath = cached?.Path ?? string.Empty,
                WorkingDirectory = _workingDirectory
            };

            IAgentRunner runner = AgentService.CreateRunner(Provider);
            string command = runner.BuildInteractiveCommandLine(request);

            // An interactive session has no stream file, so it is deliberately not
            // added to the run list: the window cannot report on what it cannot see.
            if (!AgentShell.OpenInTerminal(command, _workingDirectory, out string error))
                SetTaskStatus(L.Tr(L.K.MsgAgentTerminalFailed, error), false);
        }

        // --- Run list --------------------------------------------------------

        private VisualElement BuildRunsCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var title = new Label(L.Tr(L.K.AgentRunsTitle));
            JiraStyles.ApplySectionTitle(title);
            title.style.marginBottom = 0;
            header.Add(title);

            var refresh = new Button(() =>
            {
                AgentService.Refresh();
                RefreshRunList();
            })
            {
                text = L.Tr(L.K.BtnAgentRefresh)
            };
            JiraStyles.ApplyCompactButton(refresh, false);
            header.Add(refresh);

            card.Add(header);

            _runList = new VisualElement();
            _runList.style.marginTop = 8;
            card.Add(_runList);

            return card;
        }

        private void RefreshRunList()
        {
            if (_runList == null)
                return;

            _runList.Clear();
            IReadOnlyList<AgentRunInfo> runs = AgentService.Runs;

            if (runs.Count == 0)
            {
                var empty = new Label(L.Tr(L.K.AgentNoRuns));
                JiraStyles.ApplyMuted(empty);
                _runList.Add(empty);
                return;
            }

            foreach (AgentRunInfo run in runs)
            {
                string runId = run.RunId;
                var button = new Button(() => SelectRun(runId))
                {
                    text = FormatRunRow(run)
                };
                JiraStyles.ApplyRunRow(button, runId == _selectedRunId);
                _runList.Add(button);
            }
        }

        private static string FormatRunRow(AgentRunInfo run)
        {
            string time = run.StartedAtUtc == default(DateTime)
                ? string.Empty
                : run.StartedAtUtc.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

            return "[" + StatusText(run.Status) + "]  " + run.DisplayTitle + "   " + time;
        }

        private static string StatusText(AgentRunStatus status)
        {
            switch (status)
            {
                case AgentRunStatus.Running: return L.Tr(L.K.AgentStatusRunning);
                case AgentRunStatus.Succeeded: return L.Tr(L.K.AgentStatusSucceeded);
                case AgentRunStatus.Failed: return L.Tr(L.K.AgentStatusFailed);
                case AgentRunStatus.Canceled: return L.Tr(L.K.AgentStatusCanceled);
                default: return L.Tr(L.K.AgentStatusOrphaned);
            }
        }

        private static JiraTone StatusTone(AgentRunStatus status)
        {
            switch (status)
            {
                case AgentRunStatus.Running: return JiraTone.Accent;
                case AgentRunStatus.Succeeded: return JiraTone.Success;
                case AgentRunStatus.Failed: return JiraTone.Danger;
                case AgentRunStatus.Canceled: return JiraTone.Neutral;
                default: return JiraTone.Danger;
            }
        }

        private void SelectMostRelevantRun()
        {
            IReadOnlyList<AgentRunInfo> runs = AgentService.Runs;

            // Prefer whatever is in flight; otherwise the newest.
            foreach (AgentRunInfo run in runs)
            {
                if (run.IsRunning)
                {
                    SelectRun(run.RunId);
                    return;
                }
            }

            if (runs.Count > 0)
                SelectRun(runs[0].RunId);
            else
                RenderTranscript(null);
        }

        private void SelectRun(string runId)
        {
            _selectedRunId = runId ?? string.Empty;
            AgentRunInfo run = AgentService.Find(_selectedRunId);

            // A finished run's transcript is only replayed from disk once it is opened.
            if (run != null)
                AgentService.Hydrate(run);

            RefreshRunList();
            RenderTranscript(run);
        }

        // --- Transcript ------------------------------------------------------

        private VisualElement BuildTranscriptCard()
        {
            _transcriptCard = new VisualElement();
            JiraStyles.ApplyCard(_transcriptCard);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            _runHeader = new Label();
            JiraStyles.ApplySectionTitle(_runHeader);
            _runHeader.style.marginBottom = 0;
            headerRow.Add(_runHeader);

            _transcriptCard.Add(headerRow);

            _runMeta = new Label();
            JiraStyles.ApplyFieldHint(_runMeta);
            _transcriptCard.Add(_runMeta);

            _transcript = new ScrollView(ScrollViewMode.Vertical);
            JiraStyles.ApplyTranscriptScroll(_transcript);
            _transcriptCard.Add(_transcript);

            _transcriptEmpty = new Label(L.Tr(L.K.AgentWaitingFirstEvent));
            JiraStyles.ApplyMuted(_transcriptEmpty);
            _transcript.Add(_transcriptEmpty);

            _resultLabel = new Label();
            _resultLabel.style.display = DisplayStyle.None;
            _transcriptCard.Add(_resultLabel);

            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

            _cancelButton = new Button(() => AgentService.Cancel(_selectedRunId))
            {
                text = L.Tr(L.K.BtnAgentCancel)
            };
            JiraStyles.ApplyCompactButton(_cancelButton, true);
            row.Add(_cancelButton);

            var copy = new Button(() =>
            {
                AgentRunInfo run = AgentService.Find(_selectedRunId);
                if (run == null)
                    return;

                EditorGUIUtility.systemCopyBuffer = run.FinalText ?? string.Empty;
                SetTaskStatus(L.Tr(L.K.MsgAgentCopied), true);
            })
            {
                text = L.Tr(L.K.BtnAgentCopyResult)
            };
            JiraStyles.ApplyCompactButton(copy, false);
            row.Add(copy);

            var openFolder = new Button(() =>
            {
                AgentRunInfo run = AgentService.Find(_selectedRunId);
                if (run != null && !string.IsNullOrWhiteSpace(run.Directory))
                    EditorUtility.RevealInFinder(run.Directory);
            })
            {
                text = L.Tr(L.K.BtnAgentOpenFolder)
            };
            JiraStyles.ApplyCompactButton(openFolder, false);
            row.Add(openFolder);

            var delete = new Button(() =>
            {
                string runId = _selectedRunId;
                AgentService.Delete(runId);
                RefreshRunList();
                SelectMostRelevantRun();
            })
            {
                text = L.Tr(L.K.BtnAgentDelete)
            };
            JiraStyles.ApplyCompactButton(delete, true);
            row.Add(delete);

            _transcriptCard.Add(row);
            return _transcriptCard;
        }

        private void RenderTranscript(AgentRunInfo run)
        {
            if (_transcript == null)
                return;

            _transcript.Clear();
            _renderedEventCount = 0;

            // Added unconditionally; AppendNewEvents removes it as soon as the run has
            // produced something, so a just-started run is never a blank box.
            _transcript.Add(_transcriptEmpty);

            if (run == null)
            {
                _runHeader.text = L.Tr(L.K.AgentTranscriptTitle);
                _runMeta.text = string.Empty;
                _resultLabel.style.display = DisplayStyle.None;
                _cancelButton.SetEnabled(false);
                return;
            }

            _runHeader.text = StatusText(run.Status) + " · " + run.DisplayTitle;
            _runMeta.text = BuildMeta(run);
            _cancelButton.SetEnabled(run.IsRunning);

            AppendNewEvents(run);
            RenderResult(run);
        }

        private static string BuildMeta(AgentRunInfo run)
        {
            var parts = new List<string> { AgentProvider.DisplayName(run.Provider) };

            if (run.DurationMs > 0)
            {
                parts.Add(L.Tr(L.K.AgentMetaDuration,
                    (run.DurationMs / 1000d).ToString("0.0", CultureInfo.InvariantCulture)));
            }

            if (run.CostUsd > 0)
            {
                parts.Add(L.Tr(L.K.AgentMetaCost,
                    run.CostUsd.ToString("0.0000", CultureInfo.InvariantCulture)));
            }

            if (!string.IsNullOrWhiteSpace(run.SessionId))
                parts.Add(run.SessionId);

            return string.Join("  ·  ", parts.ToArray());
        }

        /// <summary>
        /// Appends only the events not rendered yet. Rebuilding the whole transcript on
        /// every tick would rebuild hundreds of elements four times a second.
        /// </summary>
        private void AppendNewEvents(AgentRunInfo run)
        {
            // ScrollView reparents children into its contentContainer, so comparing
            // against the ScrollView itself would never match — ask the element.
            if (run.Events.Count > 0 && _transcriptEmpty.parent != null)
                _transcriptEmpty.RemoveFromHierarchy();

            for (int i = _renderedEventCount; i < run.Events.Count; i++)
            {
                VisualElement row = BuildEventRow(run.Events[i]);
                if (row != null)
                    _transcript.Add(row);
            }

            _renderedEventCount = run.Events.Count;

            // Keep the newest line in view while a run is live.
            if (run.IsRunning)
                _transcript.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private static VisualElement BuildEventRow(AgentEvent agentEvent)
        {
            string tag;
            JiraTone tone;
            string text = agentEvent.Text ?? string.Empty;

            switch (agentEvent.Kind)
            {
                case AgentEventKind.Started:
                    tag = L.Tr(L.K.AgentEventStarted);
                    tone = JiraTone.Accent;
                    break;

                case AgentEventKind.Thinking:
                    tag = L.Tr(L.K.AgentEventThinking);
                    tone = JiraTone.Neutral;
                    break;

                case AgentEventKind.ToolUse:
                    tag = L.Tr(L.K.AgentEventTool);
                    tone = JiraTone.Accent;
                    if (!string.IsNullOrWhiteSpace(agentEvent.Detail))
                        text += "  " + agentEvent.Detail;
                    break;

                case AgentEventKind.ToolResult:
                    // Successful tool returns add noise without adding information.
                    if (!agentEvent.IsError)
                        return null;
                    tag = L.Tr(L.K.AgentEventTool);
                    tone = JiraTone.Danger;
                    break;

                case AgentEventKind.Error:
                    tag = L.Tr(L.K.AgentEventError);
                    tone = JiraTone.Danger;
                    break;

                case AgentEventKind.Text:
                    tag = string.Empty;
                    tone = JiraTone.Success;
                    break;

                case AgentEventKind.Result:
                    // Rendered separately as the result block.
                    return null;

                default:
                    return null;
            }

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var row = new VisualElement();
            JiraStyles.ApplyTranscriptRow(row);

            var tagLabel = new Label(tag);
            JiraStyles.ApplyTranscriptTag(tagLabel, tone);
            row.Add(tagLabel);

            var textLabel = new Label(Shorten(text));
            JiraStyles.ApplyTranscriptText(textLabel);
            row.Add(textLabel);

            return row;
        }

        /// <summary>
        /// Caps one line's length. A single tool result or a long reasoning block can
        /// be tens of kilobytes, which would stall UI Toolkit's text layout.
        /// </summary>
        private static string Shorten(string text)
        {
            const int limit = 600;
            string single = text.Replace("\r", string.Empty).Trim();

            return single.Length > limit ? single.Substring(0, limit) + " [...]" : single;
        }

        private void RenderResult(AgentRunInfo run)
        {
            if (_resultLabel == null)
                return;

            if (run.IsRunning || string.IsNullOrWhiteSpace(run.FinalText))
            {
                _resultLabel.style.display = DisplayStyle.None;
                return;
            }

            bool isError = run.Status == AgentRunStatus.Failed || run.Status == AgentRunStatus.Orphaned;

            _resultLabel.style.display = DisplayStyle.Flex;
            _resultLabel.text = run.FinalText;
            JiraStyles.ApplyResultBlock(_resultLabel, isError);
        }

        // --- Service callbacks -----------------------------------------------

        private void OnRunsChanged()
        {
            if (_root == null)
                return;

            RefreshRunList();

            AgentRunInfo selected = AgentService.Find(_selectedRunId);
            if (selected != null)
            {
                _runHeader.text = StatusText(selected.Status) + " · " + selected.DisplayTitle;
                _runMeta.text = BuildMeta(selected);
                _cancelButton.SetEnabled(selected.IsRunning);
                RenderResult(selected);
            }

            _repaint?.Invoke();
        }

        private void OnRunUpdated(AgentRunInfo run)
        {
            if (_root == null || run == null || run.RunId != _selectedRunId)
                return;

            AppendNewEvents(run);
            _runMeta.text = BuildMeta(run);
            RenderResult(run);
            _repaint?.Invoke();
        }
    }
}
