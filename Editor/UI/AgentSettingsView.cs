using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// Everything about the local agent that is configuration rather than
    /// conversation: which CLI, which model, the project instructions, the env file
    /// exported into the run, and the token budget the quota meter measures against.
    /// </summary>
    /// <remarks>
    /// Split out of the agent tab on purpose. Those fields are set once per machine or
    /// per project and were taking up most of a tab whose job is to hold a
    /// conversation. Here they sit beside the other project settings, which is also
    /// where a developer looks for them.
    /// </remarks>
    internal sealed class AgentSettingsView
    {
        private readonly Action _repaint;

        /// <summary>Rebuilds the settings panel, for changes that swap per-provider fields.</summary>
        private readonly Action _rebuild;

        private Label _cliStatus;
        private TextField _cliPathField;

        private Label _skillStatus;

        private Label _envResolved;
        private Label _envSummary;
        private Label _envJiraStatus;
        private Label _envApiKeyWarning;
        private VisualElement _envCreateRow;
        private TextField _envEditor;

        private Label _usageSummary;
        private Label _status;

        private string _workingDirectory = string.Empty;

        public AgentSettingsView(Action repaint, Action rebuild)
        {
            _repaint = repaint;
            _rebuild = rebuild;
        }

        private static string Provider => JiraPreferences.AgentProviderId;

        public VisualElement Build()
        {
            var container = new VisualElement();

            container.Add(BuildCliCard());
            container.Add(BuildSkillCard());
            container.Add(BuildEnvCard());
            container.Add(BuildUsageCard());

            _ = ProbeCliAsync(false);
            _ = ResolveWorkingDirectoryAsync();

            return container;
        }

        private void SetStatus(string message, bool success)
        {
            if (_status == null)
                return;

            _status.text = message ?? string.Empty;
            JiraStyles.ApplyInlineStatus(_status, success);
            _repaint?.Invoke();
        }

        /// <summary>
        /// Resolves the repository the agent works in, then fills the field that
        /// depends on it: where the project instructions are written.
        /// </summary>
        /// <remarks>
        /// The env file is not among them — it is anchored to the project root, not to
        /// the run's working directory, so its card is complete the moment it is built.
        /// </remarks>
        private async Task ResolveWorkingDirectoryAsync()
        {
            _workingDirectory = await AgentService.ResolveWorkingDirectoryAsync();

            RefreshSkillStatus();
            RefreshUsageSummary();
            _repaint?.Invoke();
        }

        // --- CLI -------------------------------------------------------------

        private VisualElement BuildCliCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentSettingsTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            // States plainly that this feature is not the API-key path, because the two
            // sit in the same window and are easy to conflate.
            var authNote = new Label(L.Tr(L.K.AgentNoApiKeyNote));
            JiraStyles.ApplyMuted(authNote);
            card.Add(authNote);

            var providerDropdown = new DropdownField(L.Tr(L.K.AgentProviderLabel))
            {
                choices = BuildProviderLabels()
            };
            providerDropdown.index = Math.Max(0, Array.IndexOf(AgentProvider.All, Provider));
            JiraStyles.ApplyDropdown(providerDropdown);
            providerDropdown.RegisterValueChangedCallback(_ =>
            {
                int index = providerDropdown.index;
                if (index < 0 || index >= AgentProvider.All.Length)
                    return;

                JiraPreferences.AgentProviderId = AgentProvider.All[index];

                // The CLI path field, model list and skill target are all per-provider,
                // so rebuild rather than trying to patch each one in place.
                AgentCliLocator.InvalidateCache();
                _rebuild?.Invoke();
            });
            card.Add(providerDropdown);

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

            // Only shown when the provider actually offers a choice; a dropdown with a
            // single "default" entry would just be furniture.
            if (AgentModelCatalog.HasChoices(Provider))
            {
                string[] modelIds = AgentModelCatalog.Ids(Provider);

                var modelDropdown = new DropdownField(L.Tr(L.K.AgentModelLabel))
                {
                    choices = BuildModelLabels(modelIds)
                };
                modelDropdown.index = Math.Max(0, Array.IndexOf(
                    modelIds, AgentModelCatalog.Sanitize(Provider, JiraPreferences.GetAgentModel(Provider))));
                JiraStyles.ApplyDropdown(modelDropdown);
                modelDropdown.RegisterValueChangedCallback(_ =>
                {
                    int index = modelDropdown.index;
                    if (index >= 0 && index < modelIds.Length)
                        JiraPreferences.SetAgentModel(Provider, modelIds[index]);
                });
                card.Add(modelDropdown);

                var modelNote = new Label(L.Tr(L.K.AgentModelNote));
                JiraStyles.ApplyNote(modelNote);
                card.Add(modelNote);
            }

            var actions = new VisualElement();
            JiraStyles.ApplyButtonRow(actions);

            var check = new Button(() => _ = ProbeCliAsync(true)) { text = L.Tr(L.K.BtnAgentCheckCli) };
            JiraStyles.ApplyCompactButton(check, false);
            actions.Add(check);

            var install = new Button(() => Application.OpenURL(AgentCliLocator.InstallUrl(Provider)))
            {
                text = L.Tr(L.K.BtnAgentInstallCli)
            };
            JiraStyles.ApplyCompactButton(install, false);
            actions.Add(install);

            var copyInstall = new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = AgentCliLocator.InstallCommand(Provider);
                SetStatus(L.Tr(L.K.MsgAgentInstallCopied), true);
            })
            {
                text = L.Tr(L.K.BtnAgentCopyInstall)
            };
            JiraStyles.ApplyCompactButton(copyInstall, false);
            actions.Add(copyInstall);

            // Turns a bare "not found" into something reportable: the exact list of
            // locations that were checked, and whether the host was detected as Windows.
            var diagnostics = new Button(() =>
            {
                AgentCliInfo? probed = AgentCliLocator.Cached(Provider);
                string report = probed.HasValue ? probed.Value.Diagnostics : L.Tr(L.K.AgentCliChecking);

                EditorGUIUtility.systemCopyBuffer = report;
                Debug.Log("[Jira] Agent CLI diagnostics\n\n" + report);
                SetStatus(L.Tr(L.K.MsgAgentDiagnosticsCopied), true);
            })
            {
                text = L.Tr(L.K.BtnAgentDiagnostics)
            };
            JiraStyles.ApplyCompactButton(diagnostics, false);
            actions.Add(diagnostics);

            card.Add(actions);

            _status = new Label();
            JiraStyles.ApplyInlineStatus(_status, true);
            _status.style.marginTop = 8;
            card.Add(_status);

            return card;
        }

        /// <summary>Provider names for the dropdown, in <see cref="AgentProvider.All"/> order.</summary>
        private static List<string> BuildProviderLabels()
        {
            var labels = new List<string>(AgentProvider.All.Length);

            foreach (string provider in AgentProvider.All)
                labels.Add(AgentProvider.DisplayName(provider));

            return labels;
        }

        /// <summary>
        /// Labels for the model dropdown. The default entry is translated; the rest are
        /// shown as their raw CLI id, which keeps the list from going stale in a
        /// package that ships ahead of model releases.
        /// </summary>
        private static List<string> BuildModelLabels(string[] modelIds)
        {
            var labels = new List<string>(modelIds.Length);

            foreach (string id in modelIds)
                labels.Add(string.IsNullOrWhiteSpace(id) ? L.Tr(L.K.AgentModelCliDefault) : id);

            return labels;
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

            _repaint?.Invoke();
        }

        // --- Project instructions (skill) -------------------------------------

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

            var pathCaption = new Label(L.Tr(L.K.AgentSkillPathLabel,
                SkillInstaller.WriterFor(Provider).RelativePath));
            JiraStyles.ApplyDynamicFieldLabel(pathCaption);
            card.Add(pathCaption);

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

            var open = new Button(OpenSkillFile) { text = L.Tr(L.K.BtnAgentOpenSkill) };
            JiraStyles.ApplyCompactButton(open, false);
            row.Add(open);

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
                ? L.Tr(L.K.MsgAgentSkillPresent,
                    SkillInstaller.ResolvePath(Provider, _workingDirectory))
                : L.Tr(L.K.MsgAgentSkillAbsent);
        }

        private void InstallSkill()
        {
            SkillInstallResult result = SkillInstaller.Install(Provider, _workingDirectory);

            if (result.Success)
            {
                SetStatus(L.Tr(L.K.MsgAgentSkillInstalled, result.Path), true);
                AssetDatabase.Refresh();
            }
            else
            {
                SetStatus(L.Tr(L.K.MsgAgentSkillFailed, result.Error), false);
            }

            RefreshSkillStatus();
        }

        private void PreviewSkill()
        {
            // A read-only scratch buffer is enough here, and it avoids writing a file
            // just so the user can look at what would be written.
            string body = SkillInstaller.BuildBody(Provider);
            EditorGUIUtility.systemCopyBuffer = body;
            SetStatus(L.Tr(L.K.MsgAgentCopied), true);
            Debug.Log("[Jira] " + SkillInstaller.WriterFor(Provider).RelativePath + "\n\n" + body);
        }

        /// <summary>Reveals the instruction file, so it can be read where it lives.</summary>
        private void OpenSkillFile()
        {
            string path = SkillInstaller.ResolvePath(Provider, _workingDirectory);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                SetStatus(L.Tr(L.K.MsgAgentSkillAbsent), false);
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        // --- Env file --------------------------------------------------------

        private VisualElement BuildEnvCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentEnvTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var note = new Label(L.Tr(L.K.AgentEnvNote));
            JiraStyles.ApplyMuted(note);
            card.Add(note);

            // The path comes first and always shows where the file actually is. The
            // previous layout led with an empty override field, which read as "there
            // is nothing configured here" even though the file existed.
            _envResolved = new Label();
            JiraStyles.ApplyDynamicFieldLabel(_envResolved);
            card.Add(_envResolved);

            _envJiraStatus = new Label();
            JiraStyles.ApplyFieldHint(_envJiraStatus);
            card.Add(_envJiraStatus);

            _envCreateRow = new VisualElement();
            JiraStyles.ApplyButtonRow(_envCreateRow);
            _envCreateRow.style.marginTop = 0;

            var create = new Button(CreateEnv) { text = L.Tr(L.K.BtnAgentEnvCreate) };
            JiraStyles.ApplyPrimaryButton(create);
            create.style.height = 26;
            _envCreateRow.Add(create);

            card.Add(_envCreateRow);

            _envEditor = new TextField { multiline = true };
            JiraStyles.ApplyField(_envEditor);
            JiraStyles.ApplyMultiline(_envEditor);
            _envEditor.style.minHeight = 150;
            _envEditor.RegisterValueChangedCallback(_ => RefreshEnvSummary());
            card.Add(_envEditor);

            _envSummary = new Label();
            JiraStyles.ApplyFieldHint(_envSummary);
            card.Add(_envSummary);

            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

            var save = new Button(SaveEnv) { text = L.Tr(L.K.BtnAgentEnvSave) };
            JiraStyles.ApplyCompactButton(save, false);
            row.Add(save);

            var reload = new Button(() => RefreshEnv(true)) { text = L.Tr(L.K.BtnAgentEnvReload) };
            JiraStyles.ApplyCompactButton(reload, false);
            row.Add(reload);

            // Saves retyping the token that the Connection tab already holds — and
            // typing it twice is how the two copies end up disagreeing.
            var fill = new Button(FillFromConnection) { text = L.Tr(L.K.BtnAgentEnvFill) };
            JiraStyles.ApplyCompactButton(fill, false);
            row.Add(fill);

            // Answers "is the authentication working?" with the actual HTTP result
            // instead of leaving it to be inferred from what the agent said.
            var test = new Button(() => _ = TestJiraAsync()) { text = L.Tr(L.K.BtnAgentEnvTest) };
            JiraStyles.ApplyCompactButton(test, false);
            row.Add(test);

            var reveal = new Button(() =>
            {
                string path = AgentEnvFile.Resolve();

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    SetStatus(L.Tr(L.K.MsgAgentEnvMissing), false);
                    return;
                }

                EditorUtility.RevealInFinder(path);
            })
            {
                text = L.Tr(L.K.BtnAgentEnvReveal)
            };
            JiraStyles.ApplyCompactButton(reveal, false);
            row.Add(reveal);

            card.Add(row);

            var enabled = new Toggle(L.Tr(L.K.AgentEnvEnabledLabel))
            {
                value = JiraPreferences.AgentEnvEnabled
            };
            enabled.style.marginTop = 10;
            enabled.RegisterValueChangedCallback(evt =>
            {
                JiraPreferences.AgentEnvEnabled = evt.newValue;
                RefreshEnvSummary();
            });
            card.Add(enabled);

            // The override is the rare case, so it sits at the bottom rather than
            // being the first thing the card asks for.
            var pathField = new TextField(L.Tr(L.K.AgentEnvPathLabel))
            {
                value = JiraPreferences.AgentEnvPath
            };
            JiraStyles.ApplyField(pathField);
            pathField.style.marginTop = 8;
            pathField.RegisterCallback<FocusOutEvent>(focusOut =>
            {
                JiraPreferences.AgentEnvPath = pathField.value;

                // A different file means different contents; reload rather than leave
                // the editor showing the previous file's text.
                RefreshEnv(true);
            });
            card.Add(pathField);

            var pathHint = new Label(L.Tr(L.K.AgentEnvPathHint, AgentEnvFile.DefaultFileName));
            JiraStyles.ApplyFieldHint(pathHint);
            card.Add(pathHint);

            _envApiKeyWarning = new Label();
            _envApiKeyWarning.style.display = DisplayStyle.None;
            JiraStyles.ApplyNote(_envApiKeyWarning);
            card.Add(_envApiKeyWarning);

            var warning = new Label(L.Tr(L.K.AgentEnvSecretsNote));
            JiraStyles.ApplyNote(warning);
            card.Add(warning);

            RefreshEnv(true);
            return card;
        }

        /// <summary>Creates the file with the template, for a project that lost it.</summary>
        private void CreateEnv()
        {
            if (!AgentEnvFile.EnsureCreated())
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvFailed, AgentEnvFile.Resolve()), false);
                return;
            }

            AssetDatabase.Refresh();
            RefreshEnv(true);
            SetStatus(L.Tr(L.K.MsgAgentEnvSaved, AgentEnvFile.Resolve()), true);
        }

        /// <summary>
        /// Copies the window's Jira connection into the editor buffer.
        /// </summary>
        /// <remarks>
        /// Deliberately not written to disk here. Putting a personal API token in a
        /// file is a decision, so the developer sees the values land in the editor and
        /// presses save — or does not.
        /// </remarks>
        private void FillFromConnection()
        {
            if (string.IsNullOrWhiteSpace(JiraPreferences.BaseUrl) ||
                string.IsNullOrWhiteSpace(JiraPreferences.Token))
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvNoConnection), false);
                return;
            }

            _envEditor.value = AgentEnvFile.FillFromConnection(_envEditor.value);
            RefreshEnvSummary();
            SetStatus(L.Tr(L.K.MsgAgentEnvFilled), true);
        }

        /// <summary>
        /// Reloads the env file into the editor.
        /// </summary>
        /// <param name="reloadContent">
        /// True whenever the file on disk is the truth we want on screen, which
        /// discards unsaved edits by design.
        /// </param>
        private void RefreshEnv(bool reloadContent)
        {
            if (_envEditor == null)
                return;

            string path = AgentEnvFile.Resolve();
            bool exists = AgentEnvFile.Exists();

            _envResolved.text = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : L.Tr(exists ? L.K.MsgAgentEnvPath : L.K.MsgAgentEnvPathAbsent, path);

            // Creating is only offered when there is nothing there; otherwise the
            // button would be a way to lose the file's contents by accident.
            _envCreateRow.style.display = exists ? DisplayStyle.None : DisplayStyle.Flex;
            _envEditor.SetEnabled(exists);

            if (reloadContent)
                _envEditor.SetValueWithoutNotify(AgentEnvFile.Read());

            RefreshEnvSummary();
        }

        private void RefreshEnvSummary()
        {
            if (_envSummary == null)
                return;

            int count = AgentEnvFile.Parse(_envEditor.value).Count;

            _envSummary.text = JiraPreferences.AgentEnvEnabled
                ? L.Tr(L.K.MsgAgentEnvVars, count.ToString(CultureInfo.InvariantCulture))
                : L.Tr(L.K.MsgAgentEnvDisabled);

            bool connected = AgentEnvFile.HasJiraConnection(_envEditor.value);
            _envJiraStatus.text = L.Tr(connected ? L.K.MsgAgentEnvJiraOk : L.K.MsgAgentEnvJiraMissing);
            JiraStyles.ApplyInlineStatus(_envJiraStatus, connected);

            // An API key set here survives the plan-only guard, which clears the
            // machine's copy but not one the developer wrote on purpose. Saying so is
            // the only way that stays a decision rather than a surprise on the invoice.
            bool billed = false;
            foreach (AgentEnvVariable variable in AgentEnvFile.Parse(_envEditor.value))
            {
                if (variable.Key == "ANTHROPIC_API_KEY" && !string.IsNullOrWhiteSpace(variable.Value))
                    billed = true;
            }

            _envApiKeyWarning.style.display = billed ? DisplayStyle.Flex : DisplayStyle.None;
            _envApiKeyWarning.text = L.Tr(L.K.MsgAgentEnvApiKeyWarning);

            _repaint?.Invoke();
        }

        /// <summary>
        /// Calls Jira with the credentials in the editor, through the same shell a run
        /// uses, and reports what came back.
        /// </summary>
        /// <remarks>
        /// Uses <c>curl</c> rather than the window's own HTTP client on purpose. The
        /// question this button answers is not "are these credentials valid" but "does
        /// the command the agent runs work here" — a proxy, a missing curl or a
        /// corporate TLS interception fails one and not the other.
        /// </remarks>
        private async Task TestJiraAsync()
        {
            string url = null, email = null, token = null;

            foreach (AgentEnvVariable variable in AgentEnvFile.Parse(_envEditor.value))
            {
                if (variable.Key == AgentEnvFile.KeyUrl)
                    url = variable.Value;
                else if (variable.Key == AgentEnvFile.KeyEmail)
                    email = variable.Value;
                else if (variable.Key == AgentEnvFile.KeyToken)
                    token = variable.Value;
            }

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token))
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvJiraMissing), false);
                return;
            }

            SetStatus(L.Tr(L.K.MsgAgentEnvTesting), true);

            string command = "curl -s -u \"" + email + ":" + token + "\" "
                             + "\"" + url.TrimEnd('/') + "/rest/api/3/myself\"";

            ShellResult result = await AgentShell.RunAsync(command, AgentEnvFile.ProjectRoot, 25);
            string body = (result.StdOut ?? string.Empty).Trim();

            if (body.IndexOf("accountId", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvTestOk, ExtractDisplayName(body)), true);
                return;
            }

            // Anything else is the failure the developer needs to read: a 401 body, an
            // HTML login page from a wrong URL, or curl's own error on stderr.
            string detail = body.Length > 0 ? body : (result.StdErr ?? string.Empty).Trim();
            if (detail.Length > 300)
                detail = detail.Substring(0, 300) + " [...]";

            SetStatus(L.Tr(L.K.MsgAgentEnvTestFailed,
                detail.Length == 0 ? "sem resposta / no response" : detail), false);
        }

        /// <summary>Pulls the display name out of the /myself payload, or empty.</summary>
        private static string ExtractDisplayName(string body)
        {
            string name = AgentJson.String(AgentJson.Parse(body), "displayName");
            return string.IsNullOrWhiteSpace(name) ? "?" : name;
        }

        private void SaveEnv()
        {
            string error = AgentEnvFile.Write(_envEditor.value);

            if (error != null)
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvFailed, error), false);
                return;
            }

            AssetDatabase.Refresh();
            SetStatus(L.Tr(L.K.MsgAgentEnvSaved, AgentEnvFile.Resolve()), true);
            RefreshEnv(false);
        }

        // --- Token budget ----------------------------------------------------

        private VisualElement BuildUsageCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AgentBudgetTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var note = new Label(L.Tr(L.K.AgentBudgetNote));
            JiraStyles.ApplyMuted(note);
            card.Add(note);

            var budgetField = new TextField(L.Tr(L.K.AgentBudgetLabel))
            {
                value = JiraPreferences.AgentTokenBudget.ToString(CultureInfo.InvariantCulture)
            };
            JiraStyles.ApplyField(budgetField);
            budgetField.RegisterCallback<FocusOutEvent>(_ =>
            {
                // Digits only, and a bad value clears the budget rather than being
                // silently kept: an unparseable figure must not drive a percentage.
                string digits = Digits(budgetField.value);
                JiraPreferences.AgentTokenBudget =
                    long.TryParse(digits, out long parsed) ? parsed : 0;

                budgetField.SetValueWithoutNotify(
                    JiraPreferences.AgentTokenBudget.ToString(CultureInfo.InvariantCulture));
                RefreshUsageSummary();
            });
            card.Add(budgetField);

            var budgetHint = new Label(L.Tr(L.K.AgentBudgetHint));
            JiraStyles.ApplyFieldHint(budgetHint);
            card.Add(budgetHint);

            var hoursField = new TextField(L.Tr(L.K.AgentWindowHoursLabel))
            {
                value = JiraPreferences.AgentUsageWindowHours.ToString(CultureInfo.InvariantCulture)
            };
            JiraStyles.ApplyField(hoursField);
            hoursField.RegisterCallback<FocusOutEvent>(_ =>
            {
                string digits = Digits(hoursField.value);
                if (int.TryParse(digits, out int parsed))
                    JiraPreferences.AgentUsageWindowHours = parsed;

                hoursField.SetValueWithoutNotify(
                    JiraPreferences.AgentUsageWindowHours.ToString(CultureInfo.InvariantCulture));
                RefreshUsageSummary();
            });
            card.Add(hoursField);

            var hoursHint = new Label(L.Tr(L.K.AgentWindowHoursHint));
            JiraStyles.ApplyFieldHint(hoursHint);
            card.Add(hoursHint);

            _usageSummary = new Label();
            JiraStyles.ApplyFieldHint(_usageSummary);
            card.Add(_usageSummary);

            // Sits with the token numbers because this is the switch that decides
            // whether those numbers are an estimate or an invoice.
            var planOnly = new Toggle(L.Tr(L.K.AgentPlanOnlyLabel))
            {
                value = JiraPreferences.AgentPlanOnly
            };
            planOnly.style.marginTop = 6;
            planOnly.RegisterValueChangedCallback(evt => JiraPreferences.AgentPlanOnly = evt.newValue);
            card.Add(planOnly);

            var planOnlyHint = new Label(L.Tr(L.K.AgentPlanOnlyHint));
            JiraStyles.ApplyFieldHint(planOnlyHint);
            planOnlyHint.style.marginTop = 2;
            card.Add(planOnlyHint);

            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

            var clear = new Button(() =>
            {
                AgentUsageLedger.Clear();
                RefreshUsageSummary();
                SetStatus(L.Tr(L.K.MsgAgentUsageCleared), true);
            })
            {
                text = L.Tr(L.K.BtnAgentClearUsage)
            };
            JiraStyles.ApplyCompactButton(clear, true);
            row.Add(clear);

            card.Add(row);

            var estimateNote = new Label(L.Tr(L.K.AgentUsageEstimateNote));
            JiraStyles.ApplyNote(estimateNote);
            card.Add(estimateNote);

            var costNote = new Label(L.Tr(L.K.AgentCostMeaningNote));
            JiraStyles.ApplyNote(costNote);
            card.Add(costNote);

            RefreshUsageSummary();
            return card;
        }

        private static string Digits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsDigit(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private void RefreshUsageSummary()
        {
            if (_usageSummary == null)
                return;

            AgentUsageWindow window = AgentUsageLedger.CurrentWindow();

            if (!window.Active)
            {
                _usageSummary.text = L.Tr(L.K.MsgAgentUsageIdle);
                _repaint?.Invoke();
                return;
            }

            _usageSummary.text = L.Tr(L.K.MsgAgentUsageWindow,
                window.Usage.Total.ToString("N0", CultureInfo.InvariantCulture),
                window.RunCount.ToString(CultureInfo.InvariantCulture),
                window.StartUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                window.EndUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                window.CostUsd.ToString("0.0000", CultureInfo.InvariantCulture));

            _repaint?.Invoke();
        }
    }
}
