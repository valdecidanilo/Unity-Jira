using System;
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
    /// The ai-jira section of the settings tab: install it, and say exactly what is
    /// still missing before its commands can work.
    /// </summary>
    /// <remarks>
    /// Deliberately not a place to do work, which is why it is a settings card and not
    /// a tab. The commands live in the agent chat, typed as <c>/jira-card</c> and
    /// friends, because that is where the conversation they need already happens: these
    /// skills stop and ask for an epic, a type, a team, and an answer to that belongs
    /// in a chat, not in a panel that would have to grow a second one.
    /// <para>
    /// So this card answers one question — can those commands run right now — and
    /// gives the shortest path to yes. The checklist is the whole design: five things
    /// are installed separately from each other, four of them can be absent
    /// independently, and each absence produces a different failure that looks like the
    /// agent misbehaving.
    /// </para>
    /// <para>
    /// Installing runs someone else's script on the developer's machine, so it is a
    /// two-click action: the first click shows the exact command line, the second runs
    /// it. See <see cref="AiJiraInstaller"/>.
    /// </para>
    /// </remarks>
    internal sealed class AiJiraView
    {
        private enum InstallState
        {
            Idle,
            Confirming,
            Running
        }

        private readonly Action _repaint;

        /// <summary>Switches to the agent tab, where the commands are typed.</summary>
        private readonly Action _openAgent;

        private VisualElement _root;
        private VisualElement _statusCard;
        private VisualElement _installCard;
        private VisualElement _commandsCard;
        private Label _status;

        private AiJiraInfo _info;
        private bool _probing;

        private InstallState _installState = InstallState.Idle;
        private string _installLog = string.Empty;

        public AiJiraView(Action repaint, Action openAgent)
        {
            _repaint = repaint;
            _openAgent = openAgent;
        }

        private static string Provider => JiraPreferences.AgentProviderId;

        public VisualElement Build()
        {
            _root = new VisualElement();

            _statusCard = new VisualElement();
            JiraStyles.ApplyCard(_statusCard);
            _root.Add(_statusCard);

            _installCard = new VisualElement();
            JiraStyles.ApplyCard(_installCard);
            _installCard.style.display = DisplayStyle.None;
            _root.Add(_installCard);

            _commandsCard = new VisualElement();
            JiraStyles.ApplyCard(_commandsCard);
            _root.Add(_commandsCard);

            _status = new Label();
            JiraStyles.ApplyInlineStatus(_status, true);
            _root.Add(_status);

            RenderProbing();
            _ = RefreshAsync(false);

            return _root;
        }

        /// <summary>Called when the settings tab becomes visible.</summary>
        public void OnShow()
        {
            // Re-probed every time, unlike the CLI probe: this panel exists to be
            // looked at right after the developer went and fixed one of the rows, and
            // a cached "still missing" would make the fix look like it did nothing.
            _ = RefreshAsync(true);
        }

        private async Task RefreshAsync(bool force)
        {
            if (_probing || _installState == InstallState.Running)
                return;

            _probing = true;

            try
            {
                _info = await AiJiraLocator.LocateAsync(force);
            }
            finally
            {
                _probing = false;
            }

            // The window may have been rebuilt while the probe was in flight.
            if (_root == null)
                return;

            Render();
        }

        private void RenderProbing()
        {
            if (_statusCard == null)
                return;

            _statusCard.Clear();
            _commandsCard.Clear();

            var title = new Label(L.Tr(L.K.AiJiraSectionTitle));
            JiraStyles.ApplySectionTitle(title);
            _statusCard.Add(title);

            var checking = new Label(L.Tr(L.K.AiJiraChecking));
            JiraStyles.ApplyMuted(checking);
            _statusCard.Add(checking);

            _repaint?.Invoke();
        }

        private void Render()
        {
            if (_statusCard == null)
                return;

            _statusCard.Clear();
            _commandsCard.Clear();
            _installCard.Clear();

            RenderStatusCard();
            RenderInstallCard();
            RenderCommandsCard();

            _repaint?.Invoke();
        }

        // --- Status ----------------------------------------------------------

        private void RenderStatusCard()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(L.Tr(L.K.AiJiraSectionTitle));
            JiraStyles.ApplySectionTitle(title);
            title.style.marginBottom = 0;
            title.style.flexGrow = 1;
            header.Add(title);

            var chip = new Label(ChipText());
            JiraStyles.ApplyChip(chip, ChipTone());
            header.Add(chip);

            _statusCard.Add(header);

            var intro = new Label(L.Tr(L.K.AiJiraIntro));
            JiraStyles.ApplyMuted(intro);
            intro.style.marginBottom = 12;
            _statusCard.Add(intro);

            RenderChecklist();
            RenderStatusButtons();
        }

        private string ChipText()
        {
            if (!_info.Found)
                return L.Tr(L.K.AiJiraChipMissing);

            return _info.IsReady ? L.Tr(L.K.AiJiraChipReady) : L.Tr(L.K.AiJiraChipIncomplete);
        }

        private JiraTone ChipTone()
        {
            if (!_info.Found)
                return JiraTone.Neutral;

            return _info.IsReady ? JiraTone.Success : JiraTone.Danger;
        }

        /// <summary>
        /// The five things that have to line up, each with its own fix.
        /// </summary>
        /// <remarks>
        /// Ordered by dependency, not by importance: nothing below the install matters
        /// until the install exists, and <c>config.json</c> cannot be generated before
        /// the credentials resolve. A developer reading top to bottom hits their real
        /// blocker first instead of chasing the last red row.
        /// </remarks>
        private void RenderChecklist()
        {
            _statusCard.Add(Row(
                _info.Found,
                L.Tr(L.K.AiJiraCheckInstall),
                _info.Found ? _info.Home : L.Tr(L.K.AiJiraCheckInstallMissing),
                false));

            _statusCard.Add(Row(
                _info.HasPowerShell,
                L.Tr(L.K.AiJiraCheckPowerShell),
                _info.HasPowerShell ? _info.PowerShellPath : L.Tr(L.K.AiJiraCheckPowerShellMissing),
                false));

            _statusCard.Add(Row(
                _info.HasCredentials,
                L.Tr(L.K.AiJiraCheckCredentials),
                _info.HasCredentials
                    ? L.Tr(L.K.AiJiraCheckCredentialsOk)
                    : L.Tr(L.K.AiJiraCheckCredentialsMissing),
                false));

            _statusCard.Add(Row(
                _info.HasConfig,
                L.Tr(L.K.AiJiraCheckConfig),
                _info.HasConfig ? _info.ConfigPath : L.Tr(L.K.AiJiraCheckConfigMissing),
                false));

            // Optional: only jira-pr needs it, so a red row here must not read like a
            // broken install.
            _statusCard.Add(Row(
                _info.HasGh,
                L.Tr(L.K.AiJiraCheckGh),
                _info.HasGh ? _info.GhPath : L.Tr(L.K.AiJiraCheckGhMissing),
                true));

            if (_info.Found && !AiJiraLocator.SkillsWiredFor(Provider))
            {
                var warning = new Label(L.Tr(L.K.AiJiraSkillsMissing,
                    AgentProvider.DisplayName(Provider)));
                JiraStyles.ApplyNote(warning);
                warning.style.marginTop = 8;
                _statusCard.Add(warning);
            }
        }

        private static VisualElement Row(bool ok, string label, string value, bool optional)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 6;

            // An optional item that is absent is neither a pass nor a failure, and a
            // red cross beside "gh" would send people installing it to fix a card
            // creation that never needed it.
            var mark = new Label(ok ? "✓" : optional ? "–" : "✕");
            mark.style.width = 18;
            mark.style.minWidth = 18;
            mark.style.unityFontStyleAndWeight = FontStyle.Bold;
            mark.style.color = ok
                ? new StyleColor(new Color32(87, 217, 163, 255))
                : optional
                    ? new StyleColor(new Color32(150, 158, 171, 255))
                    : new StyleColor(new Color32(255, 118, 117, 255));
            row.Add(mark);

            var name = new Label(label);
            name.style.width = 118;
            name.style.minWidth = 118;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.fontSize = 11;
            row.Add(name);

            var text = new Label(value);
            JiraStyles.ApplyMuted(text);
            text.style.flexShrink = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);

            return row;
        }

        private void RenderStatusButtons()
        {
            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);
            row.style.marginTop = 10;

            string blocked = AiJiraInstaller.BlockedReason(_info);

            var install = new Button(BeginInstall)
            {
                text = _info.Found ? L.Tr(L.K.BtnAiJiraUpdate) : L.Tr(L.K.BtnAiJiraInstall)
            };
            JiraStyles.ApplyPrimaryButton(install);
            install.SetEnabled(string.IsNullOrEmpty(blocked) && _installState == InstallState.Idle);
            row.Add(install);

            var recheck = new Button(() => _ = RefreshAsync(true))
            {
                text = L.Tr(L.K.BtnAiJiraRecheck)
            };
            JiraStyles.ApplySecondaryButton(recheck);
            row.Add(recheck);

            var repo = new Button(() => Application.OpenURL(AiJiraLocator.RepositoryUrl))
            {
                text = L.Tr(L.K.BtnAiJiraOpenRepo)
            };
            JiraStyles.ApplySecondaryButton(repo);
            row.Add(repo);

            var diagnostics = new Button(CopyDiagnostics)
            {
                text = L.Tr(L.K.BtnAiJiraCopyDiagnostics)
            };
            JiraStyles.ApplySecondaryButton(diagnostics);
            row.Add(diagnostics);

            _statusCard.Add(row);

            if (!string.IsNullOrEmpty(blocked))
            {
                var reason = new Label(InstallBlockedText(blocked));
                JiraStyles.ApplyNote(reason);
                reason.style.marginTop = 6;
                _statusCard.Add(reason);
            }
        }

        private static string InstallBlockedText(string reason)
        {
            switch (reason)
            {
                case "windows-only": return L.Tr(L.K.AiJiraInstallWindowsOnly);
                case "git-missing": return L.Tr(L.K.AiJiraInstallNeedsGit);
                case "powershell-missing": return L.Tr(L.K.AiJiraCheckPowerShellMissing);
                default: return L.Tr(L.K.AiJiraInstallBlocked);
            }
        }

        private void CopyDiagnostics()
        {
            EditorGUIUtility.systemCopyBuffer = _info.Diagnostics;
            SetStatus(L.Tr(L.K.MsgAiJiraCopied), true);
        }

        // --- Install ----------------------------------------------------------

        private void BeginInstall()
        {
            _installState = InstallState.Confirming;
            _installLog = string.Empty;
            Render();
        }

        /// <summary>
        /// The confirmation step, the progress, and the log the run left behind.
        /// </summary>
        /// <remarks>
        /// The command line is printed verbatim before the second click. Running a
        /// script from another repository on the developer's machine is a thing they
        /// should be able to read first — and to reproduce by hand afterwards, which
        /// is what makes a failure diagnosable outside this window.
        /// </remarks>
        private void RenderInstallCard()
        {
            bool visible = _installState != InstallState.Idle || !string.IsNullOrEmpty(_installLog);
            _installCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (!visible)
                return;

            var title = new Label(_info.Found
                ? L.Tr(L.K.AiJiraUpdateTitle)
                : L.Tr(L.K.AiJiraInstallTitle));
            JiraStyles.ApplySectionTitle(title);
            _installCard.Add(title);

            if (_installState == InstallState.Confirming)
            {
                var explain = new Label(L.Tr(L.K.AiJiraInstallExplain));
                JiraStyles.ApplyMuted(explain);
                explain.style.marginBottom = 8;
                _installCard.Add(explain);

                var command = new Label(AiJiraInstaller.DescribeInstall(_info));
                JiraStyles.ApplyResultBlock(command, false);
                _installCard.Add(command);

                var buttons = new VisualElement();
                JiraStyles.ApplyButtonRow(buttons);
                buttons.style.marginTop = 10;

                var confirm = new Button(() => _ = RunInstallAsync())
                {
                    text = L.Tr(L.K.BtnAiJiraInstallConfirm)
                };
                JiraStyles.ApplyPrimaryButton(confirm);
                buttons.Add(confirm);

                var cancel = new Button(() =>
                {
                    _installState = InstallState.Idle;
                    Render();
                })
                {
                    text = L.Tr(L.K.BtnAiJiraInstallCancel)
                };
                JiraStyles.ApplySecondaryButton(cancel);
                buttons.Add(cancel);

                _installCard.Add(buttons);
                return;
            }

            if (_installState == InstallState.Running)
            {
                var running = new Label(L.Tr(L.K.AiJiraInstallRunning));
                JiraStyles.ApplyMuted(running);
                _installCard.Add(running);
                return;
            }

            var log = new Label(_installLog);
            JiraStyles.ApplyResultBlock(log, false);
            _installCard.Add(log);
        }

        private async Task RunInstallAsync()
        {
            _installState = InstallState.Running;
            Render();

            AiJiraInstallResult result = await AiJiraInstaller.RunAsync(_info);

            if (_root == null)
                return;

            _installState = InstallState.Idle;
            _installLog = result.Output ?? string.Empty;

            SetStatus(result.Success
                    ? L.Tr(L.K.MsgAiJiraInstallOk)
                    : L.Tr(L.K.MsgAiJiraInstallFailed, result.Error ?? "?"),
                result.Success);

            // The cache was invalidated by the installer; this is what redraws the
            // checklist from what is actually on disk now.
            await RefreshAsync(true);
        }

        // --- Commands ---------------------------------------------------------

        /// <summary>
        /// What to type in the chat, once the checklist is green.
        /// </summary>
        /// <remarks>
        /// Present even when the install is incomplete, greyed rather than hidden. The
        /// point of the tab is to explain what this thing gives you; hiding the payoff
        /// until setup finishes makes the setup look like busywork.
        /// </remarks>
        private void RenderCommandsCard()
        {
            var title = new Label(L.Tr(L.K.AiJiraCommandsTitle));
            JiraStyles.ApplySectionTitle(title);
            _commandsCard.Add(title);

            var hint = new Label(_info.IsReady
                ? L.Tr(L.K.AiJiraCommandsHint)
                : L.Tr(L.K.AiJiraCommandsPending));
            JiraStyles.ApplyMuted(hint);
            hint.style.marginBottom = 12;
            _commandsCard.Add(hint);

            foreach (string name in AiJiraLocator.KnownCommands)
                _commandsCard.Add(CommandRow(name));

            var open = new Button(() => _openAgent?.Invoke())
            {
                text = L.Tr(L.K.BtnAiJiraOpenChat)
            };
            JiraStyles.ApplySecondaryButton(open);
            open.style.marginTop = 4;
            open.SetEnabled(_openAgent != null);
            _commandsCard.Add(open);
        }

        private static VisualElement CommandRow(string command)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 8;

            var name = new Label("/" + command);
            name.style.width = 96;
            name.style.minWidth = 96;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.fontSize = 11;
            row.Add(name);

            var description = new Label(Description(command));
            JiraStyles.ApplyMuted(description);
            description.style.flexShrink = 1;
            description.style.whiteSpace = WhiteSpace.Normal;
            row.Add(description);

            return row;
        }

        private static string Description(string command)
        {
            switch (command)
            {
                case AiJiraLocator.CommandInit: return L.Tr(L.K.AiJiraInitDesc);
                case AiJiraLocator.CommandCard: return L.Tr(L.K.AiJiraCardDesc);
                case AiJiraLocator.CommandPr: return L.Tr(L.K.AiJiraPrDesc);
                case AiJiraLocator.CommandSync: return L.Tr(L.K.AiJiraSyncDesc);
                default: return string.Empty;
            }
        }

        private void SetStatus(string message, bool success)
        {
            if (_status == null)
                return;

            _status.text = message ?? string.Empty;
            JiraStyles.ApplyInlineStatus(_status, success);
            _repaint?.Invoke();
        }
    }
}
