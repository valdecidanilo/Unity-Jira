using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        private sealed class AdditionalFieldBinding
        {
            public JiraFieldMeta Meta;
            public TextField TextField;
            public DropdownField Dropdown;
            public Toggle BooleanToggle;
            public readonly List<Toggle> OptionToggles = new List<Toggle>();
        }

        private sealed class QuickSubtaskBinding
        {
            public VisualElement Root;
            public Label Header;
            public TextField Title;
            public TextField Description;
        }

        private sealed class QuickSubtaskInput
        {
            public string Title;
            public string Description;
        }

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
        private bool _isConnected;

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
        private VisualElement _quickSubtaskContainer;
        private VisualElement _quickSubtasksList;
        private readonly List<QuickSubtaskBinding> _quickSubtasks =
            new List<QuickSubtaskBinding>();
        private Label _fieldsStatusLabel;
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
        private TextField _assigneeSearchField;
        private DropdownField _assigneeDropdown;
        private JiraFieldMeta _assigneeMeta;
        private TextField _startDateField;
        private JiraFieldMeta _startDateMeta;
        private TextField _dueDateField;
        private JiraFieldMeta _dueDateMeta;
        private JiraFieldMeta _descriptionMeta;
        private VisualElement _additionalFieldsCard;
        private VisualElement _additionalFieldsContent;
        private readonly List<AdditionalFieldBinding> _additionalFields =
            new List<AdditionalFieldBinding>();

        // Attachment
        private string _attachmentPath = string.Empty;
        private Label _attachmentLabel;

        // AI assistant
        private TextField _aiPromptField;
        private Button _aiGenerateButton;
        private VisualElement _aiInputContainer;
        private Button _aiSetupButton;
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
        private readonly List<JiraUser> _filteredAssignableUsers = new List<JiraUser>();
        private JiraUser _myself;
        private int _activeBoardId = -1;
        private bool _epicsLoadFailed;
        private bool _areFieldsLoading;
        private bool _fieldsLoaded;
        private int _fieldLoadVersion;
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
            JiraStyles.ApplyContentViewport(scroll);
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
            JiraStyles.ApplyBrandFooter(footer);

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
            _createTab.style.display = DisplayStyle.None;

            bar.Add(_connectionTab);
            bar.Add(_createTab);
            bar.Add(_settingsTab);
            rootVisualElement.Add(bar);
        }

        private void SelectTab(Tab tab)
        {
            if (tab == Tab.Create && !_isConnected)
                tab = Tab.Connection;

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
            _createForm.Add(BuildDetailsCard());
            _createForm.Add(BuildAiCard());
            _createForm.Add(BuildAdditionalFieldsCard());
            _createForm.Add(BuildClassifyCard());
            _createForm.Add(BuildAttachmentCard());
            _createForm.Add(BuildDatesCard());
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

            _fieldsStatusLabel = new Label();
            JiraStyles.ApplyMuted(_fieldsStatusLabel);
            _fieldsStatusLabel.style.marginTop = 8;
            _fieldsStatusLabel.style.display = DisplayStyle.None;
            card.Add(_fieldsStatusLabel);

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

        private VisualElement BuildAdditionalFieldsCard()
        {
            _additionalFieldsCard = new VisualElement();
            JiraStyles.ApplyCard(_additionalFieldsCard);

            var title = new Label(L.Tr(L.K.CreateAdditionalFieldsTitle));
            JiraStyles.ApplySectionTitle(title);
            _additionalFieldsCard.Add(title);

            var hint = new Label(L.Tr(L.K.AdditionalFieldsHint));
            JiraStyles.ApplyMuted(hint);
            hint.style.marginBottom = 12;
            _additionalFieldsCard.Add(hint);

            _additionalFieldsContent = new VisualElement();
            _additionalFieldsCard.Add(_additionalFieldsContent);

            _additionalFieldsCard.style.display = DisplayStyle.None;
            return _additionalFieldsCard;
        }

        private VisualElement BuildDetailsCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.CreateDetailsTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _summaryField = new TextField(L.Tr(L.K.FieldSummary) + " *");
            JiraStyles.ApplyField(_summaryField);
            card.Add(_summaryField);

            _descriptionField = new TextField(L.Tr(L.K.FieldDescription));
            JiraStyles.ApplyMultiline(_descriptionField);
            card.Add(_descriptionField);

            _quickSubtaskContainer = new VisualElement();

            var quickSubtaskHeader = new VisualElement();
            quickSubtaskHeader.style.flexDirection = FlexDirection.Row;
            quickSubtaskHeader.style.alignItems = Align.Center;

            var quickSubtaskTitle = new Label(L.Tr(L.K.FieldQuickSubtask));
            JiraStyles.ApplyDynamicFieldLabel(quickSubtaskTitle);
            quickSubtaskTitle.style.flexGrow = 1;
            quickSubtaskHeader.Add(quickSubtaskTitle);

            var addQuickSubtaskButton = new Button(AddQuickSubtask)
            {
                text = L.Tr(L.K.BtnAddQuickSubtask)
            };
            JiraStyles.ApplyCompactButton(addQuickSubtaskButton, false);
            quickSubtaskHeader.Add(addQuickSubtaskButton);
            _quickSubtaskContainer.Add(quickSubtaskHeader);

            var quickSubtaskHint = new Label(L.Tr(L.K.QuickSubtaskHint));
            JiraStyles.ApplyFieldHint(quickSubtaskHint);
            _quickSubtaskContainer.Add(quickSubtaskHint);

            _quickSubtasksList = new VisualElement();
            _quickSubtaskContainer.Add(_quickSubtasksList);
            AddQuickSubtask();

            _quickSubtaskContainer.style.display = DisplayStyle.None;
            card.Add(_quickSubtaskContainer);

            return card;
        }

        private void AddQuickSubtask()
        {
            if (_quickSubtasksList == null)
                return;

            var binding = new QuickSubtaskBinding
            {
                Root = new VisualElement(),
                Header = new Label(),
                Title = new TextField(L.Tr(L.K.FieldQuickSubtaskTitle)),
                Description = new TextField(L.Tr(L.K.FieldQuickSubtaskDescription))
            };
            JiraStyles.ApplyNestedCard(binding.Root);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            JiraStyles.ApplyDynamicFieldLabel(binding.Header);
            binding.Header.style.flexGrow = 1;
            headerRow.Add(binding.Header);

            var removeButton = new Button(() => RemoveQuickSubtask(binding))
            {
                text = "−",
                tooltip = L.Tr(L.K.BtnRemoveQuickSubtask)
            };
            JiraStyles.ApplyCompactButton(removeButton, true);
            headerRow.Add(removeButton);
            binding.Root.Add(headerRow);

            JiraStyles.ApplyField(binding.Title);
            binding.Root.Add(binding.Title);

            JiraStyles.ApplyMultiline(binding.Description);
            binding.Description.style.minHeight = 64;
            binding.Root.Add(binding.Description);

            _quickSubtasks.Add(binding);
            _quickSubtasksList.Add(binding.Root);
            RefreshQuickSubtaskHeaders();
        }

        private void RemoveQuickSubtask(QuickSubtaskBinding binding)
        {
            if (binding == null || !_quickSubtasks.Remove(binding))
                return;

            binding.Root?.RemoveFromHierarchy();
            RefreshQuickSubtaskHeaders();
        }

        private void RefreshQuickSubtaskHeaders()
        {
            for (int i = 0; i < _quickSubtasks.Count; i++)
                _quickSubtasks[i].Header.text =
                    L.Tr(L.K.QuickSubtaskNumber, i + 1);
        }

        private void ResetQuickSubtasks()
        {
            _quickSubtasks.Clear();
            _quickSubtasksList?.Clear();
            AddQuickSubtask();
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

            var screenshotButton = new Button(CaptureGameViewAttachment)
            {
                text = L.Tr(L.K.BtnCaptureGameView)
            };
            JiraStyles.ApplyGhostButton(screenshotButton);
            screenshotButton.style.marginRight = 8;
            row.Add(screenshotButton);

            var removeButton = new Button(ClearAttachment) { text = L.Tr(L.K.BtnRemoveFile) };
            JiraStyles.ApplyGhostButton(removeButton);
            row.Add(removeButton);

            card.Add(row);

            _attachmentLabel = new Label(L.Tr(L.K.NoFileSelected));
            JiraStyles.ApplyFieldHint(_attachmentLabel);
            _attachmentLabel.style.marginTop = 8;
            card.Add(_attachmentLabel);

            var screenshotHint = new Label(L.Tr(L.K.CaptureGameViewHint));
            JiraStyles.ApplyFieldHint(screenshotHint);
            card.Add(screenshotHint);

            return card;
        }

        private VisualElement BuildAiCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.AiSectionTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            _aiInputContainer = new VisualElement();
            _aiPromptField = new TextField(L.Tr(L.K.AiPromptLabel));
            JiraStyles.ApplyMultiline(_aiPromptField);
            _aiPromptField.style.minHeight = 56;
            _aiInputContainer.Add(_aiPromptField);

            _aiGenerateButton = new Button(GenerateWithAiAsync) { text = L.Tr(L.K.BtnAiGenerate) };
            JiraStyles.ApplySecondaryButton(_aiGenerateButton);
            _aiInputContainer.Add(_aiGenerateButton);
            card.Add(_aiInputContainer);

            _aiSetupButton = new Button(() => SelectTab(Tab.Settings))
            {
                text = L.Tr(L.K.BtnConfigureAi)
            };
            JiraStyles.ApplySecondaryButton(_aiSetupButton);
            card.Add(_aiSetupButton);

            RefreshAiAvailability();

            return card;
        }

        private void RefreshAiAvailability()
        {
            if (_aiInputContainer == null || _aiSetupButton == null)
                return;

            bool configured = HasAiConfiguration();
            _aiInputContainer.style.display = configured
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _aiSetupButton.style.display = configured
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private static bool HasAiConfiguration()
        {
            string provider = JiraPreferences.AiProvider;
            return !string.IsNullOrWhiteSpace(JiraPreferences.GetAiToken(provider)) &&
                   !string.IsNullOrWhiteSpace(JiraPreferences.GetAiModel(provider));
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
            bool connected = _isConnected;
            _createNotice.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            _createForm.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected && !_projectsLoaded)
                ReloadProjectsAsync();

            if (connected)
                RefreshAiAvailability();
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

        private void CaptureGameViewAttachment()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
#if UNITY_2022_2_OR_NEWER
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            }
            if (camera == null)
            {
                _attachmentLabel.text = L.Tr(L.K.MsgNoCameraForScreenshot);
                return;
            }

            int width = Mathf.Clamp(camera.pixelWidth, 640, 3840);
            int height = Mathf.Clamp(camera.pixelHeight, 360, 2160);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;

            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;

                screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();

                string fileName =
                    $"jira-gameview-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                string path = Path.Combine(Application.temporaryCachePath, fileName);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());

                _attachmentPath = path;
                _attachmentLabel.text = L.Tr(L.K.MsgScreenshotCaptured, fileName);
            }
            catch (Exception exception)
            {
                _attachmentLabel.text =
                    L.Tr(L.K.MsgScreenshotFailed, exception.Message);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);
                if (screenshot != null)
                    DestroyImmediate(screenshot);
            }
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
            tokenField.RegisterValueChangedCallback(evt =>
            {
                JiraPreferences.SetAiToken(provider, evt.newValue);
                RefreshAiAvailability();
            });
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
            SetConnectionAvailability(false);

            ShowStatus(L.Tr(L.K.MsgDataCleared), true);
            SelectTab(Tab.Connection);
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
                int presetIndex = FindDefaultIssueTypeIndex(_issueTypes);
                string presetType = JiraPreferences.PresetIssueTypeName;
                for (int i = 0; i < _issueTypes.Count; i++)
                {
                    JiraIssueType type = _issueTypes[i];
                    typeLabels.Add(type.subtask ? $"{type.name} (subtask)" : type.name);
                    if (presetIndex < 0 &&
                        !string.IsNullOrEmpty(presetType) &&
                        type.name == presetType)
                        presetIndex = i;
                }

                if (presetIndex < 0)
                    presetIndex = 0;

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

        private static int FindDefaultIssueTypeIndex(List<JiraIssueType> issueTypes)
        {
            for (int i = 0; i < issueTypes.Count; i++)
            {
                JiraIssueType issueType = issueTypes[i];
                if (IsStoryIssueType(issueType))
                    return i;
            }

            return -1;
        }

        private void UpdateQuickSubtaskVisibility(JiraIssueType issueType)
        {
            if (_quickSubtaskContainer == null)
                return;

            bool canCreate = IsStoryIssueType(issueType) &&
                             FindQuickSubtaskType() != null;
            _quickSubtaskContainer.style.display = canCreate
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static bool IsStoryIssueType(JiraIssueType issueType)
        {
            if (issueType == null || issueType.subtask)
                return false;

            string name = issueType.name?.Trim();
            return string.Equals(name, "História", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Historia", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Story", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "User Story", StringComparison.OrdinalIgnoreCase);
        }

        private JiraIssueType FindQuickSubtaskType()
        {
            JiraIssueType fallback = null;
            foreach (JiraIssueType issueType in _issueTypes)
            {
                if (issueType == null || !issueType.subtask)
                    continue;

                if (fallback == null)
                    fallback = issueType;

                string name = issueType.name?.Trim();
                if (string.Equals(name, "Subtarefa", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Sub-task", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Subtask", StringComparison.OrdinalIgnoreCase))
                    return issueType;
            }

            return fallback;
        }

        private async Task LoadBoardDataAsync(JiraClient client, string projectKey)
        {
            _activeBoardId = -1;
            _epicsLoadFailed = false;
            _epics.Clear();
            _sprints.Clear();

            try
            {
                _epics.AddRange(await client.GetProjectEpicsAsync(projectKey));
            }
            catch
            {
                _epicsLoadFailed = true;
            }

            try
            {
                List<JiraBoard> boards = await client.GetBoardsAsync(projectKey);
                if (boards.Count > 0)
                    _activeBoardId = boards[0].id;
            }
            catch { }

            if (_activeBoardId > 0)
            {
                try { _sprints.AddRange(await client.GetActiveSprintsAsync(_activeBoardId)); } catch { }
            }

            PopulateEpicChoices();
            PopulateSprintChoices();
        }

        private void PopulateEpicChoices()
        {
            var labels = new List<string>();

            if (_epicsLoadFailed)
            {
                labels.Add(L.Tr(L.K.MsgEpicsFailedOption));
            }
            else if (_epics.Count == 0)
            {
                labels.Add(L.Tr(L.K.MsgNoEpicsOption));
            }
            else
            {
                labels.Add(L.Tr(L.K.NoneOption));
                foreach (JiraEpic epic in _epics)
                    labels.Add($"{epic.DisplayName} ({epic.key})");
            }

            _epicDropdown.choices = labels;
            _epicDropdown.SetValueWithoutNotify(labels[0]);
            _epicDropdown.SetEnabled(!_epicsLoadFailed && _epics.Count > 0);

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
            UpdateQuickSubtaskVisibility(type);
            _parentField.label = isSubtask
                ? L.Tr(L.K.FieldParent) + " *"
                : L.Tr(L.K.FieldParent);

            JiraProject project = SelectedProject();
            if (project == null || type == null)
            {
                _fieldLoadVersion++;
                _fieldsLoaded = false;
                SetFieldsLoading(false);
                ClearDynamicFields();
                _fieldsStatusLabel.style.display = DisplayStyle.None;
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
            {
                _fieldsLoaded = false;
                return;
            }

            int loadVersion = ++_fieldLoadVersion;
            string projectKey = project.key;
            string issueTypeId = type.id;

            _fieldsLoaded = false;
            ClearDynamicFields();
            SetFieldsLoading(true);
            ShowFieldsStatus(L.Tr(L.K.MsgLoadingFields), true);

            try
            {
                List<JiraFieldMeta> fields =
                    await client.GetCreateFieldsAsync(projectKey, issueTypeId);

                if (loadVersion != _fieldLoadVersion ||
                    SelectedProject()?.key != projectKey ||
                    SelectedIssueType()?.id != issueTypeId)
                    return;

                if (fields.Count == 0)
                {
                    ShowFieldsStatus(L.Tr(L.K.MsgNoFieldsReturned), false);
                    return;
                }

                JiraFieldMeta priorityMeta = FindById(fields, FieldPriority);
                if (priorityMeta != null && !priorityMeta.HasAllowedValues)
                {
                    List<JiraAllowedValue> priorities =
                        await client.GetPrioritiesAsync();

                    if (loadVersion != _fieldLoadVersion)
                        return;

                    if (priorities.Count > 0)
                        priorityMeta.allowedValues = priorities.ToArray();
                }

                RebuildDynamicFields(fields);
                _fieldsLoaded = true;
                ShowFieldsStatus(L.Tr(L.K.MsgFieldsLoaded, fields.Count), true);
            }
            catch (Exception exception)
            {
                if (loadVersion == _fieldLoadVersion)
                    ShowFieldsStatus(
                        L.Tr(L.K.MsgFieldsLoadFailed, exception.Message),
                        false);
            }
            finally
            {
                if (loadVersion == _fieldLoadVersion)
                    SetFieldsLoading(false);
            }
        }

        private void SetFieldsLoading(bool loading)
        {
            _areFieldsLoading = loading;
            if (_createButton != null)
                _createButton.SetEnabled(!_isCreating && !_areFieldsLoading);
        }

        private void ShowFieldsStatus(string message, bool success)
        {
            _fieldsStatusLabel.text = message;
            _fieldsStatusLabel.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyInlineStatus(_fieldsStatusLabel, success);
        }

        // --- Dynamic fields -------------------------------------------------

        private void ClearDynamicFields()
        {
            _priorityDropdown = null; _priorityMeta = null;
            _assigneeSearchField = null; _assigneeDropdown = null; _assigneeMeta = null;
            _filteredAssignableUsers.Clear();
            _startDateField = null; _startDateMeta = null;
            _dueDateField = null; _dueDateMeta = null;
            _descriptionMeta = null;
            _descriptionField.label = L.Tr(L.K.FieldDescription);
            _additionalFields.Clear();

            _classifyContent.Clear();
            _datesContent.Clear();
            _additionalFieldsContent.Clear();
            _classifyCard.style.display = DisplayStyle.None;
            _datesCard.style.display = DisplayStyle.None;
            _additionalFieldsCard.style.display = DisplayStyle.None;
        }

        private void RebuildDynamicFields(List<JiraFieldMeta> fields)
        {
            ClearDynamicFields();
            var specializedFields = new HashSet<string>();

            _descriptionMeta = FindById(fields, "description");
            _descriptionField.label = FieldLabel(
                _descriptionMeta,
                L.Tr(L.K.FieldDescription));

            // Priority
            _priorityMeta = FindById(fields, FieldPriority);
            VisualElement priorityWidget = null;
            if (_priorityMeta != null && _priorityMeta.HasAllowedValues)
            {
                _priorityDropdown = BuildAllowedDropdown(
                    FieldLabel(_priorityMeta, L.Tr(L.K.FieldPriority)),
                    _priorityMeta,
                    JiraPreferences.PresetPriorityId,
                    preferMedium: true);
                priorityWidget = _priorityDropdown;
                specializedFields.Add(_priorityMeta.fieldId);
            }

            if (priorityWidget != null)
                _classifyContent.Add(priorityWidget);

            // Assignee
            _assigneeMeta = FindById(fields, FieldAssignee);
            if (_assigneeMeta != null)
            {
                _classifyContent.Add(BuildAssigneeWidget(
                    FieldLabel(_assigneeMeta, L.Tr(L.K.FieldAssignee)),
                    JiraPreferences.PresetAssigneeAccountId));
                specializedFields.Add(_assigneeMeta.fieldId);
            }

            _classifyCard.style.display = _classifyContent.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Dates
            _startDateMeta = FindStartDate(fields);
            _dueDateMeta = FindById(fields, FieldDueDate);

            VisualElement startWidget = _startDateMeta != null
                ? BuildDateWidget(
                    FieldLabel(_startDateMeta, L.Tr(L.K.FieldStartDate)),
                    out _startDateField)
                : null;
            VisualElement dueWidget = _dueDateMeta != null
                ? BuildDateWidget(
                    FieldLabel(_dueDateMeta, L.Tr(L.K.FieldDueDate)),
                    out _dueDateField)
                : null;

            if (_startDateMeta != null)
                specializedFields.Add(_startDateMeta.fieldId);
            if (_dueDateMeta != null)
                specializedFields.Add(_dueDateMeta.fieldId);

            if (startWidget != null && dueWidget != null)
                _datesContent.Add(JiraStyles.Row(startWidget, dueWidget));
            else if (startWidget != null)
                _datesContent.Add(startWidget);
            else if (dueWidget != null)
                _datesContent.Add(dueWidget);

            _datesCard.style.display = _datesContent.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            AddAdditionalFields(fields, specializedFields, required: true);
            AddAdditionalFields(fields, specializedFields, required: false);

            _additionalFieldsCard.style.display = _additionalFieldsContent.childCount > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void AddAdditionalFields(
            List<JiraFieldMeta> fields,
            HashSet<string> specializedFields,
            bool required)
        {
            foreach (JiraFieldMeta field in fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.fieldId) ||
                    field.required != required ||
                    IsCoreField(field.fieldId) ||
                    specializedFields.Contains(field.fieldId))
                    continue;

                _additionalFields.Add(BuildAdditionalField(field));
            }
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

        private VisualElement BuildAssigneeWidget(string label, string presetAccountId)
        {
            var container = new VisualElement();

            EnsureMyselfInAssignable();

            _assigneeSearchField = new TextField(L.Tr(L.K.FieldAssigneeSearch));
            JiraStyles.ApplyField(_assigneeSearchField);
            container.Add(_assigneeSearchField);

            _assigneeDropdown = new DropdownField(label);
            JiraStyles.ApplyDropdown(_assigneeDropdown);
            container.Add(_assigneeDropdown);

            RefreshAssigneeChoices(string.Empty, presetAccountId);
            _assigneeSearchField.RegisterValueChangedCallback(evt =>
            {
                string selectedAccountId = SelectedAssigneeAccountId();
                RefreshAssigneeChoices(evt.newValue, selectedAccountId);
            });

            var selfButton = new Button(AssignToSelf) { text = L.Tr(L.K.BtnAssignSelf) };
            JiraStyles.ApplyGhostButton(selfButton);
            selfButton.style.marginBottom = 10;
            container.Add(selfButton);

            return container;
        }

        private void RefreshAssigneeChoices(string query, string preferredAccountId)
        {
            if (_assigneeDropdown == null)
                return;

            _filteredAssignableUsers.Clear();
            string normalizedQuery = query?.Trim();

            foreach (JiraUser user in _assignableUsers)
            {
                if (user == null || !MatchesAssignee(user, normalizedQuery))
                    continue;

                _filteredAssignableUsers.Add(user);
                if (_filteredAssignableUsers.Count >= 100)
                    break;
            }

            if (!string.IsNullOrWhiteSpace(preferredAccountId) &&
                !_filteredAssignableUsers.Exists(
                    user => user.accountId == preferredAccountId))
            {
                JiraUser preferred = _assignableUsers.Find(
                    user => user != null && user.accountId == preferredAccountId);
                if (preferred != null)
                {
                    if (_filteredAssignableUsers.Count >= 100)
                        _filteredAssignableUsers.RemoveAt(
                            _filteredAssignableUsers.Count - 1);
                    _filteredAssignableUsers.Add(preferred);
                }
            }

            var labels = new List<string> { L.Tr(L.K.AssigneeNone) };
            int selected = 0;
            for (int i = 0; i < _filteredAssignableUsers.Count; i++)
            {
                JiraUser user = _filteredAssignableUsers[i];
                labels.Add(AssigneeDisplay(user));
                if (!string.IsNullOrWhiteSpace(preferredAccountId) &&
                    user.accountId == preferredAccountId)
                    selected = i + 1;
            }

            _assigneeDropdown.choices = labels;
            _assigneeDropdown.SetValueWithoutNotify(labels[selected]);
        }

        private static bool MatchesAssignee(JiraUser user, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return ContainsIgnoreCase(user.displayName, query) ||
                   ContainsIgnoreCase(user.emailAddress, query) ||
                   ContainsIgnoreCase(user.accountId, query);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string AssigneeDisplay(JiraUser user)
        {
            string name = !string.IsNullOrWhiteSpace(user?.displayName)
                ? user.displayName
                : user?.accountId;
            return !string.IsNullOrWhiteSpace(user?.emailAddress)
                ? $"{name} — {user.emailAddress}"
                : name;
        }

        private string SelectedAssigneeAccountId()
        {
            if (_assigneeDropdown == null || _assigneeDropdown.index <= 0)
                return null;

            int userIndex = _assigneeDropdown.index - 1;
            return userIndex >= 0 && userIndex < _filteredAssignableUsers.Count
                ? _filteredAssignableUsers[userIndex].accountId
                : null;
        }

        private AdditionalFieldBinding BuildAdditionalField(JiraFieldMeta meta)
        {
            var binding = new AdditionalFieldBinding { Meta = meta };
            string label = FieldLabel(meta, meta.fieldId);
            string type = meta.schema?.type ?? "string";

            if (meta.HasAllowedValues && type == "array")
            {
                var labelElement = new Label(label);
                JiraStyles.ApplyDynamicFieldLabel(labelElement);
                _additionalFieldsContent.Add(labelElement);

                var options = new VisualElement();
                JiraStyles.ApplyDynamicOptions(options);
                foreach (JiraAllowedValue allowedValue in meta.allowedValues)
                {
                    var toggle = new Toggle(allowedValue.Display)
                    {
                        userData = allowedValue
                    };
                    binding.OptionToggles.Add(toggle);
                    options.Add(toggle);
                }
                _additionalFieldsContent.Add(options);
            }
            else if (meta.HasAllowedValues)
            {
                binding.Dropdown = BuildAdditionalDropdown(label, meta);
                _additionalFieldsContent.Add(binding.Dropdown);
            }
            else if (type == "boolean")
            {
                binding.BooleanToggle = new Toggle(label);
                binding.BooleanToggle.style.marginBottom = 10;
                _additionalFieldsContent.Add(binding.BooleanToggle);
            }
            else
            {
                binding.TextField = new TextField(label);
                if (IsMultilineField(meta))
                    JiraStyles.ApplyMultiline(binding.TextField);
                else
                    JiraStyles.ApplyField(binding.TextField);
                _additionalFieldsContent.Add(binding.TextField);

                if (type == "date" || type == "datetime")
                {
                    var dateHint = new Label(L.Tr(L.K.DateHint));
                    JiraStyles.ApplyFieldHint(dateHint);
                    _additionalFieldsContent.Add(dateHint);
                }
                else if (type == "array")
                {
                    var arrayHint = new Label(L.Tr(L.K.ArrayFieldHint));
                    JiraStyles.ApplyFieldHint(arrayHint);
                    _additionalFieldsContent.Add(arrayHint);
                }
                else if (IsAtlassianTeamField(meta))
                {
                    var teamHint = new Label(L.Tr(L.K.TeamIdHint));
                    JiraStyles.ApplyFieldHint(teamHint);
                    _additionalFieldsContent.Add(teamHint);
                }
            }

            if (!string.IsNullOrWhiteSpace(meta.description))
            {
                var description = new Label(meta.description);
                JiraStyles.ApplyFieldHint(description);
                _additionalFieldsContent.Add(description);
            }

            return binding;
        }

        private static DropdownField BuildAdditionalDropdown(
            string label,
            JiraFieldMeta meta)
        {
            var dropdown = new DropdownField(label);
            var labels = new List<string>();

            if (!meta.required)
                labels.Add(L.Tr(L.K.NoneOption));

            foreach (JiraAllowedValue allowedValue in meta.allowedValues)
                labels.Add(allowedValue.Display);

            dropdown.choices = labels;
            if (labels.Count > 0)
                dropdown.SetValueWithoutNotify(labels[0]);
            JiraStyles.ApplyDropdown(dropdown);
            return dropdown;
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

            _assigneeSearchField?.SetValueWithoutNotify(_myself.displayName ?? string.Empty);
            RefreshAssigneeChoices(_myself.displayName, _myself.accountId);
            int index = _filteredAssignableUsers.FindIndex(
                u => u.accountId == _myself.accountId);
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

        private static bool IsCoreField(string fieldId)
        {
            return fieldId == "project" ||
                   fieldId == "issuetype" ||
                   fieldId == "summary" ||
                   fieldId == "description" ||
                   fieldId == "parent" ||
                   fieldId == "attachment";
        }

        private static string FieldLabel(JiraFieldMeta meta, string fallback)
        {
            string label = !string.IsNullOrWhiteSpace(meta?.name) ? meta.name : fallback;
            return meta != null && meta.required ? label + " *" : label;
        }

        private bool TryCollectQuickSubtasks(
            JiraIssueType issueType,
            out List<QuickSubtaskInput> inputs,
            out string error)
        {
            inputs = new List<QuickSubtaskInput>();
            error = null;

            if (!IsStoryIssueType(issueType))
                return true;

            foreach (QuickSubtaskBinding binding in _quickSubtasks)
            {
                string title = binding.Title?.value?.Trim();
                string description = binding.Description?.value?.Trim();
                if (string.IsNullOrWhiteSpace(title) &&
                    string.IsNullOrWhiteSpace(description))
                    continue;

                if (string.IsNullOrWhiteSpace(title))
                {
                    error = L.Tr(L.K.MsgQuickSubtaskTitleRequired);
                    return false;
                }

                inputs.Add(new QuickSubtaskInput
                {
                    Title = title,
                    Description = description
                });
            }

            return true;
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
            if (!_fieldsLoaded) { SetCreateStatus(L.Tr(L.K.MsgFieldsNotLoaded), false); return; }
            if (string.IsNullOrWhiteSpace(summary)) { SetCreateStatus(L.Tr(L.K.MsgSummaryRequired), false); return; }
            if (!ValidateDynamicFields(out string fieldError))
            {
                SetCreateStatus(fieldError, false);
                return;
            }
            if (!TryCollectQuickSubtasks(
                    type,
                    out List<QuickSubtaskInput> quickSubtasks,
                    out string quickSubtaskError))
            {
                SetCreateStatus(quickSubtaskError, false);
                return;
            }

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

                JiraIssueType quickSubtaskType = FindQuickSubtaskType();
                if (IsStoryIssueType(type) && quickSubtaskType != null)
                {
                    foreach (QuickSubtaskInput quickSubtask in quickSubtasks)
                    {
                        var subtaskDraft = new JiraIssueDraft
                        {
                            ProjectKey = project.key,
                            IssueTypeId = quickSubtaskType.id,
                            ParentKey = result.IssueKey,
                            Summary = quickSubtask.Title,
                            Description = quickSubtask.Description
                        };

                        JiraCreateIssueResult subtaskResult =
                            await client.CreateIssueAsync(subtaskDraft);
                        message += subtaskResult.Success
                            ? L.Tr(
                                L.K.MsgQuickSubtaskCreated,
                                subtaskResult.IssueKey)
                            : L.Tr(
                                L.K.MsgQuickSubtaskFailed,
                                quickSubtask.Title,
                                subtaskResult.Message);
                    }
                }

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
                ResetQuickSubtasks();
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
                ApplyAllowedValue(draft, _priorityMeta.fieldId, value);
            }

            if (_assigneeMeta != null && _assigneeDropdown != null && _assigneeDropdown.index > 0)
            {
                int userIndex = _assigneeDropdown.index - 1;
                if (userIndex < _filteredAssignableUsers.Count)
                    draft.SetFieldObject(
                        _assigneeMeta.fieldId,
                        "accountId",
                        _filteredAssignableUsers[userIndex].accountId);
            }

            if (_startDateMeta != null && _startDateField != null)
                draft.SetFieldString(_startDateMeta.fieldId, _startDateField.value?.Trim());

            if (_dueDateMeta != null && _dueDateField != null)
                draft.SetFieldString(_dueDateMeta.fieldId, _dueDateField.value?.Trim());

            foreach (AdditionalFieldBinding binding in _additionalFields)
                ApplyAdditionalField(draft, binding);
        }

        private bool ValidateDynamicFields(out string error)
        {
            if (_descriptionMeta != null && _descriptionMeta.required &&
                string.IsNullOrWhiteSpace(_descriptionField.value))
            {
                error = L.Tr(L.K.MsgRequiredField, _descriptionMeta.name);
                return false;
            }

            if (_assigneeMeta != null && _assigneeMeta.required &&
                (_assigneeDropdown == null || _assigneeDropdown.index <= 0))
            {
                error = L.Tr(L.K.MsgRequiredField, _assigneeMeta.name);
                return false;
            }

            if (!ValidateTextField(_startDateMeta, _startDateField, out error) ||
                !ValidateTextField(_dueDateMeta, _dueDateField, out error))
                return false;

            foreach (AdditionalFieldBinding binding in _additionalFields)
            {
                if (binding.Meta.required && !HasAdditionalValue(binding))
                {
                    error = L.Tr(L.K.MsgRequiredField, binding.Meta.name);
                    return false;
                }

                if (binding.TextField != null &&
                    binding.Meta.schema?.type == "number" &&
                    !string.IsNullOrWhiteSpace(binding.TextField.value) &&
                    !TryNormalizeNumber(binding.TextField.value, out _))
                {
                    error = L.Tr(L.K.MsgInvalidNumber, binding.Meta.name);
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateTextField(
            JiraFieldMeta meta,
            TextField field,
            out string error)
        {
            if (meta != null && meta.required &&
                (field == null || string.IsNullOrWhiteSpace(field.value)))
            {
                error = L.Tr(L.K.MsgRequiredField, meta.name);
                return false;
            }

            error = null;
            return true;
        }

        private static bool HasAdditionalValue(AdditionalFieldBinding binding)
        {
            if (binding.BooleanToggle != null)
                return true;
            if (binding.TextField != null)
            {
                if (binding.Meta.schema?.type != "array")
                    return !string.IsNullOrWhiteSpace(binding.TextField.value);

                foreach (string value in SplitValues(binding.TextField.value ?? string.Empty))
                    if (!string.IsNullOrWhiteSpace(value))
                        return true;
                return false;
            }
            if (binding.Dropdown != null)
                return binding.Meta.required
                    ? binding.Dropdown.index >= 0
                    : binding.Dropdown.index > 0;

            foreach (Toggle toggle in binding.OptionToggles)
                if (toggle.value)
                    return true;

            return false;
        }

        private static void ApplyAdditionalField(
            JiraIssueDraft draft,
            AdditionalFieldBinding binding)
        {
            JiraFieldMeta meta = binding.Meta;
            string type = meta.schema?.type ?? "string";

            if (binding.BooleanToggle != null)
            {
                draft.SetFieldBoolean(meta.fieldId, binding.BooleanToggle.value);
                return;
            }

            if (binding.Dropdown != null)
            {
                int allowedIndex = binding.Meta.required
                    ? binding.Dropdown.index
                    : binding.Dropdown.index - 1;
                JiraAllowedValue allowedValue = AllowedAt(meta, allowedIndex);
                if (IsAtlassianTeamField(meta))
                {
                    string teamId = AllowedValueIdentifier(allowedValue);
                    if (!string.IsNullOrWhiteSpace(teamId))
                        draft.SetFieldString(meta.fieldId, teamId);
                }
                else
                {
                    ApplyAllowedValue(draft, meta.fieldId, allowedValue);
                }
                return;
            }

            if (binding.OptionToggles.Count > 0)
            {
                var selected = new List<KeyValuePair<string, string>>();
                foreach (Toggle toggle in binding.OptionToggles)
                {
                    if (!toggle.value)
                        continue;

                    JiraAllowedValue value = toggle.userData as JiraAllowedValue;
                    if (TryGetAllowedValue(value, out string property, out string rawValue))
                        selected.Add(new KeyValuePair<string, string>(property, rawValue));
                }

                if (selected.Count > 0)
                    draft.SetFieldObjectArray(meta.fieldId, selected);
                return;
            }

            string text = binding.TextField?.value?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (type == "number")
            {
                if (TryNormalizeNumber(text, out string number))
                    draft.SetFieldNumber(meta.fieldId, number);
            }
            else if (type == "array")
            {
                draft.SetFieldStringArray(meta.fieldId, SplitValues(text));
            }
            else if (type == "user")
            {
                draft.SetFieldObject(meta.fieldId, "accountId", text);
            }
            else if (type == "option")
            {
                draft.SetFieldValueObject(meta.fieldId, text);
            }
            else if (IsMultilineField(meta))
            {
                draft.SetFieldAdf(meta.fieldId, text);
            }
            else
            {
                draft.SetFieldString(meta.fieldId, text);
            }
        }

        private static void ApplyAllowedValue(
            JiraIssueDraft draft,
            string fieldId,
            JiraAllowedValue value)
        {
            if (TryGetAllowedValue(value, out string property, out string rawValue))
                draft.SetFieldObject(fieldId, property, rawValue);
        }

        private static bool TryGetAllowedValue(
            JiraAllowedValue value,
            out string property,
            out string rawValue)
        {
            if (!string.IsNullOrWhiteSpace(value?.id))
            {
                property = "id";
                rawValue = value.id;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(value?.accountId))
            {
                property = "accountId";
                rawValue = value.accountId;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(value?.value))
            {
                property = "value";
                rawValue = value.value;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(value?.key))
            {
                property = "key";
                rawValue = value.key;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(value?.name))
            {
                property = "name";
                rawValue = value.name;
                return true;
            }

            property = null;
            rawValue = null;
            return false;
        }

        private static bool TryNormalizeNumber(string text, out string normalized)
        {
            if (decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal number) ||
                decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out number))
            {
                normalized = number.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            normalized = null;
            return false;
        }

        private static IEnumerable<string> SplitValues(string text)
        {
            return text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool IsMultilineField(JiraFieldMeta meta)
        {
            return meta != null &&
                   (meta.fieldId == "environment" ||
                    (!string.IsNullOrWhiteSpace(meta.schema?.custom) &&
                     meta.schema.custom.IndexOf(
                         "textarea",
                         StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool IsAtlassianTeamField(JiraFieldMeta meta)
        {
            string customType = meta?.schema?.custom;
            return !string.IsNullOrWhiteSpace(customType) &&
                   customType.IndexOf(
                       "rm-teams-custom-field-team",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string AllowedValueIdentifier(JiraAllowedValue value)
        {
            if (!string.IsNullOrWhiteSpace(value?.id))
                return value.id;
            if (!string.IsNullOrWhiteSpace(value?.accountId))
                return value.accountId;
            if (!string.IsNullOrWhiteSpace(value?.value))
                return value.value;
            if (!string.IsNullOrWhiteSpace(value?.key))
                return value.key;
            return value?.name;
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
                if (userIndex < _filteredAssignableUsers.Count)
                    JiraPreferences.PresetAssigneeAccountId =
                        _filteredAssignableUsers[userIndex].accountId;
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
                    SetConnectionAvailability(false);
                    return;
                }

                ShowStatus(result.Message, true);
                ShowConnectedUser(result.User);
                _myself = result.User;
                _projectsLoaded = false;
                SetConnectionAvailability(true);
                SelectTab(Tab.Create);
            }
            catch (Exception exception)
            {
                ShowStatus(exception.Message, false);
                _connectedCard.style.display = DisplayStyle.None;
                SetConnectionAvailability(false);
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
            SetConnectionAvailability(false);
            ShowStatus(L.Tr(L.K.MsgTokenRemoved), true);
            SelectTab(Tab.Connection);
        }

        private async void RefreshConnectionState()
        {
            SetConnectionAvailability(false);
            _connectedCard.style.display = DisplayStyle.None;

            if (!HasCredentials())
                return;

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            SetBusy(true);
            ShowStatus(L.Tr(L.K.MsgValidating), true);
            try
            {
                JiraConnectionResult result = await client.TestConnectionAsync();
                if (!result.Success)
                {
                    ShowStatus(result.Message, false);
                    return;
                }

                _myself = result.User;
                ShowConnectedUser(result.User);
                ShowStatus(result.Message, true);
                SetConnectionAvailability(true);
            }
            catch (Exception exception)
            {
                ShowStatus(exception.Message, false);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetConnectionAvailability(bool connected)
        {
            _isConnected = connected;
            if (_createTab != null)
                _createTab.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;
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
            _createButton.SetEnabled(!busy && !_areFieldsLoading);
            _createButton.text = busy ? L.Tr(L.K.BtnCreating) : L.Tr(L.K.BtnCreate);
            _quickSubtaskContainer?.SetEnabled(!busy);
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
