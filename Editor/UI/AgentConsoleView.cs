using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Agents;
using OxenteGames.JiraCommunication.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using L = OxenteGames.JiraCommunication.Localization.JiraLoc;

namespace OxenteGames.JiraCommunication.UI
{
    /// <summary>
    /// The agent tab: one conversation with the local coding agent.
    /// </summary>
    /// <remarks>
    /// Presented as a chat because that is what it is — the developer writes a
    /// message, the agent answers, and the next message continues the same CLI
    /// session. The mechanics underneath are still one detached process per turn,
    /// which is what survives a domain reload; <see cref="AgentRunInfo.ThreadId"/> is
    /// what stitches those processes back into a single exchange on screen.
    /// <para>
    /// The step-by-step CLI output is deliberately collapsed into one line per turn.
    /// It is diagnostic detail — which tool ran, what it touched — and leaving it
    /// expanded pushed the actual answer off the screen. It is one click away, and
    /// stays open per turn once opened.
    /// </para>
    /// <para>
    /// Everything that is configuration rather than conversation — the CLI path, the
    /// model, the project instructions, the env file, the token budget — lives in the
    /// settings tab, in <see cref="AgentSettingsView"/>. Kept out of <c>JiraWindow</c>
    /// for the same reason as before: this view owns event subscriptions that must be
    /// released when the window rebuilds. <see cref="Dispose"/> exists for that.
    /// </para>
    /// </remarks>
    internal sealed class AgentConsoleView
    {
        /// <summary>How often the quota countdown is refreshed, in milliseconds.</summary>
        private const long UsageRefreshMs = 30_000;

        /// <summary>Frame interval of the working indicator, in milliseconds.</summary>
        private const long TypingFrameMs = 110;

        /// <summary>Frames one dot is held for, so the ellipsis reads as a pulse.</summary>
        private const int TypingFramesPerDot = 4;

        private readonly Action _repaint;

        /// <summary>Opens the settings tab, where the agent is configured.</summary>
        private readonly Action _openSettings;

        private VisualElement _root;

        private Label _cliStatus;
        private Label _usageLabel;
        private Label _resetLabel;
        private VisualElement _meterTrack;
        private VisualElement _meterFill;

        private Label _issueChip;
        private Button _unlinkButton;
        private DropdownField _permissionDropdown;

        private Button _historyButton;
        private VisualElement _historyPanel;

        private ScrollView _chat;
        private Label _chatEmpty;

        private TextField _composer;
        private Button _sendButton;
        private Button _cancelButton;
        private Label _status;

        private VisualElement _typingRow;
        private VisualElement _typingSpinner;
        private Label _typingLabel;
        private IVisualElementScheduledItem _typingAnimation;
        private int _typingFrame;
        private bool _typingVisible;

        /// <summary>Last thing the running turn reported doing, shown while it works.</summary>
        private string _typingDetail = string.Empty;

        private readonly List<TurnView> _turns = new List<TurnView>();

        private string _threadId = string.Empty;
        private bool _subscribed;
        private bool _cliReady;
        private bool _historyOpen;
        private string _workingDirectory = string.Empty;

        private string _issueKey = string.Empty;
        private string _issueSummary = string.Empty;
        private string _issueDescription = string.Empty;
        private string _issueBranch = string.Empty;

        public AgentConsoleView(Action repaint, Action openSettings = null)
        {
            _repaint = repaint;
            _openSettings = openSettings;
        }

        /// <summary>
        /// The agent's own provider setting — deliberately not derived from the AI
        /// assistant's provider, which belongs to the API-key feature.
        /// </summary>
        private static string Provider => JiraPreferences.AgentProviderId;

        private static bool IsPortuguese => L.Current != L.En;

        /// <summary>One turn on screen: the message sent, and what came back.</summary>
        private sealed class TurnView
        {
            public string RunId = string.Empty;
            public VisualElement Root;

            /// <summary>Where agent messages are appended, in arrival order.</summary>
            public VisualElement Body;

            public Button ActivityToggle;
            public VisualElement ActivityLog;
            public Label Meta;
            public Label Result;

            /// <summary>Events already rendered, so a tick only appends what is new.</summary>
            public int RenderedEvents;

            public int StepCount;
            public string LastStep = string.Empty;
            public bool Expanded;
        }

        // --- Lifecycle -------------------------------------------------------

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;

            _root.Add(BuildToolbar());
            _root.Add(BuildHistoryPanel());
            _root.Add(BuildChat());
            _root.Add(BuildTypingRow());
            _root.Add(BuildComposer());

            Subscribe();
            SelectMostRelevantThread();
            RefreshUsage();

            // Both are async and independent; neither blocks the panel appearing.
            _ = ProbeCliAsync(false);
            _ = ResolveWorkingDirectoryAsync();

            // The reset time is a countdown, so it goes stale on its own even when
            // nothing happens in the Editor.
            _root.schedule.Execute(RefreshUsage).Every(UsageRefreshMs);

            return _root;
        }

        public void Dispose()
        {
            Unsubscribe();

            if (_typingAnimation != null)
                _typingAnimation.Pause();

            _typingAnimation = null;
            _typingRow = null;
            _root = null;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            AgentService.RunsChanged += OnRunsChanged;
            AgentService.RunUpdated += OnRunUpdated;
            AgentUsageLedger.Changed += RefreshUsage;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            AgentService.RunsChanged -= OnRunsChanged;
            AgentService.RunUpdated -= OnRunUpdated;
            AgentUsageLedger.Changed -= RefreshUsage;
            _subscribed = false;
        }

        /// <summary>Called when the tab becomes visible.</summary>
        public void OnShow()
        {
            RefreshHistory();
            RefreshUsage();
            UpdateSendState();
        }

        /// <summary>
        /// Seeds the conversation from a Jira issue. Called when the user sends an
        /// issue over from the Resolve tab.
        /// </summary>
        public void SetIssueContext(string issueKey, string summary, string description, string branchName)
        {
            _issueKey = issueKey ?? string.Empty;
            _issueSummary = summary ?? string.Empty;
            _issueDescription = description ?? string.Empty;
            _issueBranch = branchName ?? string.Empty;

            // An issue arriving from another tab is a new subject, not a follow-up to
            // whatever conversation happened to be open.
            StartNewThread();
            RefreshIssueChip();

            _composer?.Focus();
        }

        // --- Toolbar ---------------------------------------------------------

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            JiraStyles.ApplyToolbar(bar);

            _cliStatus = new Label(L.Tr(L.K.AgentCliChecking));
            JiraStyles.ApplyToolbarText(_cliStatus, JiraTone.Neutral);
            bar.Add(_cliStatus);

            _meterTrack = new VisualElement();
            JiraStyles.ApplyMeterTrack(_meterTrack);
            _meterFill = new VisualElement();
            JiraStyles.ApplyMeterFill(_meterFill, 0f, JiraTone.Accent);
            _meterTrack.Add(_meterFill);
            bar.Add(_meterTrack);

            _usageLabel = new Label();
            JiraStyles.ApplyToolbarText(_usageLabel, JiraTone.Neutral);
            bar.Add(_usageLabel);

            _resetLabel = new Label();
            JiraStyles.ApplyToolbarText(_resetLabel, JiraTone.Neutral);
            bar.Add(_resetLabel);

            // Pushes the buttons to the right edge of the toolbar.
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            spacer.style.minWidth = 8;
            bar.Add(spacer);

            var newChat = new Button(StartNewThread) { text = L.Tr(L.K.BtnAgentNewChat) };
            JiraStyles.ApplyToolbarButton(newChat, true);
            bar.Add(newChat);

            _historyButton = new Button(ToggleHistory) { text = L.Tr(L.K.BtnAgentHistory) };
            JiraStyles.ApplyToolbarButton(_historyButton, false);
            bar.Add(_historyButton);

            var terminal = new Button(OpenInTerminal) { text = L.Tr(L.K.BtnAgentTerminal) };
            JiraStyles.ApplyToolbarButton(terminal, false);
            bar.Add(terminal);

            // Configuration lives in the settings tab; this is the way back to it.
            var settings = new Button(() => _openSettings?.Invoke())
            {
                text = L.Tr(L.K.BtnAgentConfigure)
            };
            JiraStyles.ApplyToolbarButton(settings, false);
            settings.SetEnabled(_openSettings != null);
            bar.Add(settings);

            return bar;
        }

        private void ToggleHistory()
        {
            _historyOpen = !_historyOpen;
            _historyButton.text = (_historyOpen ? "▴  " : string.Empty) + L.Tr(L.K.BtnAgentHistory);
            RefreshHistory();
            _repaint?.Invoke();
        }

        private VisualElement BuildHistoryPanel()
        {
            _historyPanel = new VisualElement();
            _historyPanel.style.display = DisplayStyle.None;
            _historyPanel.style.marginBottom = 6;
            return _historyPanel;
        }

        private void RefreshHistory()
        {
            if (_historyPanel == null)
                return;

            _historyPanel.style.display = _historyOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _historyPanel.Clear();

            if (!_historyOpen)
                return;

            List<AgentRunInfo> threads = AgentService.Threads();

            if (threads.Count == 0)
            {
                var empty = new Label(L.Tr(L.K.AgentNoRuns));
                JiraStyles.ApplyMuted(empty);
                _historyPanel.Add(empty);
                return;
            }

            foreach (AgentRunInfo head in threads)
            {
                string threadId = string.IsNullOrWhiteSpace(head.ThreadId) ? head.RunId : head.ThreadId;
                var row = new Button(() => SelectThread(threadId))
                {
                    text = FormatThreadRow(head)
                };
                JiraStyles.ApplyRunRow(row, threadId == _threadId);
                _historyPanel.Add(row);
            }

            var row2 = new VisualElement();
            JiraStyles.ApplyButtonRow(row2);

            var reload = new Button(() =>
            {
                AgentService.Refresh();
                RefreshHistory();
                RenderThread();
            })
            {
                text = L.Tr(L.K.BtnAgentRefresh)
            };
            JiraStyles.ApplyToolbarButton(reload, false);
            row2.Add(reload);

            var openFolder = new Button(() =>
            {
                AgentRunInfo run = LastTurn();
                if (run != null && !string.IsNullOrWhiteSpace(run.Directory))
                    EditorUtility.RevealInFinder(run.Directory);
            })
            {
                text = L.Tr(L.K.BtnAgentOpenFolder)
            };
            JiraStyles.ApplyToolbarButton(openFolder, false);
            row2.Add(openFolder);

            var copy = new Button(() =>
            {
                AgentRunInfo run = LastTurn();
                if (run == null)
                    return;

                EditorGUIUtility.systemCopyBuffer = run.FinalText ?? string.Empty;
                SetStatus(L.Tr(L.K.MsgAgentCopied), true);
            })
            {
                text = L.Tr(L.K.BtnAgentCopyResult)
            };
            JiraStyles.ApplyToolbarButton(copy, false);
            row2.Add(copy);

            var delete = new Button(DeleteThread) { text = L.Tr(L.K.BtnAgentDeleteChat) };
            JiraStyles.ApplyToolbarButton(delete, false);
            row2.Add(delete);

            _historyPanel.Add(row2);
        }

        private static string FormatThreadRow(AgentRunInfo head)
        {
            string time = head.StartedAtUtc == default(DateTime)
                ? string.Empty
                : head.StartedAtUtc.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

            return "[" + StatusText(head.Status) + "]  " + head.DisplayTitle + "   " + time;
        }

        /// <summary>Deletes every run of the open conversation.</summary>
        private void DeleteThread()
        {
            foreach (AgentRunInfo run in AgentService.Thread(_threadId))
                AgentService.Delete(run.RunId);

            _threadId = string.Empty;
            RefreshHistory();
            SelectMostRelevantThread();
        }

        // --- Usage meter -----------------------------------------------------

        /// <summary>
        /// Updates the token readout.
        /// </summary>
        /// <remarks>
        /// The percentage is against the budget configured in the settings tab, not
        /// against the account's real allowance — no CLI reports that. With no budget
        /// set the meter shows the raw total and says where to set one, rather than
        /// inventing a denominator.
        /// </remarks>
        private void RefreshUsage()
        {
            if (_usageLabel == null)
                return;

            AgentUsageWindow window = AgentUsageLedger.CurrentWindow();

            if (!window.Active)
            {
                _usageLabel.text = L.Tr(L.K.AgentUsageIdle);
                JiraStyles.ApplyToolbarText(_usageLabel, JiraTone.Neutral);
                _resetLabel.text = string.Empty;
                JiraStyles.ApplyMeterFill(_meterFill, 0f, JiraTone.Accent);
                _repaint?.Invoke();
                return;
            }

            string used = FormatTokens(window.Usage.Total);

            if (window.Budget <= 0)
            {
                _usageLabel.text = L.Tr(L.K.AgentUsageNoBudget, used);
                JiraStyles.ApplyToolbarText(_usageLabel, JiraTone.Neutral);
                JiraStyles.ApplyMeterFill(_meterFill, 0f, JiraTone.Neutral);
            }
            else
            {
                int percentLeft = (int)Math.Round((1f - window.Fraction) * 100f);
                _usageLabel.text = L.Tr(L.K.AgentUsageSummary, used,
                    percentLeft.ToString(CultureInfo.InvariantCulture),
                    FormatTokens(window.Remaining));

                JiraTone tone = window.Fraction >= 0.9f
                    ? JiraTone.Danger
                    : window.Fraction >= 0.7f ? JiraTone.Accent : JiraTone.Success;

                JiraStyles.ApplyToolbarText(_usageLabel, tone);
                JiraStyles.ApplyMeterFill(_meterFill, window.Fraction, tone);
            }

            _resetLabel.text = L.Tr(L.K.AgentUsageReset,
                window.EndUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                FormatDuration(window.TimeToReset));

            _repaint?.Invoke();
        }

        /// <summary>Token counts as a short figure: 12.3k, 1.2M.</summary>
        private static string FormatTokens(long tokens)
        {
            if (tokens >= 1_000_000)
                return (tokens / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture) + "M";

            if (tokens >= 1_000)
                return (tokens / 1_000d).ToString("0.0", CultureInfo.InvariantCulture) + "k";

            return tokens.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return ((int)span.TotalHours).ToString(CultureInfo.InvariantCulture) + "h "
                       + span.Minutes.ToString("00", CultureInfo.InvariantCulture) + "min";
            }

            return Math.Max(0, (int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "min";
        }

        // --- CLI probe -------------------------------------------------------

        private async Task ProbeCliAsync(bool forceRefresh)
        {
            if (_cliStatus == null)
                return;

            _cliStatus.text = L.Tr(L.K.AgentCliChecking);
            JiraStyles.ApplyToolbarText(_cliStatus, JiraTone.Neutral);

            string provider = Provider;
            AgentCliInfo info = await AgentCliLocator.LocateAsync(provider, forceRefresh);

            // The window may have been rebuilt while the probe was in flight.
            if (_cliStatus == null)
                return;

            _cliReady = info.Found;
            string display = AgentProvider.DisplayName(provider);

            _cliStatus.text = info.Found
                ? display + " · " + L.Tr(L.K.AgentCliReady)
                : L.Tr(L.K.AgentCliMissingShort, display);

            JiraStyles.ApplyToolbarText(_cliStatus, info.Found ? JiraTone.Success : JiraTone.Danger);

            if (!info.Found)
                SetStatus(L.Tr(L.K.AgentCliMissingHint), false);

            UpdateSendState();
            _repaint?.Invoke();
        }

        // --- Chat ------------------------------------------------------------

        private VisualElement BuildChat()
        {
            _chat = new ScrollView(ScrollViewMode.Vertical);
            JiraStyles.ApplyChatScroll(_chat);

            _chatEmpty = new Label(L.Tr(L.K.AgentChatEmpty));
            JiraStyles.ApplyMuted(_chatEmpty);
            _chat.Add(_chatEmpty);

            return _chat;
        }

        // --- Working indicator -----------------------------------------------

        /// <summary>
        /// The "still working" row shown between the transcript and the composer.
        /// </summary>
        /// <remarks>
        /// A turn can spend a long stretch producing nothing the transcript shows —
        /// reading files, thinking — and a chat that has gone completely still is
        /// indistinguishable from one that has died. A disabled send button is not
        /// enough of a signal, because it does not move.
        /// <para>
        /// It deliberately lives outside the <see cref="ScrollView"/>. Inside it, the
        /// row would be pushed off-screen by the very output it is reporting on, and
        /// <see cref="RenderThread"/> — which clears the scroll view — would delete it
        /// on every thread switch.
        /// </para>
        /// </remarks>
        private VisualElement BuildTypingRow()
        {
            _typingRow = new VisualElement();
            JiraStyles.ApplyLoaderRow(_typingRow);
            _typingRow.style.marginTop = 6;
            _typingRow.style.display = DisplayStyle.None;

            _typingSpinner = new VisualElement();
            JiraStyles.ApplyLoaderSpinner(_typingSpinner);
            _typingRow.Add(_typingSpinner);

            _typingLabel = new Label(L.Tr(L.K.AgentWorking));
            JiraStyles.ApplyMuted(_typingLabel);
            _typingLabel.style.marginLeft = 10;
            _typingLabel.style.flexShrink = 1;
            _typingLabel.style.overflow = Overflow.Hidden;
            _typingRow.Add(_typingLabel);

            // Created paused: the panel is usually opened on an idle conversation, and
            // a scheduled item that ticks ten times a second forces a repaint each
            // time whether or not anything is running.
            _typingAnimation = _typingRow.schedule.Execute(AnimateTyping).Every(TypingFrameMs);
            _typingAnimation.Pause();

            return _typingRow;
        }

        /// <summary>Shows or hides the indicator, and starts or stops its animation.</summary>
        private void UpdateTypingState(bool busy)
        {
            if (_typingRow == null || _typingAnimation == null)
                return;

            if (_typingVisible == busy)
                return;

            _typingVisible = busy;
            _typingRow.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;

            if (busy)
            {
                _typingFrame = 0;
                _typingAnimation.Resume();
            }
            else
            {
                _typingDetail = string.Empty;
                _typingAnimation.Pause();
            }
        }

        /// <summary>
        /// Advances one frame of the indicator: the spinner, the pulsing ellipsis, and
        /// the send button's label, which is the same state seen from the other side.
        /// </summary>
        private void AnimateTyping()
        {
            if (_typingRow == null || _typingRow.panel == null)
                return;

            _typingFrame++;

            // Unity ships a twelve-frame spinner as editor icons; using it keeps this
            // looking like the rest of the Editor instead of a hand-rolled widget.
            var frame = EditorGUIUtility
                .IconContent("WaitSpin" + (_typingFrame % 12).ToString("00", CultureInfo.InvariantCulture))
                ?.image as Texture2D;

            if (frame != null && _typingSpinner != null)
                _typingSpinner.style.backgroundImage = new StyleBackground(frame);

            string dots = new string('.', 1 + _typingFrame / TypingFramesPerDot % 3);
            string working = L.Tr(L.K.AgentWorking) + dots;

            if (_typingLabel != null)
            {
                _typingLabel.text = string.IsNullOrWhiteSpace(_typingDetail)
                    ? working
                    : working + "  ·  " + _typingDetail;
            }

            if (_sendButton != null)
                _sendButton.text = working;

            _repaint?.Invoke();
        }

        private void SelectMostRelevantThread()
        {
            List<AgentRunInfo> threads = AgentService.Threads();

            // Prefer whatever is in flight; otherwise the newest conversation.
            foreach (AgentRunInfo head in threads)
            {
                if (head.IsRunning)
                {
                    SelectThread(head.ThreadId);
                    return;
                }
            }

            if (threads.Count > 0)
                SelectThread(threads[0].ThreadId);
            else
                RenderThread();
        }

        private void SelectThread(string threadId)
        {
            _threadId = threadId ?? string.Empty;

            // A finished turn's transcript is only replayed from disk once it is opened.
            foreach (AgentRunInfo run in AgentService.Thread(_threadId))
                AgentService.Hydrate(run);

            RefreshHistory();
            RenderThread();
            UpdateSendState();
        }

        private void StartNewThread()
        {
            _threadId = string.Empty;
            RefreshHistory();
            RenderThread();
            UpdateSendState();
            _composer?.Focus();
        }

        /// <summary>Rebuilds the whole conversation. Used on thread changes only.</summary>
        private void RenderThread()
        {
            if (_chat == null)
                return;

            _chat.Clear();
            _turns.Clear();

            List<AgentRunInfo> turns = AgentService.Thread(_threadId);

            if (turns.Count == 0)
            {
                _chat.Add(_chatEmpty);
                _repaint?.Invoke();
                return;
            }

            foreach (AgentRunInfo run in turns)
                AppendTurn(run);

            ScrollToEnd();
            _repaint?.Invoke();
        }

        private TurnView AppendTurn(AgentRunInfo run)
        {
            // ScrollView reparents children into its contentContainer, so the empty
            // notice is asked whether it is still attached rather than compared
            // against the ScrollView itself.
            if (_chatEmpty != null && _chatEmpty.parent != null)
                _chatEmpty.RemoveFromHierarchy();

            var turn = new TurnView { RunId = run.RunId };

            turn.Root = new VisualElement();

            var userCaption = new Label(L.Tr(L.K.AgentYou) + "  ·  " + FormatTime(run.StartedAtUtc));
            JiraStyles.ApplyBubbleCaption(userCaption, true);
            turn.Root.Add(userCaption);

            var userBubble = new Label(DescribeInstruction(run));
            JiraStyles.ApplyBubble(userBubble, true);
            turn.Root.Add(userBubble);

            var agentCaption = new Label(AgentProvider.DisplayName(run.Provider));
            JiraStyles.ApplyBubbleCaption(agentCaption, false);
            turn.Root.Add(agentCaption);

            turn.ActivityToggle = new Button(() => ToggleActivity(turn)) { text = string.Empty };
            JiraStyles.ApplyActivityToggle(turn.ActivityToggle, false);
            turn.ActivityToggle.style.display = DisplayStyle.None;
            turn.Root.Add(turn.ActivityToggle);

            turn.ActivityLog = new VisualElement();
            JiraStyles.ApplyActivityLog(turn.ActivityLog);
            turn.ActivityLog.style.display = DisplayStyle.None;
            turn.Root.Add(turn.ActivityLog);

            turn.Body = new VisualElement();
            turn.Root.Add(turn.Body);

            turn.Result = new Label();
            turn.Result.style.display = DisplayStyle.None;
            turn.Root.Add(turn.Result);

            turn.Meta = new Label();
            JiraStyles.ApplyTurnMeta(turn.Meta);
            turn.Root.Add(turn.Meta);

            _chat.Add(turn.Root);
            _turns.Add(turn);

            AppendNewEvents(turn, run);
            RenderTurnTail(turn, run);
            return turn;
        }

        /// <summary>
        /// What the developer actually asked, as it should read in the bubble.
        /// </summary>
        /// <remarks>
        /// The stored prompt carries project framing and, on the first turn, the whole
        /// issue description. Showing that would bury the request under boilerplate the
        /// developer did not write, so only the typed instruction is shown, with the
        /// issue reduced to a heading line.
        /// </remarks>
        private static string DescribeInstruction(AgentRunInfo run)
        {
            string instruction = (run.Instruction ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(run.IssueKey))
            {
                string heading = run.IssueKey;
                return string.IsNullOrWhiteSpace(instruction)
                    ? heading
                    : heading + "\n" + instruction;
            }

            return string.IsNullOrWhiteSpace(instruction)
                ? (run.DisplayTitle ?? string.Empty)
                : instruction;
        }

        private void ToggleActivity(TurnView turn)
        {
            turn.Expanded = !turn.Expanded;
            turn.ActivityLog.style.display = turn.Expanded ? DisplayStyle.Flex : DisplayStyle.None;
            JiraStyles.ApplyActivityToggle(turn.ActivityToggle, turn.Expanded);
            UpdateActivityHeader(turn);
            _repaint?.Invoke();
        }

        private static void UpdateActivityHeader(TurnView turn)
        {
            if (turn.StepCount == 0)
            {
                turn.ActivityToggle.style.display = DisplayStyle.None;
                return;
            }

            turn.ActivityToggle.style.display = DisplayStyle.Flex;
            turn.ActivityToggle.text = (turn.Expanded ? "▾  " : "▸  ")
                                       + L.Tr(L.K.AgentActivitySteps,
                                           turn.StepCount.ToString(CultureInfo.InvariantCulture))
                                       + (string.IsNullOrWhiteSpace(turn.LastStep)
                                           ? string.Empty
                                           : "  ·  " + turn.LastStep);
        }

        /// <summary>
        /// Appends only the events not rendered yet. Rebuilding a whole conversation on
        /// every tick would rebuild hundreds of elements four times a second.
        /// </summary>
        private void AppendNewEvents(TurnView turn, AgentRunInfo run)
        {
            for (int i = turn.RenderedEvents; i < run.Events.Count; i++)
            {
                AgentEvent agentEvent = run.Events[i];

                if (agentEvent.Kind == AgentEventKind.Text)
                {
                    string text = (agentEvent.Text ?? string.Empty).Trim();
                    if (text.Length == 0)
                        continue;

                    var bubble = new Label(text);
                    JiraStyles.ApplyBubble(bubble, false);
                    turn.Body.Add(bubble);
                    continue;
                }

                VisualElement step = BuildStepRow(agentEvent);
                if (step == null)
                    continue;

                turn.ActivityLog.Add(step);
                turn.StepCount++;
                turn.LastStep = DescribeStep(agentEvent);
            }

            turn.RenderedEvents = run.Events.Count;
            UpdateActivityHeader(turn);

            // Keep the newest line in view while a turn is live.
            if (run.IsRunning)
            {
                _typingDetail = turn.LastStep;
                ScrollToEnd();
            }
        }

        /// <summary>One line of the collapsed step log, or null for events it hides.</summary>
        private static VisualElement BuildStepRow(AgentEvent agentEvent)
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

                case AgentEventKind.Result:
                    // Rendered separately as the turn's answer.
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

        /// <summary>The step summary shown on the collapsed line.</summary>
        private static string DescribeStep(AgentEvent agentEvent)
        {
            string text = agentEvent.Kind == AgentEventKind.ToolUse &&
                          !string.IsNullOrWhiteSpace(agentEvent.Detail)
                ? agentEvent.Text + " " + agentEvent.Detail
                : agentEvent.Text;

            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            return text.Length > 70 ? text.Substring(0, 70) + "..." : text;
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

        /// <summary>Renders the parts of a turn that only exist once it has finished.</summary>
        private void RenderTurnTail(TurnView turn, AgentRunInfo run)
        {
            bool isError = run.Status == AgentRunStatus.Failed || run.Status == AgentRunStatus.Orphaned;

            // The final text repeats the last assistant message on a successful run, so
            // it is only worth a block of its own when it explains a failure.
            if (isError && !string.IsNullOrWhiteSpace(run.FinalText))
            {
                turn.Result.style.display = DisplayStyle.Flex;
                turn.Result.text = run.FinalText;
                JiraStyles.ApplyResultBlock(turn.Result, true);
            }
            else
            {
                turn.Result.style.display = DisplayStyle.None;
            }

            turn.Meta.text = BuildMeta(run);
        }

        private static string BuildMeta(AgentRunInfo run)
        {
            var parts = new List<string> { StatusText(run.Status) };

            if (run.DurationMs > 0)
            {
                parts.Add(L.Tr(L.K.AgentMetaDuration,
                    (run.DurationMs / 1000d).ToString("0.0", CultureInfo.InvariantCulture)));
            }

            if (run.Usage.HasData)
                parts.Add(L.Tr(L.K.AgentMetaTokens, FormatTokens(run.Usage.Total)));

            if (run.CostUsd > 0)
            {
                parts.Add(L.Tr(L.K.AgentMetaCost,
                    run.CostUsd.ToString("0.0000", CultureInfo.InvariantCulture)));
            }

            if (!string.IsNullOrWhiteSpace(run.Model))
                parts.Add(run.Model);

            return string.Join("  ·  ", parts.ToArray());
        }

        private static string FormatTime(DateTime utc)
        {
            return utc == default(DateTime)
                ? string.Empty
                : utc.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }

        private void ScrollToEnd()
        {
            if (_chat != null)
                _chat.scrollOffset = new Vector2(0, float.MaxValue);
        }

        // --- Composer --------------------------------------------------------

        private VisualElement BuildComposer()
        {
            var container = new VisualElement();

            var contextRow = new VisualElement();
            contextRow.style.flexDirection = FlexDirection.Row;
            contextRow.style.alignItems = Align.Center;
            contextRow.style.flexWrap = Wrap.Wrap;
            contextRow.style.marginBottom = 4;

            _issueChip = new Label();
            JiraStyles.ApplyChip(_issueChip, JiraTone.Accent);
            contextRow.Add(_issueChip);

            _unlinkButton = new Button(() =>
            {
                _issueKey = string.Empty;
                _issueSummary = string.Empty;
                _issueDescription = string.Empty;
                _issueBranch = string.Empty;
                RefreshIssueChip();
            })
            {
                text = L.Tr(L.K.BtnAgentUnlinkIssue)
            };
            JiraStyles.ApplyToolbarButton(_unlinkButton, false);
            contextRow.Add(_unlinkButton);

            _permissionDropdown = new DropdownField
            {
                choices = new List<string>
                {
                    L.Tr(L.K.AgentPermissionPlan),
                    L.Tr(L.K.AgentPermissionDefault),
                    L.Tr(L.K.AgentPermissionAcceptEdits)
                }
            };
            _permissionDropdown.index = PermissionToIndex(JiraPreferences.AgentPermission);
            JiraStyles.ApplyMiniDropdown(_permissionDropdown);
            _permissionDropdown.RegisterValueChangedCallback(_ =>
                JiraPreferences.AgentPermission = IndexToPermission(_permissionDropdown.index));
            contextRow.Add(_permissionDropdown);

            _cancelButton = new Button(CancelActiveTurn) { text = L.Tr(L.K.BtnAgentCancel) };
            JiraStyles.ApplyToolbarButton(_cancelButton, false);
            contextRow.Add(_cancelButton);

            container.Add(contextRow);

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.FlexStart;

            _composer = new TextField { multiline = true };
            JiraStyles.ApplyField(_composer);
            JiraStyles.ApplyComposer(_composer);

            // Enter sends, Shift+Enter breaks the line — the convention every chat
            // client uses, and the reason the send button is not the only way in.
            _composer.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool isReturn = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
                if (!isReturn || evt.shiftKey)
                    return;

                evt.StopImmediatePropagation();
                _ = SendAsync();
            }, TrickleDown.TrickleDown);

            inputRow.Add(_composer);

            _sendButton = new Button(() => _ = SendAsync()) { text = L.Tr(L.K.BtnAgentSend) };
            JiraStyles.ApplySendButton(_sendButton);
            inputRow.Add(_sendButton);

            container.Add(inputRow);

            var hint = new Label(L.Tr(L.K.AgentComposerHint));
            JiraStyles.ApplyNote(hint);
            hint.style.marginTop = 4;
            container.Add(hint);

            _status = new Label();
            JiraStyles.ApplyInlineStatus(_status, true);
            container.Add(_status);

            RefreshIssueChip();
            UpdateSendState();
            return container;
        }

        private void RefreshIssueChip()
        {
            if (_issueChip == null)
                return;

            bool linked = !string.IsNullOrWhiteSpace(_issueKey);

            _issueChip.text = linked
                ? _issueKey + (string.IsNullOrWhiteSpace(_issueSummary) ? string.Empty : " — " + _issueSummary)
                : L.Tr(L.K.AgentNoIssue);

            JiraStyles.ApplyChip(_issueChip, linked ? JiraTone.Accent : JiraTone.Neutral);
            _unlinkButton.style.display = linked ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void UpdateSendState()
        {
            if (_sendButton == null)
                return;

            bool busy = AgentService.IsThreadBusy(_threadId);

            _sendButton.SetEnabled(_cliReady && !busy);

            // While busy the label is driven by the animation; this only covers the
            // frames before its first tick.
            if (!busy)
                _sendButton.text = L.Tr(L.K.BtnAgentSend);
            else if (!_sendButton.text.StartsWith(L.Tr(L.K.AgentWorking), StringComparison.Ordinal))
                _sendButton.text = L.Tr(L.K.BtnAgentRunning);

            _cancelButton.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateTypingState(busy);
        }

        private void SetStatus(string message, bool success)
        {
            if (_status == null)
                return;

            _status.text = message ?? string.Empty;
            JiraStyles.ApplyInlineStatus(_status, success);
            _repaint?.Invoke();
        }

        private void CancelActiveTurn()
        {
            foreach (AgentRunInfo run in AgentService.Thread(_threadId))
            {
                if (run.IsRunning)
                    AgentService.Cancel(run.RunId);
            }
        }

        private async Task ResolveWorkingDirectoryAsync()
        {
            _workingDirectory = await AgentService.ResolveWorkingDirectoryAsync();
            _repaint?.Invoke();
        }

        // --- Sending ---------------------------------------------------------

        /// <summary>
        /// Sends the composer's contents as the next message of the conversation.
        /// </summary>
        /// <remarks>
        /// Continuing the CLI session is preferred over starting fresh, and not only
        /// for tidiness: a resumed session already holds the project framing and
        /// everything the agent read, so the follow-up prompt is just the next
        /// instruction. Starting over would re-send and re-pay for all of that context.
        /// When the session cannot be resumed — the previous turn failed before
        /// reporting an id, or the CLI has no resume — the message still starts a new
        /// run inside the same conversation, so the chat is never a dead end.
        /// </remarks>
        private async Task SendAsync()
        {
            string instruction = (_composer?.value ?? string.Empty).Trim();

            if (AgentService.IsThreadBusy(_threadId))
            {
                SetStatus(L.Tr(L.K.MsgAgentBusy), false);
                return;
            }

            AgentRunInfo resumable = AgentService.LastResumable(_threadId);
            bool canResume = resumable != null &&
                             AgentService.CreateRunner(resumable.Provider).SupportsResume;

            // With no issue attached and no session to continue, the instruction is the
            // entire task, so an empty composer would send nothing but boilerplate.
            if (instruction.Length == 0 && (canResume || string.IsNullOrWhiteSpace(_issueKey)))
            {
                SetStatus(L.Tr(canResume ? L.K.MsgAgentNoFollowUp : L.K.MsgAgentNoTask), false);
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingDirectory))
                await ResolveWorkingDirectoryAsync();

            var request = new AgentRequest
            {
                Provider = canResume ? resumable.Provider : Provider,
                WorkingDirectory = _workingDirectory,
                IssueKey = canResume ? resumable.IssueKey : _issueKey,
                Instruction = instruction,
                ThreadId = _threadId,
                Title = AgentPrompt.BuildTitle(canResume ? resumable.IssueKey : _issueKey, instruction),
                PermissionMode = JiraPreferences.AgentPermission
            };

            if (canResume)
            {
                request.Prompt = AgentPrompt.BuildFollowUp(instruction);
                request.ResumeSessionId = resumable.SessionId;

                // Keep the resumed session on the model it started with; switching
                // models mid-session would discard the cached context we are resuming for.
                request.Model = resumable.Model ?? string.Empty;
            }
            else
            {
                request.Prompt = BuildPrompt(instruction);
                request.Model = AgentModelCatalog.Sanitize(
                    Provider, JiraPreferences.GetAgentModel(Provider));
            }

            _composer.value = string.Empty;
            SetStatus(string.Empty, true);
            await LaunchAsync(request);
        }

        /// <summary>
        /// Hands one ai-jira command to the agent as a new conversation.
        /// </summary>
        /// <remarks>
        /// A new thread rather than a follow-up, and not for tidiness: resuming would
        /// drop the routing prompt into a session already holding an unrelated task,
        /// where the previous turn's context competes with the skill's instructions.
        /// These commands are short and self-contained; a clean session is the cheap
        /// option here, not the expensive one.
        /// <para>
        /// Whatever is in the composer rides along as extra context. Someone who typed
        /// "só o que mexi em Player.cs" and then pressed the card button meant the two
        /// together, and silently discarding it would look like the button ignored them.
        /// </para>
        /// </remarks>
        public async void RunAiJiraCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            if (AgentService.IsThreadBusy(_threadId))
            {
                SetStatus(L.Tr(L.K.MsgAgentBusy), false);
                return;
            }

            string extra = (_composer?.value ?? string.Empty).Trim();

            StartNewThread();

            if (string.IsNullOrWhiteSpace(_workingDirectory))
                await ResolveWorkingDirectoryAsync();

            var request = new AgentRequest
            {
                Provider = Provider,
                WorkingDirectory = _workingDirectory,
                Instruction = string.IsNullOrWhiteSpace(extra)
                    ? AiJiraPrompt.Title(command)
                    : AiJiraPrompt.Title(command) + "\n" + extra,
                Prompt = AiJiraPrompt.Build(command, extra, IsPortuguese),
                ThreadId = string.Empty,
                Title = AiJiraPrompt.Title(command),
                PermissionMode = JiraPreferences.AgentPermission,
                Model = AgentModelCatalog.Sanitize(Provider, JiraPreferences.GetAgentModel(Provider))
            };

            if (_composer != null)
                _composer.value = string.Empty;

            SetStatus(string.Empty, true);
            await LaunchAsync(request);
        }

        private string BuildPrompt(string instruction)
        {
            if (string.IsNullOrWhiteSpace(_issueKey))
                return AgentPrompt.BuildFreeTask(instruction, IsPortuguese);

            return AgentPrompt.BuildIssueTask(
                _issueKey, _issueSummary, _issueDescription, instruction, _issueBranch, IsPortuguese);
        }

        private async Task LaunchAsync(AgentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
            {
                await ResolveWorkingDirectoryAsync();
                request.WorkingDirectory = _workingDirectory;
            }

            _sendButton?.SetEnabled(false);
            if (_sendButton != null)
                _sendButton.text = L.Tr(L.K.BtnAgentRunning);

            string failure = null;
            AgentRunInfo run = await AgentService.StartAsync(request, error => failure = error);

            if (run == null)
            {
                // Put the message back rather than losing what was typed.
                if (_composer != null && string.IsNullOrWhiteSpace(_composer.value))
                    _composer.value = request.Instruction ?? string.Empty;

                SetStatus(L.Tr(L.K.MsgAgentStartFailed, failure ?? "?"), false);
                UpdateSendState();
                return;
            }

            _threadId = run.ThreadId;
            RefreshHistory();

            // The turn is appended rather than re-rendering the conversation, so the
            // messages already on screen keep their expanded step logs.
            AppendTurn(run);
            ScrollToEnd();
            UpdateSendState();
        }

        private void OpenInTerminal()
        {
            AgentCliInfo? cached = AgentCliLocator.Cached(Provider);
            AgentRunInfo resumable = AgentService.LastResumable(_threadId);

            var request = new AgentRequest
            {
                Provider = Provider,
                ExecutablePath = cached?.Path ?? string.Empty,
                WorkingDirectory = _workingDirectory,

                // Opening the conversation that is on screen, when there is one, is the
                // point: the terminal picks up where the window left off.
                ResumeSessionId = resumable?.SessionId ?? string.Empty
            };

            IAgentRunner runner = AgentService.CreateRunner(Provider);
            string command = runner.BuildInteractiveCommandLine(request);

            // An interactive session has no stream file, so it is deliberately not
            // added to the history: the window cannot report on what it cannot see.
            if (!AgentShell.OpenInTerminal(command, _workingDirectory, out string error))
                SetStatus(L.Tr(L.K.MsgAgentTerminalFailed, error), false);
        }

        private AgentRunInfo LastTurn()
        {
            List<AgentRunInfo> turns = AgentService.Thread(_threadId);
            return turns.Count == 0 ? null : turns[turns.Count - 1];
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

        // --- Service callbacks -----------------------------------------------

        private void OnRunsChanged()
        {
            if (_root == null)
                return;

            RefreshHistory();
            UpdateSendState();

            foreach (TurnView turn in _turns)
            {
                AgentRunInfo run = AgentService.Find(turn.RunId);
                if (run != null)
                    RenderTurnTail(turn, run);
            }

            _repaint?.Invoke();
        }

        private void OnRunUpdated(AgentRunInfo run)
        {
            if (_root == null || run == null ||
                string.IsNullOrEmpty(_threadId) || run.ThreadId != _threadId)
            {
                return;
            }

            TurnView turn = FindTurn(run.RunId);

            // A turn started elsewhere — the same conversation continued from another
            // window, or a run restored mid-flight — still belongs on screen.
            if (turn == null)
            {
                turn = AppendTurn(run);
            }
            else
            {
                AppendNewEvents(turn, run);
                RenderTurnTail(turn, run);
            }

            UpdateSendState();
            _repaint?.Invoke();
        }

        private TurnView FindTurn(string runId)
        {
            foreach (TurnView turn in _turns)
            {
                if (turn.RunId == runId)
                    return turn;
            }

            return null;
        }
    }
}
