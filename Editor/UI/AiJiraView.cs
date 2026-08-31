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
    /// The ai-jira tab: the commands a local <c>ai-jira</c> install adds, and an
    /// honest account of whether each one can actually run right now.
    /// </summary>
    /// <remarks>
    /// The tab only exists on a machine that has ai-jira, so the panel spends its
    /// space on the half that is not guaranteed: the PowerShell host, the GitHub CLI,
    /// and whether <c>install.ps1</c> ever wired the skills into the agent CLI this
    /// project is configured for. Each of those fails in a way that looks like the
    /// agent misbehaving rather than a missing dependency, which is exactly the
    /// confusion worth spending a card on.
    /// <para>
    /// Nothing here runs a script. Pressing a command hands it to the agent tab, which
    /// already owns the conversation, the transcript, the quota meter and the cancel
    /// button — and ai-jira's own skills are written to be driven by an agent, not
    /// called as a library. A second execution path would be a second set of bugs for
    /// no new capability.
    /// </para>
    /// </remarks>
    internal sealed class AiJiraView
    {
        private readonly Action _repaint;

        /// <summary>Hands one command over to the agent tab.</summary>
        private readonly Action<string> _dispatch;

        private VisualElement _root;
        private VisualElement _statusCard;
        private VisualElement _commandsCard;
        private Label _status;

        private AiJiraInfo _info;
        private bool _probing;

        public AiJiraView(Action repaint, Action<string> dispatch)
        {
            _repaint = repaint;
            _dispatch = dispatch;
        }

        private static string Provider => JiraPreferences.AgentProviderId;

        public VisualElement Build()
        {
            _root = new VisualElement();

            _statusCard = new VisualElement();
            JiraStyles.ApplyCard(_statusCard);
            _root.Add(_statusCard);

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

        /// <summary>Called when the tab becomes visible.</summary>
        public void OnShow()
        {
            // A cached result is enough here: an install appearing while the window is
            // open is rare, and the explicit re-check button covers it. Re-probing on
            // every tab switch would shell out for pwsh and gh each time.
            if (AiJiraLocator.Cached.HasValue)
                Render();
            else
                _ = RefreshAsync(false);
        }

        private async Task RefreshAsync(bool force)
        {
            if (_probing)
                return;

            _probing = true;
            RenderProbing();

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
            _commandsCard.style.display = DisplayStyle.None;

            var title = new Label("ai-jira");
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

            RenderStatusCard();

            _commandsCard.style.display = _info.Found ? DisplayStyle.Flex : DisplayStyle.None;
            if (_info.Found)
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

            var title = new Label("ai-jira");
            JiraStyles.ApplySectionTitle(title);
            title.style.marginBottom = 0;
            title.style.flexGrow = 1;
            header.Add(title);

            var chip = new Label(_info.Found
                ? L.Tr(L.K.AiJiraChipFound)
                : L.Tr(L.K.AiJiraChipMissing));
            JiraStyles.ApplyChip(chip, _info.Found ? JiraTone.Success : JiraTone.Neutral);
            header.Add(chip);

            _statusCard.Add(header);

            if (!_info.Found)
            {
                RenderMissing();
                return;
            }

            var intro = new Label(L.Tr(L.K.AiJiraIntro));
            JiraStyles.ApplyMuted(intro);
            intro.style.marginBottom = 10;
            _statusCard.Add(intro);

            _statusCard.Add(Detail(L.Tr(L.K.AiJiraHomeLabel), _info.Home));

            RenderRequirements();
            RenderStatusButtons();
        }

        /// <summary>
        /// The three things that are installed separately from the scripts.
        /// </summary>
        /// <remarks>
        /// Reported as warnings, not as errors that hide the commands. A missing
        /// <c>gh</c> only stops <c>jira-pr</c>; missing skill pointers only stop the
        /// agent from recognising the command by name. Blanking the whole tab for
        /// either would hide the three commands that still work.
        /// </remarks>
        private void RenderRequirements()
        {
            if (!_info.HasPowerShell)
                _statusCard.Add(Warning(L.Tr(L.K.AiJiraPowerShellMissing)));

            if (!_info.HasGh)
                _statusCard.Add(Warning(L.Tr(L.K.AiJiraGhMissing)));

            if (!AiJiraLocator.SkillsWiredFor(Provider))
            {
                _statusCard.Add(Warning(L.Tr(L.K.AiJiraSkillsMissing,
                    AgentProvider.DisplayName(Provider))));
            }
        }

        private void RenderMissing()
        {
            var text = new Label(L.Tr(L.K.AiJiraMissingText));
            JiraStyles.ApplyMuted(text);
            text.style.marginBottom = 10;
            _statusCard.Add(text);

            RenderStatusButtons();
        }

        private void RenderStatusButtons()
        {
            var row = new VisualElement();
            JiraStyles.ApplyButtonRow(row);

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
        }

        private void CopyDiagnostics()
        {
            EditorGUIUtility.systemCopyBuffer = _info.Diagnostics;
            SetStatus(L.Tr(L.K.MsgAiJiraCopied), true);
        }

        // --- Commands --------------------------------------------------------

        private void RenderCommandsCard()
        {
            var title = new Label(L.Tr(L.K.AiJiraCommandsTitle));
            JiraStyles.ApplySectionTitle(title);
            _commandsCard.Add(title);

            var hint = new Label(L.Tr(L.K.AiJiraDispatchHint));
            JiraStyles.ApplyMuted(hint);
            hint.style.marginBottom = 12;
            _commandsCard.Add(hint);

            foreach (string name in AiJiraLocator.KnownCommands)
                _commandsCard.Add(BuildCommandRow(_info.Command(name)));
        }

        private VisualElement BuildCommandRow(AiJiraCommand command)
        {
            var card = new VisualElement();
            JiraStyles.ApplyNestedCard(card);

            var title = new Label(command.Name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            card.Add(title);

            var description = new Label(Description(command.Name));
            JiraStyles.ApplyMuted(description);
            description.style.marginBottom = 8;
            card.Add(description);

            string blocked = BlockedReason(command);

            var button = new Button(() => Dispatch(command.Name))
            {
                text = ButtonText(command.Name)
            };
            JiraStyles.ApplyPrimaryButton(button);
            button.SetEnabled(string.IsNullOrEmpty(blocked));
            card.Add(button);

            if (!string.IsNullOrEmpty(blocked))
            {
                var reason = new Label(blocked);
                JiraStyles.ApplyNote(reason);
                reason.style.marginTop = 6;
                card.Add(reason);
            }

            return card;
        }

        /// <summary>Why a command cannot run right now, or empty when it can.</summary>
        private string BlockedReason(AiJiraCommand command)
        {
            if (!command.Available)
                return L.Tr(L.K.AiJiraCommandMissing);

            if (!_info.HasPowerShell)
                return L.Tr(L.K.AiJiraPowerShellMissing);

            if (command.RequiresGh && !_info.HasGh)
                return L.Tr(L.K.AiJiraGhMissing);

            return string.Empty;
        }

        private void Dispatch(string command)
        {
            if (_dispatch == null)
                return;

            _dispatch(command);
            SetStatus(L.Tr(L.K.MsgAiJiraDispatched, command), true);
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

        private static string ButtonText(string command)
        {
            switch (command)
            {
                case AiJiraLocator.CommandInit: return L.Tr(L.K.BtnAiJiraInit);
                case AiJiraLocator.CommandCard: return L.Tr(L.K.BtnAiJiraCard);
                case AiJiraLocator.CommandPr: return L.Tr(L.K.BtnAiJiraPr);
                case AiJiraLocator.CommandSync: return L.Tr(L.K.BtnAiJiraSync);
                default: return command;
            }
        }

        // --- Small helpers ----------------------------------------------------

        private static VisualElement Detail(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 10;

            var caption = new Label(label);
            JiraStyles.ApplyMuted(caption);
            caption.style.marginRight = 8;
            row.Add(caption);

            var text = new Label(value);
            JiraStyles.ApplyMuted(text);
            text.style.flexShrink = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);

            return row;
        }

        private static Label Warning(string message)
        {
            var label = new Label(message);
            JiraStyles.ApplyNote(label);
            label.style.marginBottom = 8;
            return label;
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
