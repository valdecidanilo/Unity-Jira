using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.AI;
using OxenteGames.JiraCommunication.API;
using OxenteGames.JiraCommunication.Models;
using OxenteGames.JiraCommunication.Settings;
using OxenteGames.JiraCommunication.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using L = OxenteGames.JiraCommunication.Localization.JiraLoc;

namespace OxenteGames.JiraCommunication
{
    internal sealed class JiraWindow : EditorWindow
    {
        private const string WindowTitle = "Jira";
        private const string FieldPriority = "priority";
        private const string FieldAssignee = "assignee";
        private const string FieldDueDate = "duedate";

        private enum Tab { Connection, Create, Settings }

        // Connection tab
        private TextField _urlField;
        private TextField _emailField;
        private TextField _tokenField;
        private Button _connectButton;
        private Label _statusLabel;
        private VisualElement _connectedCard;
        private Label _connectedUserLabel;
        private Label _connectedEmailLabel;
        private bool _isConnecting;

        // Tabs
        private Button _connectionTab;
        private Button _createTab;
        private Button _settingsTab;
        private VisualElement _connectionPanel;
        private VisualElement _createPanel;
        private VisualElement _settingsPanel;
        private Tab _activeTab = Tab.Connection;

        // Create tab - core
        private VisualElement _createNotice;
        private VisualElement _createForm;
        private DropdownField _projectDropdown;
        private DropdownField _typeDropdown;
        private VisualElement _epicContainer;
        private DropdownField _epicDropdown;
        private DropdownField _sprintDropdown;
        private VisualElement _parentContainer;
        private TextField _parentField;
        private TextField _summaryField;
        private TextField _descriptionField;
        private Button _createButton;
        private Label _createStatus;
        private Button _openIssueButton;

        // Create tab - dynamic fields
        private VisualElement _classifyCard;
        private VisualElement _classifyContent;
        private VisualElement _datesCard;
        private VisualElement _datesContent;
        private DropdownField _priorityDropdown;
        private JiraFieldMeta _priorityMeta;
        private DropdownField _teamDropdown;
        private TextField _teamText;
        private JiraFieldMeta _teamMeta;
        private DropdownField _assigneeDropdown;
        private JiraFieldMeta _assigneeMeta;
        private TextField _startDateField;
        private JiraFieldMeta _startDateMeta;
        private TextField _dueDateField;
        private JiraFieldMeta _dueDateMeta;

        // Attachment
        private string _attachmentPath = string.Empty;
        private Label _attachmentLabel;

        // AI assistant
        private TextField _aiPromptField;
        private Button _aiGenerateButton;
        private bool _isAiBusy;
        private static readonly string[] ClaudeModelLabels = { "Claude Sonnet 5", "Claude Haiku 4.5", "Claude Opus 5" };
        private static readonly string[] ClaudeModelIds = { "claude-sonnet-5", "claude-haiku-4-5", "claude-opus-5" };
        private static readonly string[] OpenAiModelLabels = { "GPT-4o", "GPT-4o mini", "GPT-4.1" };
        private static readonly string[] OpenAiModelIds = { "gpt-4o", "gpt-4o-mini", "gpt-4.1" };

        // Epic progress
        private VisualElement _epicProgressContainer;
        private Label _epicProgressLabel;
        private VisualElement _epicProgressFill;

        private readonly List<JiraProject> _projects = new List<JiraProject>();
        private readonly List<JiraIssueType> _issueTypes = new List<JiraIssueType>();
        private readonly List<JiraEpic> _epics = new List<JiraEpic>();
        private readonly List<JiraSprint> _sprints = new List<JiraSprint>();
        private readonly List<JiraUser> _assignableUsers = new List<JiraUser>();
        private JiraUser _myself;
        private int _activeBoardId = -1;
        private bool _isCreating;
        private bool _projectsLoaded;

        [MenuItem("Jira/Open Jira Workspace", priority = 0)]
        public static void Open() => ShowWindow(Tab.Connection);

        [MenuItem("Jira/Create Issue", priority = 1)]
        public static void OpenCreate() => ShowWindow(Tab.Create);

        [MenuItem("Jira/Settings", priority = 2)]
        public static void OpenSettings() => ShowWindow(Tab.Settings);

        [MenuItem("Jira/Documentation", priority = 100)]
        private static void OpenDocumentation()
        {
            Application.OpenURL("https://developer.atlassian.com/cloud/jira/platform/rest/v3/intro/");
        }

        [MenuItem("Jira/Create API Token", priority = 101)]
        private static void OpenApiTokenPage()
        {
            Application.OpenURL("https://id.atlassian.com/manage-profile/security/api-tokens");
        }

        private static void ShowWindow(Tab tab)
        {
            JiraWindow window = GetWindow<JiraWindow>();
            window.titleContent = new GUIContent(WindowTitle, LoadIcon());
            window.minSize = new Vector2(560, 660);
            window._activeTab = tab;
            window.Show();
            window.SelectTab(tab);
        }

        private static Texture2D LoadIcon() => Resources.Load<Texture2D>("jira-icon");

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            JiraStyles.ApplyWindow(rootVisualElement);

            BuildHeader();
            BuildTabBar();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 22;
            scroll.style.paddingRight = 22;
            scroll.style.paddingTop = 20;
            scroll.style.paddingBottom = 20;
            rootVisualElement.Add(scroll);

            _connectionPanel = BuildConnectionPanel();
            _createPanel = BuildCreatePanel();
            _settingsPanel = BuildSettingsPanel();
            scroll.Add(_connectionPanel);
            scroll.Add(_createPanel);
            scroll.Add(_settingsPanel);

            BuildBrandFooter();

            RefreshConnectionState();
            SelectTab(_activeTab);
        }

        private void BuildBrandFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 22;
            footer.style.paddingRight = 22;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 8;
            footer.style.backgroundColor = new StyleColor(new Color32(40, 44, 52, 255));
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(new Color32(67, 73, 84, 255));

            var brand = new Label("OxenteGames");
            brand.style.fontSize = 11;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.color = new StyleColor(new Color32(173, 181, 194, 255));
            footer.Add(brand);

            rootVisualElement.Add(footer);
        }

        // --- Header & tabs --------------------------------------------------

        private void BuildHeader()
        {
            var header = new VisualElement();
            JiraStyles.ApplyHeader(header);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.Add(BuildLogo());

            var textColumn = new VisualElement();
            textColumn.style.flexGrow = 1;

            var title = new Label("Jira Communication");
            JiraStyles.ApplyTitle(title);

            var subtitle = new Label(L.Tr(L.K.HeaderSubtitle));
            JiraStyles.ApplySubtitle(subtitle);

            textColumn.Add(title);
            textColumn.Add(subtitle);
            titleRow.Add(textColumn);
            header.Add(titleRow);
            rootVisualElement.Add(header);
        }

        private static VisualElement BuildLogo()
        {
            Texture2D icon = LoadIcon();
            if (icon != null)
            {
                var logo = new VisualElement();
                logo.style.width = 36;
                logo.style.height = 36;
                logo.style.marginRight = 11;
                logo.style.backgroundImage = new StyleBackground(icon);
#if UNITY_2022_1_OR_NEWER
                logo.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                logo.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                logo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
#else
                logo.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#endif
                return logo;
            }

            var fallback = new Label("J");
            fallback.style.width = 36;
            fallback.style.height = 36;
            fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
            fallback.style.fontSize = 18;
            fallback.style.unityFontStyleAndWeight = FontStyle.Bold;
            fallback.style.backgroundColor = new StyleColor(new Color32(38, 132, 255, 255));
            fallback.style.color = Color.white;
            fallback.style.borderTopLeftRadius = 7;
            fallback.style.borderTopRightRadius = 7;
            fallback.style.borderBottomLeftRadius = 7;
            fallback.style.borderBottomRightRadius = 7;
            fallback.style.marginRight = 11;
            return fallback;
        }

        private void BuildTabBar()
        {
            var bar = new VisualElement();
            JiraStyles.ApplyTabBar(bar);

            _connectionTab = new Button(() => SelectTab(Tab.Connection)) { text = L.Tr(L.K.TabConnection) };
            _createTab = new Button(() => SelectTab(Tab.Create)) { text = L.Tr(L.K.TabCreate) };
            _settingsTab = new Button(() => SelectTab(Tab.Settings)) { text = L.Tr(L.K.TabSettings) };

            bar.Add(_connectionTab);
            bar.Add(_createTab);
            bar.Add(_settingsTab);
            rootVisualElement.Add(bar);
        }

        private void SelectTab(Tab tab)
        {
            _activeTab = tab;
            if (_connectionPanel == null || _createPanel == null || _settingsPanel == null)
                return;

            _connectionPanel.style.display = tab == Tab.Connection ? DisplayStyle.Flex : DisplayStyle.None;
            _createPanel.style.display = tab == Tab.Create ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPanel.style.display = tab == Tab.Settings ? DisplayStyle.Flex : DisplayStyle.None;

            JiraStyles.ApplyTab(_connectionTab, tab == Tab.Connection);
            JiraStyles.ApplyTab(_createTab, tab == Tab.Create);
            JiraStyles.ApplyTab(_settingsTab, tab == Tab.Settings);

            if (tab == Tab.Create)
                RefreshCreateAvailability();
        }

        // --- Connection panel ----------------------------------------------

        private VisualElement BuildConnectionPanel()
        {
            var panel = new VisualElement();
            panel.Add(BuildConnectionCard());
            panel.Add(BuildConnectedCard());
            return panel;
        }

        private VisualElement BuildConnectionCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var sectionTitle = new Label(L.Tr(L.K.ConnSectionTitle));
            JiraStyles.ApplySectionTitle(sectionTitle);
            card.Add(sectionTitle);

            var helper = new Label(L.Tr(L.K.ConnHelper));
            JiraStyles.ApplyMuted(helper);
            helper.style.marginBottom = 14;
            card.Add(helper);

            _urlField = new TextField(L.Tr(L.K.FieldUrl)) { value = JiraPreferences.BaseUrl };
            _urlField.tooltip = L.Tr(L.K.FieldUrlTooltip);
            JiraStyles.ApplyField(_urlField);
            card.Add(_urlField);

            _emailField = new TextField(L.Tr(L.K.FieldEmail)) { value = JiraPreferences.Email };
            JiraStyles.ApplyField(_emailField);
            card.Add(_emailField);

            _tokenField = new TextField(L.Tr(L.K.FieldToken))
            {
                value = JiraPreferences.SessionToken,
                isPasswordField = true
            };
            JiraStyles.ApplyField(_tokenField);
            card.Add(_tokenField);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 4;

            _connectButton = new Button(TestConnectionAsync) { text = L.Tr(L.K.BtnConnect) };
            _connectButton.style.flexGrow = 1;
            JiraStyles.ApplyPrimaryButton(_connectButton);

            var createTokenButton = new Button(OpenApiTokenPage) { text = L.Tr(L.K.BtnCreateToken) };
            JiraStyles.ApplySecondaryButton(createTokenButton);

            actions.Add(_connectButton);
            actions.Add(createTokenButton);
            card.Add(actions);

            _statusLabel = new Label();
            _statusLabel.style.display = DisplayStyle.None;
            card.Add(_statusLabel);

            return card;
        }

        private VisualElement BuildConnectedCard()
        {
            _connectedCard = new VisualElement();
            JiraStyles.ApplyCard(_connectedCard);

            var sectionTitle = new Label(L.Tr(L.K.ConnectedTitle));
            JiraStyles.ApplySectionTitle(sectionTitle);
            _connectedCard.Add(sectionTitle);

            _connectedUserLabel = new Label("Atlassian");
            _connectedUserLabel.style.fontSize = 14;
            _connectedUserLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _connectedCard.Add(_connectedUserLabel);

            _connectedEmailLabel = new Label();
            JiraStyles.ApplyMuted(_connectedEmailLabel);
            _connectedEmailLabel.style.marginTop = 3;
            _connectedEmailLabel.style.marginBottom = 12;
            _connectedCard.Add(_connectedEmailLabel);

            var goToCreate = new Button(() => SelectTab(Tab.Create)) { text = L.Tr(L.K.BtnGoToCreate) };
            JiraStyles.ApplySecondaryButton(goToCreate);
            goToCreate.style.marginBottom = 8;
            _connectedCard.Add(goToCreate);

            var disconnectButton = new Button(Disconnect) { text = L.Tr(L.K.BtnDisconnect) };
            JiraStyles.ApplySecondaryButton(disconnectButton);
            _connectedCard.Add(disconnectButton);

            return _connectedCard;
        }

        // --- Create panel ---------------------------------------------------

        private VisualElement BuildCreatePanel()
        {
            var panel = new VisualElement();

            _createNotice = new VisualElement();
            JiraStyles.ApplyCard(_createNotice);
            var noticeTitle = new Label(L.Tr(L.K.CreateNoticeTitle));
            JiraStyles.ApplySectionTitle(noticeTitle);
            var noticeText = new Label(L.Tr(L.K.CreateNoticeText));
            JiraStyles.ApplyMuted(noticeText);
            var noticeButton = new Button(() => SelectTab(Tab.Connection)) { text = L.Tr(L.K.BtnOpenConnTab) };
            JiraStyles.ApplySecondaryButton(noticeButton);
            noticeButton.style.marginTop = 12;
            _createNotice.Add(noticeTitle);
            _createNotice.Add(noticeText);
            _createNotice.Add(noticeButton);
            panel.Add(_createNotice);

            _createForm = new VisualElement();
            _createForm.Add(BuildDestinationCard());
            _createForm.Add(BuildClassifyCard());
            _createForm.Add(BuildDatesCard());
            _createForm.Add(BuildAiCard());
            _createForm.Add(BuildDetailsCard());
            _createForm.Add(BuildAttachmentCard());
            _createForm.Add(BuildFooter());
            panel.Add(_createForm);

            return panel;
        }

        private VisualElement BuildDestinationCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.CreateDestTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _projectDropdown = new DropdownField(L.Tr(L.K.FieldProject));
            JiraStyles.ApplyDropdown(_projectDropdown);
            _projectDropdown.RegisterValueChangedCallback(_ => OnProjectSelected());

            _typeDropdown = new DropdownField(L.Tr(L.K.FieldIssueType));
            JiraStyles.ApplyDropdown(_typeDropdown);
            _typeDropdown.RegisterValueChangedCallback(_ => OnTypeSelected());

            card.Add(JiraStyles.Row(_projectDropdown, _typeDropdown));

            _parentContainer = new VisualElement();
            _parentField = new TextField(L.Tr(L.K.FieldParent));
            _parentField.tooltip = L.Tr(L.K.FieldParentTooltip);
            JiraStyles.ApplyField(_parentField);
            var parentHint = new Label(L.Tr(L.K.ParentHint));
            JiraStyles.ApplyFieldHint(parentHint);
            _parentContainer.Add(_parentField);
            _parentContainer.Add(parentHint);
            _parentContainer.style.display = DisplayStyle.None;
            card.Add(_parentContainer);

            _epicContainer = new VisualElement();
            _epicDropdown = new DropdownField(L.Tr(L.K.FieldEpic));
            _epicDropdown.tooltip = L.Tr(L.K.FieldEpicTooltip);
            JiraStyles.ApplyDropdown(_epicDropdown);
            _epicDropdown.RegisterValueChangedCallback(_ => OnEpicSelected());
            _epicContainer.Add(_epicDropdown);

            _sprintDropdown = new DropdownField(L.Tr(L.K.FieldSprint));
            _sprintDropdown.tooltip = L.Tr(L.K.FieldSprintTooltip);
            JiraStyles.ApplyDropdown(_sprintDropdown);

            card.Add(JiraStyles.Row(_epicContainer, _sprintDropdown));
            card.Add(BuildEpicProgress());

            var refreshButton = new Button(() => ReloadProjectsAsync()) { text = L.Tr(L.K.BtnReloadProjects) };
            JiraStyles.ApplyGhostButton(refreshButton);
            card.Add(refreshButton);

            return card;
        }

        private VisualElement BuildClassifyCard()
        {
            _classifyCard = new VisualElement();
            JiraStyles.ApplyCard(_classifyCard);

            var title = new Label(L.Tr(L.K.CreateClassifyTitle));
            JiraStyles.ApplySectionTitle(title);
            _classifyCard.Add(title);

            _classifyContent = new VisualElement();
            _classifyCard.Add(_classifyContent);

            _classifyCard.style.display = DisplayStyle.None;
            return _classifyCard;
        }

        private VisualElement BuildDatesCard()
        {
            _datesCard = new VisualElement();
            JiraStyles.ApplyCard(_datesCard);

            var title = new Label(L.Tr(L.K.CreateDatesTitle));
            JiraStyles.ApplySectionTitle(title);
            _datesCard.Add(title);

            _datesContent = new VisualElement();
            _datesCard.Add(_datesContent);

            _datesCard.style.display = DisplayStyle.None;
            return _datesCard;
        }

        private VisualElement BuildDetailsCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.CreateDetailsTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _summaryField = new TextField(L.Tr(L.K.FieldSummary));
            JiraStyles.ApplyField(_summaryField);
            card.Add(_summaryField);

            _descriptionField = new TextField(L.Tr(L.K.FieldDescription));
            JiraStyles.ApplyMultiline(_descriptionField);
            card.Add(_descriptionField);

            return card;
        }

        private VisualElement BuildAttachmentCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.CreateAttachmentTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var selectButton = new Button(SelectAttachment) { text = L.Tr(L.K.BtnSelectFile) };
            JiraStyles.ApplyGhostButton(selectButton);
            selectButton.style.marginRight = 8;
            row.Add(selectButton);

            var removeButton = new Button(ClearAttachment) { text = L.Tr(L.K.BtnRemoveFile) };
            JiraStyles.ApplyGhostButton(removeButton);
            row.Add(removeButton);

            card.Add(row);

            _attachmentLabel = new Label(L.Tr(L.K.NoFileSelected));
            JiraStyles.ApplyFieldHint(_attachmentLabel);
            _attachmentLabel.style.marginTop = 8;
            card.Add(_attachmentLabel);

            return card;
        }

        private VisualElement BuildAiCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AiSectionTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _aiPromptField = new TextField(L.Tr(L.K.AiPromptLabel));
            JiraStyles.ApplyMultiline(_aiPromptField);
            _aiPromptField.style.minHeight = 56;
            card.Add(_aiPromptField);

            _aiGenerateButton = new Button(GenerateWithAiAsync) { text = L.Tr(L.K.BtnAiGenerate) };
            JiraStyles.ApplySecondaryButton(_aiGenerateButton);
            card.Add(_aiGenerateButton);

            return card;
        }

        private async void GenerateWithAiAsync()
        {
            if (_isAiBusy)
                return;

            string provider = JiraPreferences.AiProvider;
            string token = JiraPreferences.GetAiToken(provider);
            if (string.IsNullOrWhiteSpace(token))
            {
                SetCreateStatus(L.Tr(L.K.MsgAiNoToken), false);
                SelectTab(Tab.Settings);
                return;
            }

            string input = _aiPromptField.value?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                SetCreateStatus(L.Tr(L.K.MsgAiNoInput), false);
                return;
            }

            JiraProject project = SelectedProject();
            JiraIssueType type = SelectedIssueType();

            var priorityNames = new List<string>();
            if (_priorityMeta?.allowedValues != null)
            {
                foreach (JiraAllowedValue value in _priorityMeta.allowedValues)
                    priorityNames.Add(value.Display);
            }

            SetAiBusy(true);
            SetCreateStatus(L.Tr(L.K.MsgAiGenerating), true);

            try
            {
                string model = JiraPreferences.GetAiModel(provider);
                IAiIssueClient client = provider == JiraPreferences.ProviderOpenAi
                    ? (IAiIssueClient)new OpenAiClient(token, model)
                    : new ClaudeClient(token, model);

                AiSuggestion suggestion = await client.SuggestIssueAsync(
                    input, project?.name, type?.name, priorityNames, L.Current != L.En);

                if (!string.IsNullOrWhiteSpace(suggestion.title))
                    _summaryField.value = suggestion.title.Trim();

                if (!string.IsNullOrWhiteSpace(suggestion.description))
                    _descriptionField.value = suggestion.description;

                if (!string.IsNullOrWhiteSpace(suggestion.priority))
                    SelectPriorityByName(suggestion.priority);

                SetCreateStatus(L.Tr(L.K.MsgAiDone), true);
            }
            catch (Exception exception)
            {
                SetCreateStatus(L.Tr(L.K.MsgAiFailed, exception.Message), false);
            }
            finally
            {
                SetAiBusy(false);
            }
        }

        private void SelectPriorityByName(string name)
        {
            if (_priorityDropdown == null || _priorityMeta?.allowedValues == null)
                return;

            string lower = name.Trim().ToLowerInvariant();
            for (int i = 0; i < _priorityMeta.allowedValues.Length; i++)
            {
                JiraAllowedValue value = _priorityMeta.allowedValues[i];
                string display = (value.Display ?? string.Empty).ToLowerInvariant();
                string plain = (value.name ?? string.Empty).ToLowerInvariant();

                if (display == lower || plain == lower ||
                    (display.Length > 0 && (display.Contains(lower) || lower.Contains(display))))
                {
                    _priorityDropdown.index = i;
                    return;
                }
            }
        }

        private void SetAiBusy(bool busy)
        {
            _isAiBusy = busy;
            _aiGenerateButton.SetEnabled(!busy);
            _aiGenerateButton.text = busy ? L.Tr(L.K.BtnAiGenerating) : L.Tr(L.K.BtnAiGenerate);
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();

            var statusNote = new Label(L.Tr(L.K.StatusNote));
            JiraStyles.ApplyNote(statusNote);
            footer.Add(statusNote);

            var presetNote = new Label(L.Tr(L.K.PresetNote));
            JiraStyles.ApplyNote(presetNote);
            presetNote.style.marginBottom = 8;
            footer.Add(presetNote);

            _createButton = new Button(CreateIssueAsync) { text = L.Tr(L.K.BtnCreate) };
            JiraStyles.ApplyPrimaryButton(_createButton);
            _createButton.style.marginRight = 0;
            footer.Add(_createButton);

            _createStatus = new Label();
            _createStatus.style.display = DisplayStyle.None;
            footer.Add(_createStatus);

            _openIssueButton = new Button { text = string.Empty };
            JiraStyles.ApplyLinkButton(_openIssueButton);
            _openIssueButton.style.display = DisplayStyle.None;
            footer.Add(_openIssueButton);

            return footer;
        }

        private void RefreshCreateAvailability()
        {
            bool connected = HasCredentials();
            _createNotice.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            _createForm.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected && !_projectsLoaded)
                ReloadProjectsAsync();
        }

        // --- Attachment -----------------------------------------------------

        private void SelectAttachment()
        {
            string path = EditorUtility.OpenFilePanel(L.Tr(L.K.BtnSelectFile), string.Empty, string.Empty);
            if (string.IsNullOrEmpty(path))
                return;

            _attachmentPath = path;
            _attachmentLabel.text = System.IO.Path.GetFileName(path);
        }

        private void ClearAttachment()
        {
            _attachmentPath = string.Empty;
            _attachmentLabel.text = L.Tr(L.K.NoFileSelected);
        }

        // --- Settings panel -------------------------------------------------

        private VisualElement BuildSettingsPanel()
        {
            var panel = new VisualElement();

            var languageCard = new VisualElement();
            JiraStyles.ApplyCard(languageCard);

            var languageTitle = new Label(L.Tr(L.K.SettingsLanguage));
            JiraStyles.ApplySectionTitle(languageTitle);
            languageCard.Add(languageTitle);

            var languageDropdown = new DropdownField(L.Tr(L.K.SettingsLanguage))
            {
                choices = new List<string> { L.Tr(L.K.LangPortuguese), L.Tr(L.K.LangEnglish) }
            };
            languageDropdown.index = L.Current == L.En ? 1 : 0;
            JiraStyles.ApplyDropdown(languageDropdown);
            languageDropdown.RegisterValueChangedCallback(_ =>
            {
                L.Current = languageDropdown.index == 1 ? L.En : L.Pt;
                _activeTab = Tab.Settings;
                CreateGUI();
            });
            languageCard.Add(languageDropdown);
            panel.Add(languageCard);

            panel.Add(BuildAiSettingsCard());

            var dataCard = new VisualElement();
            JiraStyles.ApplyCard(dataCard);

            var dataTitle = new Label(L.Tr(L.K.SettingsDataTitle));
            JiraStyles.ApplySectionTitle(dataTitle);
            dataCard.Add(dataTitle);

            var dataNote = new Label(L.Tr(L.K.SettingsDataNote));
            JiraStyles.ApplyMuted(dataNote);
            dataNote.style.marginBottom = 12;
            dataCard.Add(dataNote);

            var clearPresets = new Button(ClearPresets) { text = L.Tr(L.K.SettingsClearPresets) };
            JiraStyles.ApplySecondaryButton(clearPresets);
            clearPresets.style.marginBottom = 8;
            dataCard.Add(clearPresets);

            var clearButton = new Button(ClearConnectionData) { text = L.Tr(L.K.SettingsClearData) };
            JiraStyles.ApplySecondaryButton(clearButton);
            dataCard.Add(clearButton);

            panel.Add(dataCard);
            return panel;
        }

        private VisualElement BuildAiSettingsCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AiSettingsTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var note = new Label(L.Tr(L.K.AiSettingsNote));
            JiraStyles.ApplyMuted(note);
            note.style.marginBottom = 12;
            card.Add(note);

            string provider = JiraPreferences.AiProvider;
            bool isOpenAi = provider == JiraPreferences.ProviderOpenAi;

            var providerDropdown = new DropdownField(L.Tr(L.K.AiProviderLabel))
            {
                choices = new List<string> { L.Tr(L.K.ProviderClaude), L.Tr(L.K.ProviderOpenAi) }
            };
            providerDropdown.index = isOpenAi ? 1 : 0;
            JiraStyles.ApplyDropdown(providerDropdown);
            providerDropdown.RegisterValueChangedCallback(_ =>
            {
                JiraPreferences.AiProvider = providerDropdown.index == 1
                    ? JiraPreferences.ProviderOpenAi
                    : JiraPreferences.ProviderAnthropic;
                _activeTab = Tab.Settings;
                CreateGUI();
            });
            card.Add(providerDropdown);

            var tokenField = new TextField(L.Tr(L.K.AiTokenLabel))
            {
                value = JiraPreferences.GetAiToken(provider),
                isPasswordField = true
            };
            JiraStyles.ApplyField(tokenField);
            tokenField.RegisterValueChangedCallback(evt => JiraPreferences.SetAiToken(provider, evt.newValue));
            card.Add(tokenField);

            string[] modelLabels = isOpenAi ? OpenAiModelLabels : ClaudeModelLabels;
            string[] modelIds = isOpenAi ? OpenAiModelIds : ClaudeModelIds;

            var modelDropdown = new DropdownField(L.Tr(L.K.AiModelLabel))
            {
                choices = new List<string>(modelLabels)
            };
            int modelIndex = Array.IndexOf(modelIds, JiraPreferences.GetAiModel(provider));
            modelDropdown.index = modelIndex >= 0 ? modelIndex : 0;
            JiraStyles.ApplyDropdown(modelDropdown);
            modelDropdown.RegisterValueChangedCallback(_ =>
            {
                int index = modelDropdown.index;
                if (index >= 0 && index < modelIds.Length)
                    JiraPreferences.SetAiModel(provider, modelIds[index]);
            });
            card.Add(modelDropdown);

            string keyUrl = isOpenAi
                ? "https://platform.openai.com/api-keys"
                : "https://console.anthropic.com/settings/keys";
            var getKeyButton = new Button(() => Application.OpenURL(keyUrl)) { text = L.Tr(L.K.BtnGetAiKey) };
            JiraStyles.ApplySecondaryButton(getKeyButton);
            card.Add(getKeyButton);

            return card;
        }

        private void ClearPresets()
        {
            JiraPreferences.ClearPresets();
            ShowStatus(L.Tr(L.K.MsgPresetsCleared), true);
        }

        private void ClearConnectionData()
        {
            JiraPreferences.ClearConnectionInfo();
            _projectsLoaded = false;

            if (_urlField != null) _urlField.value = string.Empty;
            if (_emailField != null) _emailField.value = string.Empty;
            if (_tokenField != null) _tokenField.value = string.Empty;
            if (_connectedCard != null) _connectedCard.style.display = DisplayStyle.None;

            ShowStatus(L.Tr(L.K.MsgDataCleared), true);
        }

        // --- Data loading ---------------------------------------------------

        private async void ReloadProjectsAsync()
        {
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            SetCreateStatus(L.Tr(L.K.MsgLoadingProjects), true);
            try
            {
                List<JiraProject> projects = await client.GetProjectsAsync();
                _projects.Clear();
                _projects.AddRange(projects);
                _projectsLoaded = true;

                if (_projects.Count == 0)
                {
                    _projectDropdown.choices = new List<string>();
                    _projectDropdown.SetValueWithoutNotify(L.Tr(L.K.MsgNoProjectsOption));
                    SetCreateStatus(L.Tr(L.K.MsgNoProjects), false);
                    return;
                }

                var labels = new List<string>(_projects.Count);
                int presetIndex = 0;
                string presetProject = JiraPreferences.PresetProject;
                for (int i = 0; i < _projects.Count; i++)
                {
                    labels.Add($"{_projects[i].name} ({_projects[i].key})");
                    if (!string.IsNullOrEmpty(presetProject) && _projects[i].key == presetProject)
                        presetIndex = i;
                }

                _projectDropdown.choices = labels;
                _projectDropdown.SetValueWithoutNotify(labels[presetIndex]);
                HideCreateStatus();
                OnProjectSelected();
            }
            catch (Exception exception)
            {
                SetCreateStatus(exception.Message, false);
            }
        }

        private async void OnProjectSelected()
        {
            JiraProject project = SelectedProject();
            if (project == null)
                return;

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _openIssueButton.style.display = DisplayStyle.None;

            try
            {
                List<JiraIssueType> types = await client.GetIssueTypesAsync(project.key);
                _issueTypes.Clear();
                _issueTypes.AddRange(types);

                var typeLabels = new List<string>(_issueTypes.Count);
                int presetIndex = 0;
                string presetType = JiraPreferences.PresetIssueTypeName;
                for (int i = 0; i < _issueTypes.Count; i++)
                {
                    JiraIssueType type = _issueTypes[i];
                    typeLabels.Add(type.subtask ? $"{type.name} (subtask)" : type.name);
                    if (!string.IsNullOrEmpty(presetType) && type.name == presetType)
                        presetIndex = i;
                }

                _typeDropdown.choices = typeLabels;
                _typeDropdown.SetValueWithoutNotify(typeLabels.Count > 0 ? typeLabels[presetIndex] : string.Empty);

                if (_issueTypes.Count == 0)
                    SetCreateStatus(L.Tr(L.K.MsgNoIssueTypes), false);
                else
                    HideCreateStatus();
            }
            catch (Exception exception)
            {
                SetCreateStatus(exception.Message, false);
            }

            try
            {
                _assignableUsers.Clear();
                _assignableUsers.AddRange(await client.GetAssignableUsersAsync(project.key));
            }
            catch { }

            if (_myself == null)
            {
                try { _myself = await client.GetMyselfAsync(); } catch { }
            }

            await LoadBoardDataAsync(client, project.key);

            OnTypeSelected();
        }

        private async Task LoadBoardDataAsync(JiraClient client, string projectKey)
        {
            _activeBoardId = -1;
            _epics.Clear();
            _sprints.Clear();

            try
            {
                List<JiraBoard> boards = await client.GetBoardsAsync(projectKey);
                if (boards.Count > 0)
                    _activeBoardId = boards[0].id;
            }
            catch { }

            if (_activeBoardId > 0)
            {
                try { _epics.AddRange(await client.GetEpicsAsync(_activeBoardId)); } catch { }
                try { _sprints.AddRange(await client.GetActiveSprintsAsync(_activeBoardId)); } catch { }
            }

            PopulateEpicChoices();
            PopulateSprintChoices();
        }

        private void PopulateEpicChoices()
        {
            var labels = new List<string> { L.Tr(L.K.NoneOption) };
            foreach (JiraEpic epic in _epics)
                labels.Add($"{epic.DisplayName} ({epic.key})");

            _epicDropdown.choices = labels;
            _epicDropdown.SetValueWithoutNotify(labels[0]);

            if (_epicProgressContainer != null)
                _epicProgressContainer.style.display = DisplayStyle.None;
        }

        private void PopulateSprintChoices()
        {
            var labels = new List<string> { L.Tr(L.K.NoneOption) };
            foreach (JiraSprint sprint in _sprints)
                labels.Add(sprint.name);

            _sprintDropdown.choices = labels;
            _sprintDropdown.SetValueWithoutNotify(labels[0]);
            _sprintDropdown.SetEnabled(_sprints.Count > 0);
        }

        private VisualElement BuildEpicProgress()
        {
            _epicProgressContainer = new VisualElement();
            _epicProgressContainer.style.marginBottom = 10;

            _epicProgressLabel = new Label();
            JiraStyles.ApplyFieldHint(_epicProgressLabel);
            _epicProgressLabel.style.marginTop = 0;
            _epicProgressLabel.style.marginBottom = 4;
            _epicProgressContainer.Add(_epicProgressLabel);

            var track = new VisualElement();
            track.style.height = 8;
            track.style.backgroundColor = new StyleColor(new Color32(47, 52, 61, 255));
            track.style.borderTopLeftRadius = 4;
            track.style.borderTopRightRadius = 4;
            track.style.borderBottomLeftRadius = 4;
            track.style.borderBottomRightRadius = 4;
            track.style.overflow = Overflow.Hidden;

            _epicProgressFill = new VisualElement();
            _epicProgressFill.style.height = 8;
            _epicProgressFill.style.width = Length.Percent(0);
            _epicProgressFill.style.backgroundColor = new StyleColor(new Color32(54, 179, 126, 255));
            track.Add(_epicProgressFill);

            _epicProgressContainer.Add(track);
            _epicProgressContainer.style.display = DisplayStyle.None;
            return _epicProgressContainer;
        }

        private async void OnEpicSelected()
        {
            JiraEpic epic = SelectedEpic();
            if (epic == null)
            {
                _epicProgressContainer.style.display = DisplayStyle.None;
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _epicProgressContainer.style.display = DisplayStyle.Flex;
            _epicProgressLabel.text = L.Tr(L.K.EpicProgressLoading);
            _epicProgressFill.style.width = Length.Percent(0);

            try
            {
                JiraEpicProgress progress = await client.GetEpicProgressAsync(epic.key);

                // The selected epic may have changed while the request was in flight.
                if (SelectedEpic() != epic)
                    return;

                if (progress.Total == 0)
                {
                    _epicProgressLabel.text = L.Tr(L.K.EpicProgressEmpty);
                    _epicProgressFill.style.width = Length.Percent(0);
                    return;
                }

                _epicProgressLabel.text = L.Tr(L.K.EpicProgressFormat, progress.Done, progress.Total, progress.Percent);
                _epicProgressFill.style.width = Length.Percent(progress.Percent);
            }
            catch
            {
                _epicProgressLabel.text = L.Tr(L.K.EpicProgressFailed);
                _epicProgressFill.style.width = Length.Percent(0);
            }
        }

        private async void OnTypeSelected()
        {
            JiraIssueType type = SelectedIssueType();
            bool isSubtask = type != null && type.subtask;

            _parentContainer.style.display = isSubtask ? DisplayStyle.Flex : DisplayStyle.None;
            _epicContainer.style.display = isSubtask ? DisplayStyle.None : DisplayStyle.Flex;

            JiraProject project = SelectedProject();
            if (project == null || type == null)
            {
                ClearDynamicFields();
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            List<JiraFieldMeta> fields;
            try { fields = await client.GetCreateFieldsAsync(project.key, type.id); }
            catch { fields = new List<JiraFieldMeta>(); }

            RebuildDynamicFields(fields);
        }

        // --- Dynamic fields -------------------------------------------------

        private void ClearDynamicFields()
        {
            _priorityDropdown = null; _priorityMeta = null;
            _teamDropdown = null; _teamText = null; _teamMeta = null;
            _assigneeDropdown = null; _assigneeMeta = null;
            _startDateField = null; _startDateMeta = null;
            _dueDateField = null; _dueDateMeta = null;

            _classifyContent.Clear();
            _datesContent.Clear();
            _classifyCard.style.display = DisplayStyle.None;
            _datesCard.style.display = DisplayStyle.None;
        }

        private void RebuildDynamicFields(List<JiraFieldMeta> fields)
        {
            ClearDynamicFields();

            // Priority
            _priorityMeta = FindById(fields, FieldPriority);
            VisualElement priorityWidget = null;
            if (_priorityMeta != null && _priorityMeta.HasAllowedValues)
            {
                _priorityDropdown = BuildAllowedDropdown(L.Tr(L.K.FieldPriority), _priorityMeta, JiraPreferences.PresetPriorityId, preferMedium: true);
                priorityWidget = _priorityDropdown;
            }

            // Team (discovered by name)
            _teamMeta = FindTeam(fields);
            VisualElement teamWidget = null;
            if (_teamMeta != null)
            {
                if (_teamMeta.HasAllowedValues)
                {
                    _teamDropdown = BuildAllowedDropdown(L.Tr(L.K.FieldTeam), _teamMeta, JiraPreferences.PresetTeamValue, preferMedium: false);
                    teamWidget = _teamDropdown;
                }
                else
                {
                    _teamText = new TextField(L.Tr(L.K.FieldTeam)) { value = JiraPreferences.PresetTeamValue };
                    JiraStyles.ApplyField(_teamText);
                    teamWidget = _teamText;
                }
            }

            if (priorityWidget != null && teamWidget != null)
                _classifyContent.Add(JiraStyles.Row(priorityWidget, teamWidget));
            else if (priorityWidget != null)
                _classifyContent.Add(priorityWidget);
            else if (teamWidget != null)
                _classifyContent.Add(teamWidget);

            // Assignee
            _assigneeMeta = FindById(fields, FieldAssignee);
            if (_assigneeMeta != null)
                _classifyContent.Add(BuildAssigneeWidget(JiraPreferences.PresetAssigneeAccountId));

            _classifyCard.style.display = _classifyContent.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Dates
            _startDateMeta = FindStartDate(fields);
            _dueDateMeta = FindById(fields, FieldDueDate);

            VisualElement startWidget = _startDateMeta != null ? BuildDateWidget(L.Tr(L.K.FieldStartDate), out _startDateField) : null;
            VisualElement dueWidget = _dueDateMeta != null ? BuildDateWidget(L.Tr(L.K.FieldDueDate), out _dueDateField) : null;

            if (startWidget != null && dueWidget != null)
                _datesContent.Add(JiraStyles.Row(startWidget, dueWidget));
            else if (startWidget != null)
                _datesContent.Add(startWidget);
            else if (dueWidget != null)
                _datesContent.Add(dueWidget);

            _datesCard.style.display = _datesContent.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static DropdownField BuildAllowedDropdown(string label, JiraFieldMeta meta, string presetId, bool preferMedium)
        {
            var dropdown = new DropdownField(label);
            var labels = new List<string>(meta.allowedValues.Length);
            int selected = -1;

            for (int i = 0; i < meta.allowedValues.Length; i++)
            {
                JiraAllowedValue value = meta.allowedValues[i];
                labels.Add(value.Display);

                if (!string.IsNullOrEmpty(presetId) && value.id == presetId)
                    selected = i;

                if (selected < 0 && preferMedium && !string.IsNullOrEmpty(value.name))
                {
                    string lower = value.name.ToLowerInvariant();
                    if (lower.Contains("medium") || lower.Contains("média") || lower.Contains("media"))
                        selected = i;
                }
            }

            dropdown.choices = labels;
            if (labels.Count > 0)
                dropdown.SetValueWithoutNotify(labels[selected >= 0 ? selected : 0]);
            JiraStyles.ApplyDropdown(dropdown);
            return dropdown;
        }

        private VisualElement BuildAssigneeWidget(string presetAccountId)
        {
            var container = new VisualElement();

            EnsureMyselfInAssignable();

            _assigneeDropdown = new DropdownField(L.Tr(L.K.FieldAssignee));
            var labels = new List<string> { L.Tr(L.K.AssigneeNone) };
            int selected = 0;
            for (int i = 0; i < _assignableUsers.Count; i++)
            {
                JiraUser user = _assignableUsers[i];
                labels.Add(!string.IsNullOrWhiteSpace(user.displayName) ? user.displayName : user.accountId);
                if (!string.IsNullOrEmpty(presetAccountId) && user.accountId == presetAccountId)
                    selected = i + 1;
            }

            _assigneeDropdown.choices = labels;
            _assigneeDropdown.SetValueWithoutNotify(labels[selected]);
            JiraStyles.ApplyDropdown(_assigneeDropdown);
            container.Add(_assigneeDropdown);

            var selfButton = new Button(AssignToSelf) { text = L.Tr(L.K.BtnAssignSelf) };
            JiraStyles.ApplyGhostButton(selfButton);
            selfButton.style.marginBottom = 10;
            container.Add(selfButton);

            return container;
        }

        private void EnsureMyselfInAssignable()
        {
            if (_myself == null || string.IsNullOrEmpty(_myself.accountId))
                return;

            bool present = _assignableUsers.Exists(u => u.accountId == _myself.accountId);
            if (!present)
                _assignableUsers.Insert(0, _myself);
        }

        private void AssignToSelf()
        {
            if (_assigneeDropdown == null || _myself == null)
                return;

            int index = _assignableUsers.FindIndex(u => u.accountId == _myself.accountId);
            if (index >= 0)
                _assigneeDropdown.index = index + 1; // +1 for the "None" entry
        }

        private static VisualElement BuildDateWidget(string label, out TextField field)
        {
            var container = new VisualElement();
            field = new TextField(label);
            JiraStyles.ApplyField(field);
            var hint = new Label(L.Tr(L.K.DateHint));
            JiraStyles.ApplyFieldHint(hint);
            container.Add(field);
            container.Add(hint);
            return container;
        }

        private static JiraFieldMeta FindById(List<JiraFieldMeta> fields, string fieldId)
        {
            foreach (JiraFieldMeta field in fields)
                if (field != null && field.fieldId == fieldId)
                    return field;
            return null;
        }

        private static JiraFieldMeta FindTeam(List<JiraFieldMeta> fields)
        {
            foreach (JiraFieldMeta field in fields)
            {
                if (field?.name == null || field.fieldId == "assignee" || field.fieldId == "reporter")
                    continue;

                string n = field.name.Trim().ToLowerInvariant();
                if (n == "time" || n == "team" || n == "equipe" || n == "squad" ||
                    n == "times" || n.Contains("equipe") || n.Contains("squad"))
                    return field;
            }
            return null;
        }

        private static JiraFieldMeta FindStartDate(List<JiraFieldMeta> fields)
        {
            foreach (JiraFieldMeta field in fields)
            {
                if (field?.name == null || field.fieldId == "duedate")
                    continue;

                string type = field.schema?.type;
                if (type != "date" && type != "datetime")
                    continue;

                string n = field.name.ToLowerInvariant();
                if (n.Contains("start") || n.Contains("iníc") || n.Contains("inic"))
                    return field;
            }
            return null;
        }

        // --- Create ---------------------------------------------------------

        private async void CreateIssueAsync()
        {
            if (_isCreating)
                return;

            JiraProject project = SelectedProject();
            JiraIssueType type = SelectedIssueType();
            string summary = _summaryField.value?.Trim();

            if (project == null) { SetCreateStatus(L.Tr(L.K.MsgSelectProject), false); return; }
            if (type == null) { SetCreateStatus(L.Tr(L.K.MsgSelectType), false); return; }
            if (string.IsNullOrWhiteSpace(summary)) { SetCreateStatus(L.Tr(L.K.MsgSummaryRequired), false); return; }

            var draft = new JiraIssueDraft
            {
                ProjectKey = project.key,
                IssueTypeId = type.id,
                Summary = summary,
                Description = _descriptionField.value
            };

            if (type.subtask)
            {
                string parentKey = _parentField.value?.Trim();
                if (string.IsNullOrWhiteSpace(parentKey))
                {
                    SetCreateStatus(L.Tr(L.K.MsgSubtaskParentRequired), false);
                    return;
                }
                draft.ParentKey = parentKey;
            }
            else
            {
                JiraEpic epic = SelectedEpic();
                if (epic != null)
                    draft.ParentKey = epic.key;
            }

            ApplyDynamicFields(draft);

            JiraClient client = BuildClientOrNull();
            if (client == null) { SetCreateStatus(L.Tr(L.K.MsgNoCredentials), false); return; }

            SetCreatingBusy(true);
            _openIssueButton.style.display = DisplayStyle.None;
            SetCreateStatus(L.Tr(L.K.MsgCreating), true);

            try
            {
                JiraCreateIssueResult result = await client.CreateIssueAsync(draft);
                if (!result.Success)
                {
                    SetCreateStatus(result.Message, false);
                    return;
                }

                SavePresets(project, type);
                string message = L.Tr(L.K.MsgIssueCreated, result.IssueKey);

                JiraSprint sprint = SelectedSprint();
                if (sprint != null && !type.subtask)
                {
                    string sprintError = await client.MoveIssueToSprintAsync(sprint.id, result.IssueKey);
                    message += sprintError == null
                        ? L.Tr(L.K.MsgSprintAdded, sprint.name)
                        : L.Tr(L.K.MsgSprintFailed, sprintError);
                }

                if (!string.IsNullOrEmpty(_attachmentPath))
                {
                    string attachError = await client.UploadAttachmentAsync(result.IssueKey, _attachmentPath);
                    message += attachError == null
                        ? L.Tr(L.K.MsgAttachmentAdded)
                        : L.Tr(L.K.MsgAttachmentFailed, attachError);
                }

                SetCreateStatus(message, true);
                ShowOpenIssue(client.BaseUrl, result.IssueKey);

                _summaryField.value = string.Empty;
                _descriptionField.value = string.Empty;
                ClearAttachment();
            }
            catch (Exception exception)
            {
                SetCreateStatus(exception.Message, false);
            }
            finally
            {
                SetCreatingBusy(false);
            }
        }

        private void ApplyDynamicFields(JiraIssueDraft draft)
        {
            if (_priorityMeta != null && _priorityDropdown != null)
            {
                JiraAllowedValue value = AllowedAt(_priorityMeta, _priorityDropdown.index);
                if (value != null && !string.IsNullOrEmpty(value.id))
                    draft.SetFieldId(_priorityMeta.fieldId, value.id);
            }

            if (_assigneeMeta != null && _assigneeDropdown != null && _assigneeDropdown.index > 0)
            {
                int userIndex = _assigneeDropdown.index - 1;
                if (userIndex < _assignableUsers.Count)
                    draft.SetFieldId(_assigneeMeta.fieldId, _assignableUsers[userIndex].accountId);
            }

            if (_teamMeta != null)
            {
                if (_teamDropdown != null)
                {
                    JiraAllowedValue value = AllowedAt(_teamMeta, _teamDropdown.index);
                    if (value != null)
                    {
                        if (!string.IsNullOrEmpty(value.id))
                            draft.SetFieldId(_teamMeta.fieldId, value.id);
                        else
                            draft.SetFieldValueObject(_teamMeta.fieldId, value.Display);
                    }
                }
                else if (_teamText != null)
                {
                    draft.SetFieldString(_teamMeta.fieldId, _teamText.value?.Trim());
                }
            }

            if (_startDateMeta != null && _startDateField != null)
                draft.SetFieldString(_startDateMeta.fieldId, _startDateField.value?.Trim());

            if (_dueDateMeta != null && _dueDateField != null)
                draft.SetFieldString(_dueDateMeta.fieldId, _dueDateField.value?.Trim());
        }

        private static JiraAllowedValue AllowedAt(JiraFieldMeta meta, int index)
        {
            return meta?.allowedValues != null && index >= 0 && index < meta.allowedValues.Length
                ? meta.allowedValues[index]
                : null;
        }

        private void SavePresets(JiraProject project, JiraIssueType type)
        {
            JiraPreferences.PresetProject = project.key;
            JiraPreferences.PresetIssueTypeName = type.name;

            if (_priorityMeta != null && _priorityDropdown != null)
            {
                JiraAllowedValue value = AllowedAt(_priorityMeta, _priorityDropdown.index);
                if (value != null) JiraPreferences.PresetPriorityId = value.id;
            }

            if (_assigneeDropdown != null && _assigneeDropdown.index > 0)
            {
                int userIndex = _assigneeDropdown.index - 1;
                if (userIndex < _assignableUsers.Count)
                    JiraPreferences.PresetAssigneeAccountId = _assignableUsers[userIndex].accountId;
            }

            if (_teamMeta != null)
            {
                if (_teamDropdown != null)
                {
                    JiraAllowedValue value = AllowedAt(_teamMeta, _teamDropdown.index);
                    if (value != null)
                        JiraPreferences.PresetTeamValue = !string.IsNullOrEmpty(value.id) ? value.id : value.Display;
                }
                else if (_teamText != null)
                {
                    JiraPreferences.PresetTeamValue = _teamText.value?.Trim();
                }
            }
        }

        private void ShowOpenIssue(string baseUrl, string issueKey)
        {
            string url = $"{baseUrl}/browse/{issueKey}";
            _openIssueButton.text = L.Tr(L.K.BtnOpenIssue, issueKey);
            _openIssueButton.clickable = new Clickable(() => Application.OpenURL(url));
            _openIssueButton.style.display = DisplayStyle.Flex;
        }

        // --- Selection helpers ---------------------------------------------

        private JiraProject SelectedProject()
        {
            int index = _projectDropdown.index;
            return index >= 0 && index < _projects.Count ? _projects[index] : null;
        }

        private JiraIssueType SelectedIssueType()
        {
            int index = _typeDropdown.index;
            return index >= 0 && index < _issueTypes.Count ? _issueTypes[index] : null;
        }

        private JiraEpic SelectedEpic()
        {
            int index = _epicDropdown.index - 1;
            return index >= 0 && index < _epics.Count ? _epics[index] : null;
        }

        private JiraSprint SelectedSprint()
        {
            int index = _sprintDropdown.index - 1;
            return index >= 0 && index < _sprints.Count ? _sprints[index] : null;
        }

        // --- Connection actions --------------------------------------------

        private async void TestConnectionAsync()
        {
            if (_isConnecting)
                return;

            string baseUrl = _urlField.value?.Trim();
            string email = _emailField.value?.Trim();
            string token = _tokenField.value;

            if (string.IsNullOrWhiteSpace(baseUrl) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token))
            {
                ShowStatus(L.Tr(L.K.MsgFillFields), false);
                return;
            }

            JiraPreferences.BaseUrl = baseUrl;
            JiraPreferences.Email = email;
            JiraPreferences.SessionToken = token;

            SetBusy(true);
            ShowStatus(L.Tr(L.K.MsgValidating), true);

            try
            {
                var auth = new JiraBasicTokenAuthProvider(email, token);
                var client = new JiraClient(baseUrl, auth);
                JiraConnectionResult result = await client.TestConnectionAsync();

                if (!result.Success)
                {
                    ShowStatus(result.Message, false);
                    _connectedCard.style.display = DisplayStyle.None;
                    return;
                }

                ShowStatus(result.Message, true);
                ShowConnectedUser(result.User);
                _myself = result.User;
                _projectsLoaded = false;
                SelectTab(Tab.Create);
            }
            catch (Exception exception)
            {
                ShowStatus(exception.Message, false);
                _connectedCard.style.display = DisplayStyle.None;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowConnectedUser(JiraUser user)
        {
            string displayName = !string.IsNullOrWhiteSpace(user?.displayName) ? user.displayName : "Atlassian";
            string email = !string.IsNullOrWhiteSpace(user?.emailAddress) ? user.emailAddress : JiraPreferences.Email;

            _connectedUserLabel.text = displayName;
            _connectedEmailLabel.text = email;
            _connectedCard.style.display = DisplayStyle.Flex;
        }

        private void Disconnect()
        {
            JiraPreferences.ClearSessionToken();
            _tokenField.value = string.Empty;
            _connectedCard.style.display = DisplayStyle.None;
            _projectsLoaded = false;
            ShowStatus(L.Tr(L.K.MsgTokenRemoved), true);
        }

        private void RefreshConnectionState()
        {
            bool hasSessionToken = !string.IsNullOrWhiteSpace(JiraPreferences.SessionToken);
            _connectedCard.style.display = DisplayStyle.None;

            if (hasSessionToken)
                ShowStatus(L.Tr(L.K.MsgTokenLoaded), true);
        }

        private static bool HasCredentials()
        {
            return !string.IsNullOrWhiteSpace(JiraPreferences.BaseUrl)
                && !string.IsNullOrWhiteSpace(JiraPreferences.Email)
                && !string.IsNullOrWhiteSpace(JiraPreferences.SessionToken);
        }

        private static JiraClient BuildClientOrNull()
        {
            if (!HasCredentials())
                return null;

            try
            {
                var auth = new JiraBasicTokenAuthProvider(JiraPreferences.Email, JiraPreferences.SessionToken);
                return new JiraClient(JiraPreferences.BaseUrl, auth);
            }
            catch
            {
                return null;
            }
        }

        // --- Small UI helpers ----------------------------------------------

        private void SetBusy(bool busy)
        {
            _isConnecting = busy;
            _connectButton.SetEnabled(!busy);
            _connectButton.text = busy ? L.Tr(L.K.BtnConnecting) : L.Tr(L.K.BtnConnect);
            _urlField.SetEnabled(!busy);
            _emailField.SetEnabled(!busy);
            _tokenField.SetEnabled(!busy);
        }

        private void SetCreatingBusy(bool busy)
        {
            _isCreating = busy;
            _createButton.SetEnabled(!busy);
            _createButton.text = busy ? L.Tr(L.K.BtnCreating) : L.Tr(L.K.BtnCreate);
        }

        private void ShowStatus(string message, bool success)
        {
            _statusLabel.text = message;
            _statusLabel.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyStatus(_statusLabel, success);
        }

        private void SetCreateStatus(string message, bool success)
        {
            _createStatus.text = message;
            _createStatus.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyStatus(_createStatus, success);
        }

        private void HideCreateStatus()
        {
            _createStatus.style.display = DisplayStyle.None;
        }
    }
}
