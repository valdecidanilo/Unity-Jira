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
        /// Resolves the repository the agent works in, then fills the fields that
        /// depend on it: the skill path, and the env file's location and contents.
        /// </summary>
        private async Task ResolveWorkingDirectoryAsync()
        {
            _workingDirectory = await AgentService.ResolveWorkingDirectoryAsync();

            RefreshSkillStatus();
            RefreshEnv(true);
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

            var enabled = new Toggle(L.Tr(L.K.AgentEnvEnabledLabel))
            {
                value = JiraPreferences.AgentEnvEnabled
            };
            enabled.style.marginBottom = 8;
            enabled.RegisterValueChangedCallback(evt =>
            {
                JiraPreferences.AgentEnvEnabled = evt.newValue;
                RefreshEnvSummary();
            });
            card.Add(enabled);

            var pathField = new TextField(L.Tr(L.K.AgentEnvPathLabel))
            {
                value = JiraPreferences.AgentEnvPath
            };
            JiraStyles.ApplyField(pathField);
            pathField.RegisterCallback<FocusOutEvent>(_ =>
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

            _envResolved = new Label();
            JiraStyles.ApplyFieldHint(_envResolved);
            card.Add(_envResolved);

            _envEditor = new TextField { multiline = true };
            JiraStyles.ApplyField(_envEditor);
            JiraStyles.ApplyMultiline(_envEditor);
            _envEditor.style.minHeight = 130;
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

            var template = new Button(() =>
            {
                // Only offered as a starting point; it never overwrites on its own.
                _envEditor.value = AgentEnvFile.Template();
                RefreshEnvSummary();
                SetStatus(L.Tr(L.K.MsgAgentEnvTemplate), true);
            })
            {
                text = L.Tr(L.K.BtnAgentEnvTemplate)
            };
            JiraStyles.ApplyCompactButton(template, false);
            row.Add(template);

            var reveal = new Button(() =>
            {
                string path = AgentEnvFile.Resolve(_workingDirectory);

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

            var warning = new Label(L.Tr(L.K.AgentEnvSecretsNote));
            JiraStyles.ApplyNote(warning);
            card.Add(warning);

            RefreshEnv(false);
            return card;
        }

        /// <summary>
        /// Reloads the env file into the editor.
        /// </summary>
        /// <param name="reloadContent">
        /// False while building, when the field is already empty and the working
        /// directory is not known yet; true whenever the file on disk is the truth we
        /// want on screen, which discards unsaved edits by design.
        /// </param>
        private void RefreshEnv(bool reloadContent)
        {
            if (_envEditor == null)
                return;

            string path = AgentEnvFile.Resolve(_workingDirectory);
            bool exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

            _envResolved.text = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : L.Tr(exists ? L.K.MsgAgentEnvPath : L.K.MsgAgentEnvPathAbsent, path);

            if (reloadContent)
                _envEditor.value = AgentEnvFile.Read(_workingDirectory);

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

            _repaint?.Invoke();
        }

        private void SaveEnv()
        {
            string error = AgentEnvFile.Write(_workingDirectory, _envEditor.value);

            if (error != null)
            {
                SetStatus(L.Tr(L.K.MsgAgentEnvFailed, error), false);
                return;
            }

            SetStatus(L.Tr(L.K.MsgAgentEnvSaved, AgentEnvFile.Resolve(_workingDirectory)), true);
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
