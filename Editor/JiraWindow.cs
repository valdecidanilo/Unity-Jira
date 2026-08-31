using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Agents;
using OxenteGames.JiraCommunication.AI;
using OxenteGames.JiraCommunication.API;
using OxenteGames.JiraCommunication.Git;
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
        private const string PriorityDropdownIconName =
            "jira-priority-dropdown-icon";
        private const int ResolveIssuesPerPage = 15;

        private enum Tab { Connection, Create, Resolve, Agent, Settings }

        private enum ResolveSprintScope
        {
            All,
            Active,
            Backlog
        }

        private enum ResolveOwnerScope
        {
            Mine,
            Everyone
        }

        private sealed class AdditionalFieldBinding
        {
            public JiraFieldMeta Meta;
            public TextField TextField;
            public DropdownField Dropdown;
            public Toggle BooleanToggle;
            public int IssueSearchVersion;
            public readonly List<Toggle> OptionToggles = new List<Toggle>();
        }

        private sealed class QuickSubtaskBinding
        {
            public VisualElement Root;
            public Label Header;
            public TextField Title;
            public TextField Description;
            public DropdownField Priority;
            public DropdownField Team;
            public TextField TeamText;
            public DropdownField Assignee;
            public TextField StartDate;
            public TextField DueDate;
            public string AttachmentPath;
            public Label AttachmentLabel;
            public AttachmentPreviewBinding AttachmentPreview;
        }

        private sealed class AttachmentPreviewBinding
        {
            public VisualElement Root;
            public Image Image;
            public Label Info;
            public Texture2D Texture;
        }

        private sealed class QuickSubtaskInput
        {
            public string Title;
            public string Description;
            public string PriorityId;
            public string TeamId;
            public string AssigneeAccountId;
            public string StartDate;
            public string DueDate;
            public string AttachmentPath;
        }

        // Connection tab
        private TextField _urlField;
        private TextField _emailField;
        private TextField _tokenField;
        private Button _connectButton;
        private Label _statusLabel;
        private VisualElement _connectionFormCard;
        private VisualElement _connectedCard;
        private Label _connectedUserLabel;
        private Label _connectedEmailLabel;
        private bool _isConnecting;
        private bool _isConnected;

        // Tabs
        private Button _connectionTab;
        private Button _createTab;
        private Button _settingsTab;
        private Button _agentTab;
        private VisualElement _connectionPanel;
        private VisualElement _createPanel;
        private VisualElement _settingsPanel;
        private VisualElement _agentPanel;

        // Owns event subscriptions, so it must be disposed whenever the UI is rebuilt.
        private AgentConsoleView _agentConsole;
        private AgentSettingsView _agentSettings;
        private AiJiraView _aiJiraView;
        private Tab _activeTab = Tab.Connection;

        // Create tab - core
        private VisualElement _createNotice;
        private VisualElement _createForm;
        private VisualElement _destinationContent;
        private VisualElement _destinationLoader;
        private VisualElement _dynamicFieldsLoadingPanel;
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
        private JiraIssueType _quickSubtaskType;
        private JiraFieldMeta _quickSubtaskDescriptionMeta;
        private JiraFieldMeta _quickSubtaskPriorityMeta;
        private JiraFieldMeta _quickSubtaskTeamMeta;
        private JiraFieldMeta _quickSubtaskAssigneeMeta;
        private JiraFieldMeta _quickSubtaskStartDateMeta;
        private JiraFieldMeta _quickSubtaskDueDateMeta;
        private Label _fieldsStatusLabel;
        private Button _createButton;
        private Label _createStatus;
        private Button _openIssueButton;

        // Create tab - dynamic fields
        private VisualElement _classifyContent;
        private VisualElement _classifyLoader;
        private VisualElement _datesContent;
        private DropdownField _priorityDropdown;
        private JiraFieldMeta _priorityMeta;
        private TextField _assigneeSearchField;
        private DropdownField _assigneeDropdown;
        private VisualElement _assigneeResults;
        private Label _assigneeSelectedLabel;
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
        private readonly Dictionary<DropdownField, AdditionalFieldBinding>
            _associatedItemDropdowns =
                new Dictionary<DropdownField, AdditionalFieldBinding>();

        // Attachment
        private string _attachmentPath = string.Empty;
        private Label _attachmentLabel;
        private VisualElement _attachmentPreviewContainer;
        private Image _attachmentPreviewImage;
        private Label _attachmentPreviewInfo;
        private Texture2D _attachmentPreviewTexture;
#if UNITY_EDITOR_WIN
        private bool _waitingForWindowsSnip;
        private uint _windowsSnipClipboardSequence;
        private double _windowsSnipRequestedAt;
        private string _attachmentLabelBeforeWindowsSnip;
        private Action<string, string> _windowsSnipTargetSetter;
        private Label _windowsSnipTargetLabel;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(
            byte virtualKey,
            byte scanCode,
            uint flags,
            UIntPtr extraInfo);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();
#endif

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

        // Resolve tab
        private Button _resolveTab;
        private VisualElement _resolvePanel;
        private VisualElement _resolveNotice;
        private VisualElement _resolveContent;
        private readonly List<JiraWorkflowStatus> _resolveStatuses =
            new List<JiraWorkflowStatus>();
        private DropdownField _resolveStatusDropdown;
        private TextField _issueSearchField;
        private Label _resolveEpicSelectedLabel;
        private DropdownField _resolveEpicDropdown;
        private DropdownField _resolveOwnerScopeDropdown;
        private DropdownField _resolveSprintScopeDropdown;
        private VisualElement _resolveFiltersCard;
        private VisualElement _issueListCard;
        private VisualElement _issueListContainer;
        private Label _issueListStatus;
        private VisualElement _issuePagination;
        private Button _issuePreviousPageButton;
        private Button _issueNextPageButton;
        private Label _issuePageLabel;
        private VisualElement _resolveDetail;
        private VisualElement _resolveDetailBody;
        private Label _resolveDetailHeader;
        private TextField _resolveSummaryField;
        private TextField _resolveDescriptionField;
        private DropdownField _resolveEditPriorityDropdown;
        private VisualElement _resolveWeightContainer;
        private TextField _resolveWeightField;
        private Button _resolveSaveChangesButton;
        private Button _resolveCloseButton;
        private Button _resolveParentButton;
        private VisualElement _resolveSubtasksCard;
        private Label _resolveChildrenTitle;
        private VisualElement _resolveSubtasksList;
        private Label _resolveSubtasksStatus;
        private Label _resolveSubtasksCount;
        private Button _resolveAddSubtaskButton;
        private VisualElement _resolveAddSubtaskForm;
        private DropdownField _resolveNewChildTypeDropdown;
        private TextField _resolveNewSubtaskTitle;
        private TextField _resolveNewSubtaskDescription;
        private DropdownField _resolveNewSubtaskPriority;
        private VisualElement _resolveNewSubtaskTeamContainer;
        private DropdownField _resolveNewSubtaskTeam;
        private TextField _resolveNewSubtaskTeamText;
        private VisualElement _resolveNewSubtaskAssigneeContainer;
        private DropdownField _resolveNewSubtaskAssignee;
        private VisualElement _resolveNewSubtaskDatesContainer;
        private TextField _resolveNewSubtaskStartDate;
        private TextField _resolveNewSubtaskDueDate;
        private string _resolveNewSubtaskAttachmentPath;
        private Label _resolveNewChildAttachmentTitle;
        private Label _resolveNewSubtaskAttachmentLabel;
        private AttachmentPreviewBinding
            _resolveNewSubtaskAttachmentPreview;
        private JiraIssueType _resolveNewSubtaskType;
        private JiraFieldMeta _resolveNewSubtaskDescriptionMeta;
        private JiraFieldMeta _resolveNewSubtaskPriorityMeta;
        private JiraFieldMeta _resolveNewSubtaskTeamMeta;
        private JiraFieldMeta _resolveNewSubtaskAssigneeMeta;
        private JiraFieldMeta _resolveNewSubtaskStartDateMeta;
        private JiraFieldMeta _resolveNewSubtaskDueDateMeta;
        private bool _resolveSubtaskFieldsLoading;
        private int _resolveSubtaskFieldLoadVersion;
        private Button _resolveCreateSubtaskButton;
        private string _resolveOriginalSummary = string.Empty;
        private string _resolveOriginalDescription = string.Empty;
        private string _resolveOriginalPriorityId = string.Empty;
        private string _resolveOriginalWeight = string.Empty;
        private string _resolveWeightFieldId;
        private string _resolveParentTeamId = string.Empty;
        private string _resolveParentTeamFieldId;
        private TextField _resolveCommentField;
        private TextField _mentionSearchField;
        private VisualElement _mentionResults;
        private VisualElement _mentionChips;
        private Label _resolveAttachmentLabel;
        private Label _resolveStatus;

        // Git integration (Resolve detail)
        private VisualElement _gitCard;
        private DropdownField _gitTypeDropdown;
        private Label _gitBranchPreview;
        private Label _gitCommitPreview;
        private Label _gitCurrentBranchLabel;
        private Label _gitStatus;
        private bool _gitTypeUserPicked;
        private bool _gitBusy;
        private string _gitRepoRootCache;

        private JiraWorkflowStatus _selectedResolveStatus;
        private JiraUser _selectedResolveAssignee;
        private int _resolveIssuePage;
        private ResolveOwnerScope _resolveOwnerScope = ResolveOwnerScope.Mine;
        private ResolveSprintScope _resolveSprintScope = ResolveSprintScope.All;
        private readonly List<JiraListIssue> _resolveIssues = new List<JiraListIssue>();
        private readonly List<JiraUser> _mentionSelected = new List<JiraUser>();
        private readonly HashSet<string> _statusBusyIssues = new HashSet<string>();
        private readonly HashSet<string> _priorityBusyIssues = new HashSet<string>();
        private readonly List<JiraAllowedValue> _resolvePriorities = new List<JiraAllowedValue>();
        private readonly List<JiraIssueType> _resolveAvailableChildTypes =
            new List<JiraIssueType>();
        private readonly List<JiraListIssue> _resolveSelectedChildren =
            new List<JiraListIssue>();
        private readonly List<JiraEpic> _resolveEpics = new List<JiraEpic>();
        private readonly List<JiraEpic> _filteredResolveEpics = new List<JiraEpic>();
        private readonly List<JiraUser> _resolveAssignableUsers =
            new List<JiraUser>();
        private JiraEpic _selectedResolveEpic;
        private string _resolveProjectKey;
        private string _resolveAssignableProjectKey;
        private bool _resolveAssignableUsersLoaded;
        private JiraListIssue _selectedIssue;
        private JiraListIssue _resolveParentIssue;
        private readonly List<JiraListIssue> _resolveParentStack =
            new List<JiraListIssue>();
        private VisualElement _statusPopupOverlay;
        private string _resolveAttachmentPath = string.Empty;
        private bool _issuesLoaded;
        private bool _issuesLoading;
        private bool _resolveStatusesLoaded;
        private bool _resolveStatusesLoading;
        private bool _resolvePrioritiesLoaded;
        private bool _resolvePrioritiesLoading;
        private bool _resolveEpicsLoaded;
        private bool _resolveEpicsLoading;
        private bool _isResolving;
        private int _issueLoadVersion;
        private int _issueDetailLoadVersion;
        private int _resolveEpicLoadVersion;
        private int _resolveStatusLoadVersion;
        private int _resolveOwnerSearchVersion;
        private int _mentionSearchVersion;

        private static Sprite[] _prioritySprites;

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
        private bool _projectsLoading;
        private int _projectLoadVersion;
        private int _projectSelectionVersion;
        private int _connectionValidationVersion;
        private readonly List<VisualElement> _loaderSpinners = new List<VisualElement>();
        private IVisualElementScheduledItem _loaderAnimation;
        private int _loaderFrame;
        private bool _destinationIsLoading;
        private bool _modulesAreLoading;

        [MenuItem("Jira/Jira Workspace", priority = 0)]
        public static void Open() => ShowWindow(Tab.Connection);

        [MenuItem("Jira/Documentação oficial do Jira", priority = 100)]
        private static void OpenJiraDocumentation()
        {
            Application.OpenURL("https://developer.atlassian.com/cloud/jira/platform/rest/v3/intro/");
        }

        [MenuItem("Jira/Documentação do GitHub", priority = 101)]
        private static void OpenGitHubDocumentation()
        {
            Application.OpenURL("https://github.com/valdecidanilo/Unity-Jira");
        }

        private static void OpenApiTokenPage()
        {
            Application.OpenURL(
                "https://id.atlassian.com/manage-profile/security/api-tokens");
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
            Tab tabAfterValidation = _activeTab;

            ReleaseAttachmentPreviewTexture();
            CloseStatusPopup();

            // rootVisualElement is cleared below; the outgoing console must let go of
            // its AgentService subscriptions or it would keep updating dead elements.
            _agentConsole?.Dispose();
            _agentConsole = null;
            _agentSettings = null;
            _aiJiraView = null;
            _loaderAnimation?.Pause();
            _loaderSpinners.Clear();
            _destinationIsLoading = false;
            _modulesAreLoading = false;
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
            _resolvePanel = BuildResolvePanel();
            _agentPanel = BuildAgentPanel();
            _settingsPanel = BuildSettingsPanel();
            scroll.Add(_connectionPanel);
            scroll.Add(_createPanel);
            scroll.Add(_resolvePanel);
            scroll.Add(_agentPanel);
            scroll.Add(_settingsPanel);

            BuildBrandFooter();
            _loaderAnimation = rootVisualElement.schedule
                .Execute(AnimateLoaderSpinners)
                .Every(80);
            _loaderAnimation.Pause();

            SetConnectionAvailability(false);
            SelectTab(Tab.Connection);
            RefreshConnectionState(tabAfterValidation);
        }

#if UNITY_EDITOR_WIN
        private void OnFocus()
        {
            if (!_waitingForWindowsSnip ||
                EditorApplication.timeSinceStartup -
                _windowsSnipRequestedAt < 0.35d)
            {
                return;
            }

            _waitingForWindowsSnip = false;
            ImportWindowsSnipAsync(_windowsSnipClipboardSequence);
        }
#endif

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
            _resolveTab = new Button(() => SelectTab(Tab.Resolve)) { text = L.Tr(L.K.TabResolve) };
            _agentTab = new Button(() => SelectTab(Tab.Agent)) { text = L.Tr(L.K.TabAgent) };
            _settingsTab = new Button(() => SelectTab(Tab.Settings)) { text = L.Tr(L.K.TabSettings) };
            _createTab.style.display = DisplayStyle.None;
            _resolveTab.style.display = DisplayStyle.None;

            // The agent works on the repository, not on Jira, so it stays reachable
            // even without a validated connection.
            bar.Add(_connectionTab);
            bar.Add(_createTab);
            bar.Add(_resolveTab);
            bar.Add(_agentTab);
            bar.Add(_settingsTab);
            rootVisualElement.Add(bar);
        }

        private void SelectTab(Tab tab)
        {
            CloseStatusPopup();

            if ((tab == Tab.Create || tab == Tab.Resolve) && !_isConnected)
                tab = Tab.Connection;

            _activeTab = tab;
            if (_connectionPanel == null || _createPanel == null ||
                _resolvePanel == null || _settingsPanel == null || _agentPanel == null)
                return;

            _connectionPanel.style.display = tab == Tab.Connection ? DisplayStyle.Flex : DisplayStyle.None;
            _createPanel.style.display = tab == Tab.Create ? DisplayStyle.Flex : DisplayStyle.None;
            _resolvePanel.style.display = tab == Tab.Resolve ? DisplayStyle.Flex : DisplayStyle.None;
            _agentPanel.style.display = tab == Tab.Agent ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPanel.style.display = tab == Tab.Settings ? DisplayStyle.Flex : DisplayStyle.None;

            JiraStyles.ApplyTab(_connectionTab, tab == Tab.Connection);
            JiraStyles.ApplyTab(_createTab, tab == Tab.Create);
            JiraStyles.ApplyTab(_resolveTab, tab == Tab.Resolve);
            JiraStyles.ApplyTab(_agentTab, tab == Tab.Agent);
            JiraStyles.ApplyTab(_settingsTab, tab == Tab.Settings);

            if (tab == Tab.Create)
                RefreshCreateAvailability();
            else if (tab == Tab.Resolve)
                RefreshResolveAvailability();
            else if (tab == Tab.Agent)
                _agentConsole?.OnShow();
            else if (tab == Tab.Settings)
                _aiJiraView?.OnShow();
        }

        // --- Resolve panel -------------------------------------------------

        private VisualElement BuildResolvePanel()
        {
            var panel = new VisualElement();

            _resolveNotice = new VisualElement();
            JiraStyles.ApplyCard(_resolveNotice);
            var noticeTitle = new Label(L.Tr(L.K.CreateNoticeTitle));
            JiraStyles.ApplySectionTitle(noticeTitle);
            var noticeText = new Label(L.Tr(L.K.ResolveNoticeText));
            JiraStyles.ApplyMuted(noticeText);
            var noticeButton = new Button(() => SelectTab(Tab.Connection)) { text = L.Tr(L.K.BtnOpenConnTab) };
            JiraStyles.ApplySecondaryButton(noticeButton);
            noticeButton.style.marginTop = 12;
            _resolveNotice.Add(noticeTitle);
            _resolveNotice.Add(noticeText);
            _resolveNotice.Add(noticeButton);
            panel.Add(_resolveNotice);

            _resolveContent = new VisualElement();
            _resolveContent.Add(BuildResolveFilters());
            _resolveContent.Add(BuildResolveDetail());
            _resolveContent.Add(BuildIssueListCard());
            _resolveContent.RegisterCallback<PointerDownEvent>(
                OnStyledDropdownPointerDown,
                TrickleDown.TrickleDown);
            panel.Add(_resolveContent);

            return panel;
        }

        private VisualElement BuildResolveFilters()
        {
            _resolveFiltersCard = new VisualElement();
            JiraStyles.ApplyCard(_resolveFiltersCard);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label(L.Tr(L.K.ResolveFiltersTitle));
            JiraStyles.ApplySectionTitle(title);
            title.style.flexGrow = 1;
            title.style.marginBottom = 0;

            var reloadBtn = new Button(() => LoadResolveStatusesAsync(true))
            {
                text = L.Tr(L.K.BtnReload)
            };
            JiraStyles.ApplyGhostButton(reloadBtn);
            reloadBtn.style.flexShrink = 0;

            header.Add(title);
            header.Add(reloadBtn);
            _resolveFiltersCard.Add(header);

            _resolveStatusDropdown =
                new DropdownField(L.Tr(L.K.ResolveStatusFilter));
            JiraStyles.ApplyDropdown(_resolveStatusDropdown);
            AlignResolveFilterField(_resolveStatusDropdown);
            _resolveStatusDropdown.style.marginBottom = 12;
            _resolveStatusDropdown.RegisterValueChangedCallback(_ =>
                SelectResolveStatusFromDropdown());
            _resolveFiltersCard.Add(_resolveStatusDropdown);
            RefreshResolveStatusDropdown();

            _resolveSprintScopeDropdown =
                new DropdownField(L.Tr(L.K.ResolveSprintScope));
            _resolveSprintScopeDropdown.choices = new List<string>
            {
                L.Tr(L.K.ResolveSprintAll),
                L.Tr(L.K.ResolveSprintActive),
                L.Tr(L.K.ResolveSprintBacklog)
            };
            _resolveSprintScopeDropdown.SetValueWithoutNotify(
                _resolveSprintScopeDropdown.choices[0]);
            _resolveSprintScopeDropdown.RegisterValueChangedCallback(_ =>
            {
                _resolveSprintScope = (ResolveSprintScope)Mathf.Clamp(
                    _resolveSprintScopeDropdown.index,
                    0,
                    2);
                LoadIssuesAsync();
            });
            JiraStyles.ApplyDropdown(_resolveSprintScopeDropdown);
            AlignResolveFilterField(_resolveSprintScopeDropdown);
            _resolveFiltersCard.Add(_resolveSprintScopeDropdown);

            _resolveEpicDropdown = new DropdownField(L.Tr(L.K.ResolveEpicFilter));
            _resolveEpicDropdown.choices = new List<string>
            {
                L.Tr(L.K.ResolveAllEpics)
            };
            _resolveEpicDropdown.SetValueWithoutNotify(L.Tr(L.K.ResolveAllEpics));
            _resolveEpicDropdown.RegisterValueChangedCallback(_ =>
                SelectResolveEpicAndReload());
            JiraStyles.ApplyDropdown(_resolveEpicDropdown);
            AlignResolveFilterField(_resolveEpicDropdown);
            _resolveFiltersCard.Add(_resolveEpicDropdown);

            _resolveEpicSelectedLabel = new Label();
            JiraStyles.ApplyFieldHint(_resolveEpicSelectedLabel);
            _resolveEpicSelectedLabel.style.marginTop = -5;
            _resolveEpicSelectedLabel.style.marginBottom = 8;
            _resolveFiltersCard.Add(_resolveEpicSelectedLabel);
            UpdateResolveEpicSelectedLabel();

            _resolveOwnerScopeDropdown =
                new DropdownField(L.Tr(L.K.ResolveOwnerScope));
            _resolveOwnerScopeDropdown.choices = new List<string>
            {
                L.Tr(L.K.ResolveOwnerMine),
                L.Tr(L.K.ResolveOwnerEveryone)
            };
            _resolveOwnerScopeDropdown.SetValueWithoutNotify(
                _resolveOwnerScopeDropdown.choices[0]);
            _resolveOwnerScopeDropdown.RegisterValueChangedCallback(_ =>
                SelectResolveOwnerScopeFromDropdown());
            JiraStyles.ApplyDropdown(_resolveOwnerScopeDropdown);
            AlignResolveFilterField(_resolveOwnerScopeDropdown);
            _resolveFiltersCard.Add(_resolveOwnerScopeDropdown);

            return _resolveFiltersCard;
        }

        private void SelectResolveOwnerScopeFromDropdown()
        {
            if (_resolveOwnerScopeDropdown == null)
                return;

            int index = Mathf.Clamp(_resolveOwnerScopeDropdown.index, 0, 1);
            _selectedResolveAssignee = null;
            _resolveOwnerScope = (ResolveOwnerScope)index;
            RefreshResolveOwnerDropdown();
            LoadIssuesAsync();
        }

        private void RefreshResolveOwnerDropdown()
        {
            if (_resolveOwnerScopeDropdown == null)
                return;

            var choices = new List<string>
            {
                L.Tr(L.K.ResolveOwnerMine),
                L.Tr(L.K.ResolveOwnerEveryone)
            };
            int selectedIndex = (int)_resolveOwnerScope;
            if (_selectedResolveAssignee != null)
            {
                choices.Add(AssigneeDisplay(_selectedResolveAssignee));
                selectedIndex = 2;
            }

            _resolveOwnerScopeDropdown.choices = choices;
            _resolveOwnerScopeDropdown.SetValueWithoutNotify(
                choices[Mathf.Clamp(selectedIndex, 0, choices.Count - 1)]);
        }

        private static void AlignResolveFilterField(
            BaseField<string> field)
        {
            if (field == null)
                return;

            field.style.flexDirection = FlexDirection.Row;
            field.style.alignItems = Align.Center;

            Label label = field.labelElement;
            label.style.width = 180;
            label.style.minWidth = 180;
            label.style.maxWidth = 180;
            label.style.marginRight = 10;
            label.style.marginBottom = 0;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;

            VisualElement input = field.Q<VisualElement>(
                className: "unity-base-field__input");
            if (input == null)
                return;

            input.style.flexGrow = 1;
            input.style.flexShrink = 1;
            input.style.minWidth = 0;
        }

        private VisualElement BuildIssueListCard()
        {
            _issueListCard = new VisualElement();
            JiraStyles.ApplyCard(_issueListCard);

            var title = new Label(L.Tr(L.K.ResolveIssueListTitle));
            JiraStyles.ApplySectionTitle(title);
            _issueListCard.Add(title);

            _issueSearchField =
                new TextField(L.Tr(L.K.SearchIssuesLabel));
            JiraStyles.ApplyField(_issueSearchField);
            _issueSearchField.style.marginBottom = 3;
            _issueSearchField.RegisterValueChangedCallback(_ =>
            {
                _resolveIssuePage = 0;
                RenderIssueList();
            });
            _issueListCard.Add(_issueSearchField);

            var searchHint = new Label(L.Tr(L.K.SearchIssuesExample));
            JiraStyles.ApplyFieldHint(searchHint);
            searchHint.style.marginTop = 0;
            searchHint.style.marginBottom = 10;
            _issueListCard.Add(searchHint);

            _issueListStatus = new Label(L.Tr(L.K.MsgLoadingIssues));
            JiraStyles.ApplyMuted(_issueListStatus);
            _issueListCard.Add(_issueListStatus);

            _issueListContainer = new VisualElement();
            _issueListContainer.style.marginTop = 6;

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.style.minHeight = 120;
            listScroll.style.maxHeight = 360;
            listScroll.Add(_issueListContainer);
            _issueListCard.Add(listScroll);

            _issuePagination = new VisualElement();
            _issuePagination.style.flexDirection = FlexDirection.Row;
            _issuePagination.style.alignItems = Align.Center;
            _issuePagination.style.marginTop = 10;
            _issuePagination.style.display = DisplayStyle.None;

            _issuePreviousPageButton = new Button(() => ChangeIssuePage(-1))
            {
                text = "‹",
                tooltip = L.Tr(L.K.PreviousPageTooltip)
            };
            JiraStyles.ApplyGhostButton(_issuePreviousPageButton);
            _issuePreviousPageButton.style.width = 38;
            _issuePreviousPageButton.style.minWidth = 38;
            _issuePreviousPageButton.style.flexShrink = 0;

            _issuePageLabel = new Label();
            JiraStyles.ApplyMuted(_issuePageLabel);
            _issuePageLabel.style.flexGrow = 1;
            _issuePageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            _issueNextPageButton = new Button(() => ChangeIssuePage(1))
            {
                text = "›",
                tooltip = L.Tr(L.K.NextPageTooltip)
            };
            JiraStyles.ApplyGhostButton(_issueNextPageButton);
            _issueNextPageButton.style.width = 38;
            _issueNextPageButton.style.minWidth = 38;
            _issueNextPageButton.style.flexShrink = 0;

            _issuePagination.Add(_issuePreviousPageButton);
            _issuePagination.Add(_issuePageLabel);
            _issuePagination.Add(_issueNextPageButton);
            _issueListCard.Add(_issuePagination);

            return _issueListCard;
        }

        private VisualElement BuildResolveDetail()
        {
            _resolveDetail = new VisualElement();
            JiraStyles.ApplyCard(_resolveDetail);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 10;

            _resolveDetailHeader = new Label(L.Tr(L.K.SelectIssueHint));
            JiraStyles.ApplySectionTitle(_resolveDetailHeader);
            _resolveDetailHeader.style.flexGrow = 1;
            _resolveDetailHeader.style.flexShrink = 1;
            _resolveDetailHeader.style.minWidth = 0;
            _resolveDetailHeader.style.marginBottom = 0;
            _resolveDetailHeader.style.whiteSpace = WhiteSpace.NoWrap;
            _resolveDetailHeader.style.overflow = Overflow.Hidden;
            _resolveDetailHeader.style.textOverflow = TextOverflow.Ellipsis;

            _resolveCloseButton = new Button(CloseSelectedIssue) { text = "×" };
            _resolveCloseButton.tooltip = L.Tr(L.K.CloseIssueTooltip);
            JiraStyles.ApplyCloseButton(_resolveCloseButton);

            headerRow.Add(_resolveDetailHeader);
            headerRow.Add(_resolveCloseButton);
            _resolveDetail.Add(headerRow);

            _resolveDetailBody = new VisualElement();

            _resolveParentButton = new Button(ReturnToParentIssue);
            JiraStyles.ApplyGhostButton(_resolveParentButton);
            _resolveParentButton.style.display = DisplayStyle.None;
            _resolveParentButton.style.alignSelf = Align.FlexStart;
            _resolveParentButton.style.marginBottom = 6;
            _resolveParentButton.style.color =
                new StyleColor(new Color32(110, 177, 255, 255));
            _resolveDetailBody.Add(_resolveParentButton);

            var openButton = new Button(OpenSelectedIssue) { text = L.Tr(L.K.BtnOpenIssue, "issue") };
            JiraStyles.ApplyLinkButton(openButton);
            openButton.name = "resolve-open";
            openButton.style.marginTop = 0;
            _resolveDetailBody.Add(openButton);

            // Outside the Git card on purpose: that card is hidden when the Git
            // integration is off, and handing an issue to the agent does not need it.
            var sendToAgentButton = new Button(SendCurrentIssueToAgent)
            {
                text = L.Tr(L.K.BtnAgentSendToAgent)
            };
            JiraStyles.ApplySecondaryButton(sendToAgentButton);
            sendToAgentButton.style.alignSelf = Align.FlexStart;
            sendToAgentButton.style.marginTop = 4;
            sendToAgentButton.style.marginBottom = 4;
            _resolveDetailBody.Add(sendToAgentButton);

            _resolveDetailBody.Add(BuildResolveGitCard());

            var editCard = new VisualElement();
            JiraStyles.ApplyNestedCard(editCard);

            var editTitle = new Label(L.Tr(L.K.ResolveEditTitle));
            JiraStyles.ApplyDynamicFieldLabel(editTitle);
            editTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            editCard.Add(editTitle);

            _resolveSummaryField = new TextField(L.Tr(L.K.FieldSummary));
            JiraStyles.ApplyField(_resolveSummaryField);
            editCard.Add(_resolveSummaryField);

            _resolveDescriptionField = new TextField(L.Tr(L.K.FieldDescription));
            JiraStyles.ApplyMultiline(_resolveDescriptionField);
            _resolveDescriptionField.style.minHeight = 112;
            editCard.Add(_resolveDescriptionField);

            _resolveEditPriorityDropdown =
                new DropdownField(L.Tr(L.K.FieldPriority));
            _resolveEditPriorityDropdown.userData = FieldPriority;
            JiraStyles.ApplyDropdown(_resolveEditPriorityDropdown);
            ConfigurePriorityDropdownIcon(_resolveEditPriorityDropdown);
            editCard.Add(_resolveEditPriorityDropdown);

            _resolveWeightContainer = new VisualElement();
            _resolveWeightField = new TextField(L.Tr(L.K.FieldActivityWeight));
            _resolveWeightField.tooltip = L.Tr(L.K.FieldActivityWeightHint);
            JiraStyles.ApplyField(_resolveWeightField);
            _resolveWeightContainer.Add(_resolveWeightField);
            _resolveWeightContainer.style.display = DisplayStyle.None;
            editCard.Add(_resolveWeightContainer);

            _resolveSaveChangesButton = new Button(() => SaveIssueChangesAsync())
            {
                text = L.Tr(L.K.BtnSaveIssueChanges)
            };
            JiraStyles.ApplySecondaryButton(_resolveSaveChangesButton);
            editCard.Add(_resolveSaveChangesButton);
            _resolveDetailBody.Add(editCard);

            _resolveSubtasksCard = new VisualElement();
            JiraStyles.ApplyNestedCard(_resolveSubtasksCard);

            var subtasksHeader = new VisualElement();
            subtasksHeader.style.flexDirection = FlexDirection.Row;
            subtasksHeader.style.alignItems = Align.Center;
            subtasksHeader.style.marginBottom = 8;

            _resolveChildrenTitle =
                new Label(L.Tr(L.K.ResolveSubtasksTitle));
            JiraStyles.ApplyDynamicFieldLabel(_resolveChildrenTitle);
            _resolveChildrenTitle.style.flexGrow = 1;
            _resolveChildrenTitle.style.marginBottom = 0;
            _resolveChildrenTitle.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            _resolveSubtasksCount = new Label();
            _resolveSubtasksCount.style.fontSize = 10;
            _resolveSubtasksCount.style.color =
                new StyleColor(new Color32(173, 181, 194, 255));

            _resolveAddSubtaskButton = new Button(ToggleResolveAddSubtaskForm)
            {
                text = L.Tr(L.K.BtnAddQuickSubtask)
            };
            JiraStyles.ApplyCompactButton(_resolveAddSubtaskButton, false);
            _resolveAddSubtaskButton.style.marginLeft = 10;
            _resolveAddSubtaskButton.style.flexShrink = 0;

            subtasksHeader.Add(_resolveChildrenTitle);
            subtasksHeader.Add(_resolveSubtasksCount);
            subtasksHeader.Add(_resolveAddSubtaskButton);
            _resolveSubtasksCard.Add(subtasksHeader);

            _resolveSubtasksStatus = new Label(L.Tr(L.K.ResolveNoSubtasks));
            JiraStyles.ApplyFieldHint(_resolveSubtasksStatus);
            _resolveSubtasksStatus.style.marginTop = 0;
            _resolveSubtasksCard.Add(_resolveSubtasksStatus);

            _resolveSubtasksList = new VisualElement();
            _resolveSubtasksCard.Add(_resolveSubtasksList);

            _resolveAddSubtaskForm = new VisualElement();
            JiraStyles.ApplyNestedCard(_resolveAddSubtaskForm);
            _resolveAddSubtaskForm.style.marginTop = 10;
            _resolveAddSubtaskForm.style.display = DisplayStyle.None;

            _resolveNewChildTypeDropdown =
                new DropdownField(
                    RequiredLabel(
                        L.Tr(L.K.FieldChildActivityType)));
            JiraStyles.ApplyDropdown(_resolveNewChildTypeDropdown);
            _resolveNewChildTypeDropdown.style.display = DisplayStyle.None;
            _resolveNewChildTypeDropdown.RegisterValueChangedCallback(_ =>
                OnResolveChildTypeChanged());
            _resolveAddSubtaskForm.Add(
                _resolveNewChildTypeDropdown);

            _resolveNewSubtaskTitle =
                new TextField(
                    RequiredLabel(L.Tr(L.K.FieldQuickSubtaskTitle)));
            JiraStyles.ApplyField(_resolveNewSubtaskTitle);
            _resolveAddSubtaskForm.Add(_resolveNewSubtaskTitle);

            _resolveNewSubtaskDescription =
                new TextField(L.Tr(L.K.FieldQuickSubtaskDescription));
            JiraStyles.ApplyMultiline(_resolveNewSubtaskDescription);
            _resolveNewSubtaskDescription.style.minHeight = 70;
            _resolveAddSubtaskForm.Add(_resolveNewSubtaskDescription);

            _resolveNewSubtaskPriority =
                new DropdownField(L.Tr(L.K.FieldQuickSubtaskPriority));
            _resolveNewSubtaskPriority.userData = FieldPriority;
            JiraStyles.ApplyDropdown(_resolveNewSubtaskPriority);
            ConfigurePriorityDropdownIcon(_resolveNewSubtaskPriority);
            _resolveAddSubtaskForm.Add(_resolveNewSubtaskPriority);

            _resolveNewSubtaskTeamContainer = new VisualElement();
            _resolveNewSubtaskTeam =
                new DropdownField(L.Tr(L.K.FieldTeam));
            JiraStyles.ApplyDropdown(_resolveNewSubtaskTeam);
            _resolveNewSubtaskTeamContainer.Add(
                _resolveNewSubtaskTeam);
            _resolveNewSubtaskTeamText =
                new TextField(L.Tr(L.K.FieldTeam));
            JiraStyles.ApplyField(_resolveNewSubtaskTeamText);
            _resolveNewSubtaskTeamContainer.Add(
                _resolveNewSubtaskTeamText);
            _resolveNewSubtaskTeamContainer.style.display =
                DisplayStyle.None;
            _resolveAddSubtaskForm.Add(
                _resolveNewSubtaskTeamContainer);

            _resolveNewSubtaskAssigneeContainer = new VisualElement();
            _resolveNewSubtaskAssignee =
                new DropdownField(L.Tr(L.K.FieldAssignee));
            JiraStyles.ApplyDropdown(_resolveNewSubtaskAssignee);
            _resolveNewSubtaskAssigneeContainer.Add(
                _resolveNewSubtaskAssignee);
            var resolveAssignSelfButton = new Button(() =>
                AssignDropdownToSelf(_resolveNewSubtaskAssignee))
            {
                text = L.Tr(L.K.BtnAssignSelf)
            };
            JiraStyles.ApplyGhostButton(resolveAssignSelfButton);
            resolveAssignSelfButton.style.marginBottom = 10;
            _resolveNewSubtaskAssigneeContainer.Add(
                resolveAssignSelfButton);
            _resolveNewSubtaskAssigneeContainer.style.display =
                DisplayStyle.None;
            _resolveAddSubtaskForm.Add(
                _resolveNewSubtaskAssigneeContainer);

            _resolveNewSubtaskDatesContainer = new VisualElement();
            _resolveNewSubtaskStartDate =
                new TextField(L.Tr(L.K.FieldStartDate));
            _resolveNewSubtaskStartDate.tooltip = L.Tr(L.K.DateHint);
            JiraStyles.ApplyField(_resolveNewSubtaskStartDate);
            _resolveNewSubtaskDueDate =
                new TextField(L.Tr(L.K.FieldDueDate));
            _resolveNewSubtaskDueDate.tooltip = L.Tr(L.K.DateHint);
            JiraStyles.ApplyField(_resolveNewSubtaskDueDate);
            _resolveNewSubtaskDatesContainer.Add(
                JiraStyles.Row(
                    _resolveNewSubtaskStartDate,
                    _resolveNewSubtaskDueDate));
            var resolveSubtaskDateHint =
                new Label(L.Tr(L.K.DateHint));
            JiraStyles.ApplyFieldHint(resolveSubtaskDateHint);
            _resolveNewSubtaskDatesContainer.Add(
                resolveSubtaskDateHint);
            _resolveNewSubtaskDatesContainer.style.display =
                DisplayStyle.None;
            _resolveAddSubtaskForm.Add(
                _resolveNewSubtaskDatesContainer);

            var resolveSubtaskAttachment = new VisualElement();
            _resolveNewChildAttachmentTitle = new Label(
                L.Tr(L.K.FieldSubtaskAttachment));
            JiraStyles.ApplyDynamicFieldLabel(
                _resolveNewChildAttachmentTitle);
            resolveSubtaskAttachment.Add(
                _resolveNewChildAttachmentTitle);
            var resolveSubtaskAttachmentRow = new VisualElement();
            resolveSubtaskAttachmentRow.style.flexDirection =
                FlexDirection.Row;
            resolveSubtaskAttachmentRow.style.flexWrap = Wrap.Wrap;
            var resolveSelectSubtaskAttachment = new Button(
                SelectResolveSubtaskAttachment)
            {
                text = L.Tr(L.K.BtnSelectFile)
            };
            JiraStyles.ApplyGhostButton(
                resolveSelectSubtaskAttachment);
            resolveSelectSubtaskAttachment.style.marginRight = 8;
#if UNITY_EDITOR_WIN
            var resolveClipSubtaskAttachment = new Button(
                StartResolveSubtaskScreenClip)
            {
                text = L.Tr(L.K.BtnCaptureScreenArea)
            };
            JiraStyles.ApplyGhostButton(
                resolveClipSubtaskAttachment);
            resolveClipSubtaskAttachment.style.marginRight = 8;
#endif
            var resolveRemoveSubtaskAttachment = new Button(
                ClearResolveSubtaskAttachment)
            {
                text = L.Tr(L.K.BtnRemoveFile)
            };
            JiraStyles.ApplyGhostButton(
                resolveRemoveSubtaskAttachment);
            resolveSubtaskAttachmentRow.Add(
                resolveSelectSubtaskAttachment);
#if UNITY_EDITOR_WIN
            resolveSubtaskAttachmentRow.Add(
                resolveClipSubtaskAttachment);
#endif
            resolveSubtaskAttachmentRow.Add(
                resolveRemoveSubtaskAttachment);
            resolveSubtaskAttachment.Add(
                resolveSubtaskAttachmentRow);
            _resolveNewSubtaskAttachmentLabel =
                new Label(L.Tr(L.K.NoFileSelected));
            JiraStyles.ApplyFieldHint(
                _resolveNewSubtaskAttachmentLabel);
            _resolveNewSubtaskAttachmentLabel.style.marginTop = 7;
            resolveSubtaskAttachment.Add(
                _resolveNewSubtaskAttachmentLabel);
            _resolveNewSubtaskAttachmentPreview =
                CreateInlineAttachmentPreview();
            resolveSubtaskAttachment.Add(
                _resolveNewSubtaskAttachmentPreview.Root);
            var resolveInlineImageHint = new Label(
                L.Tr(L.K.AttachmentInlineDescriptionHint));
            JiraStyles.ApplyFieldHint(resolveInlineImageHint);
            resolveSubtaskAttachment.Add(resolveInlineImageHint);
            _resolveAddSubtaskForm.Add(
                resolveSubtaskAttachment);

            _resolveCreateSubtaskButton =
                new Button(() => CreateResolveSubtaskAsync())
                {
                    text = L.Tr(L.K.BtnCreateSubtask)
                };
            JiraStyles.ApplyPrimaryButton(_resolveCreateSubtaskButton);
            _resolveCreateSubtaskButton.style.marginRight = 0;
            _resolveAddSubtaskForm.Add(_resolveCreateSubtaskButton);
            _resolveSubtasksCard.Add(_resolveAddSubtaskForm);
            _resolveDetailBody.Add(_resolveSubtasksCard);

            var updateTitle = new Label(L.Tr(L.K.ResolveUpdateTitle));
            JiraStyles.ApplyDynamicFieldLabel(updateTitle);
            updateTitle.style.marginTop = 14;
            updateTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _resolveDetailBody.Add(updateTitle);

            _resolveCommentField = new TextField(L.Tr(L.K.FieldComment));
            JiraStyles.ApplyMultiline(_resolveCommentField);
            _resolveDetailBody.Add(_resolveCommentField);

            var mentionLabel = new Label(L.Tr(L.K.FieldMention));
            JiraStyles.ApplyDynamicFieldLabel(mentionLabel);
            _resolveDetailBody.Add(mentionLabel);

            _mentionSearchField = new TextField();
            _mentionSearchField.tooltip = L.Tr(L.K.MentionSearchPlaceholder);
            JiraStyles.ApplyField(_mentionSearchField);
            _mentionSearchField.style.marginBottom = 4;
            _mentionSearchField.RegisterValueChangedCallback(evt => OnMentionSearchChanged(evt.newValue));
            _resolveDetailBody.Add(_mentionSearchField);

            _mentionResults = new VisualElement();
            _resolveDetailBody.Add(_mentionResults);

            _mentionChips = new VisualElement();
            _mentionChips.style.flexDirection = FlexDirection.Row;
            _mentionChips.style.flexWrap = Wrap.Wrap;
            _mentionChips.style.marginBottom = 8;
            _resolveDetailBody.Add(_mentionChips);

            var attachRow = new VisualElement();
            attachRow.style.flexDirection = FlexDirection.Row;
            attachRow.style.alignItems = Align.Center;
            var attachSelect = new Button(SelectResolveAttachment) { text = L.Tr(L.K.BtnSelectFile) };
            JiraStyles.ApplyGhostButton(attachSelect);
            attachSelect.style.marginRight = 8;
            var attachRemove = new Button(ClearResolveAttachment) { text = L.Tr(L.K.BtnRemoveFile) };
            JiraStyles.ApplyGhostButton(attachRemove);
            attachRow.Add(attachSelect);
            attachRow.Add(attachRemove);
            _resolveDetailBody.Add(attachRow);

            _resolveAttachmentLabel = new Label(L.Tr(L.K.AttachFixHint));
            JiraStyles.ApplyFieldHint(_resolveAttachmentLabel);
            _resolveAttachmentLabel.style.marginTop = 6;
            _resolveDetailBody.Add(_resolveAttachmentLabel);
            var resolveInlineAttachmentHint = new Label(
                L.Tr(L.K.AttachmentInlineDescriptionHint));
            JiraStyles.ApplyFieldHint(resolveInlineAttachmentHint);
            _resolveDetailBody.Add(resolveInlineAttachmentHint);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8;

            var updateButton = new Button(() => UpdateActivityAsync()) { text = L.Tr(L.K.BtnUpdateActivity) };
            JiraStyles.ApplyPrimaryButton(updateButton);
            updateButton.style.flexGrow = 1;
            updateButton.style.marginRight = 0;

            actions.Add(updateButton);
            _resolveDetailBody.Add(actions);

            _resolveStatus = new Label();
            _resolveStatus.style.display = DisplayStyle.None;
            _resolveDetailBody.Add(_resolveStatus);
            _resolveDetail.Add(_resolveDetailBody);

            SetDetailInteractable(false);
            return _resolveDetail;
        }

        private void SetDetailInteractable(bool hasIssue)
        {
            if (_resolveDetail != null)
                _resolveDetail.style.display = hasIssue ? DisplayStyle.Flex : DisplayStyle.None;
            if (_resolveDetailBody != null)
                _resolveDetailBody.style.display = hasIssue ? DisplayStyle.Flex : DisplayStyle.None;
            if (_resolveFiltersCard != null)
                _resolveFiltersCard.style.display = hasIssue ? DisplayStyle.None : DisplayStyle.Flex;
            if (_issueListCard != null)
                _issueListCard.style.display = hasIssue ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshResolveAvailability()
        {
            bool connected = _isConnected;
            _resolveNotice.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            _resolveContent.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected && !_resolveStatusesLoaded)
            {
                if (!_resolveStatusesLoading)
                    LoadResolveStatusesAsync(true);
            }
            else if (connected && !_issuesLoaded && !_issuesLoading)
            {
                LoadIssuesAsync();
            }
            if (connected && !_resolveEpicsLoaded && !_resolveEpicsLoading)
                LoadResolveEpicsAsync();
        }

        private async void LoadResolveEpicsAsync()
        {
            JiraClient client = BuildClientOrNull();
            if (client == null || _resolveEpicDropdown == null)
                return;

            int version = ++_resolveEpicLoadVersion;
            _resolveEpicsLoading = true;
            _resolveEpicDropdown.SetEnabled(false);
            _resolveEpicDropdown.choices = new List<string> { L.Tr(L.K.StatusLoading) };
            _resolveEpicDropdown.SetValueWithoutNotify(L.Tr(L.K.StatusLoading));

            try
            {
                string projectKey = JiraPreferences.PresetProject;
                if (string.IsNullOrWhiteSpace(projectKey))
                {
                    if (_projects.Count > 0)
                    {
                        projectKey = _projects[0].key;
                    }
                    else
                    {
                        List<JiraProject> projects = await client.GetProjectsAsync();
                        if (version != _resolveEpicLoadVersion)
                            return;
                        if (projects.Count > 0)
                            projectKey = projects[0].key;
                    }
                }

                if (!string.Equals(
                        _resolveProjectKey,
                        projectKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _resolveProjectKey = projectKey;
                    _resolveAssignableProjectKey = null;
                    _resolveAssignableUsersLoaded = false;
                    _resolveAssignableUsers.Clear();
                }

                var epics = string.IsNullOrWhiteSpace(projectKey)
                    ? new List<JiraEpic>()
                    : await client.GetProjectEpicsAsync(projectKey);
                if (version != _resolveEpicLoadVersion)
                    return;

                _resolveEpics.Clear();
                _resolveEpics.AddRange(epics);
                _selectedResolveEpic = null;
                FilterResolveEpicChoices(string.Empty);
                _resolveEpicDropdown.SetEnabled(true);
                _resolveEpicsLoaded = true;
            }
            catch (Exception exception)
            {
                if (version != _resolveEpicLoadVersion)
                    return;

                _resolveEpicDropdown.choices =
                    new List<string> { L.Tr(L.K.ResolveAllEpics) };
                _resolveEpicDropdown.SetValueWithoutNotify(L.Tr(L.K.ResolveAllEpics));
                _resolveEpicDropdown.SetEnabled(true);
                ShowNotification(new GUIContent(
                    L.Tr(L.K.MsgResolveEpicLoadFailed, exception.Message)));
            }
            finally
            {
                if (version == _resolveEpicLoadVersion)
                    _resolveEpicsLoading = false;
            }
        }

        private void FilterResolveEpicChoices(string query)
        {
            if (_resolveEpicDropdown == null)
                return;

            string normalizedQuery = query?.Trim() ?? string.Empty;
            _filteredResolveEpics.Clear();
            foreach (JiraEpic epic in _resolveEpics)
            {
                if (epic == null)
                    continue;

                if (normalizedQuery.Length == 0 ||
                    ContainsIgnoreCase(epic.DisplayName, normalizedQuery) ||
                    ContainsIgnoreCase(epic.key, normalizedQuery))
                {
                    _filteredResolveEpics.Add(epic);
                }
            }

            var labels = new List<string> { L.Tr(L.K.ResolveAllEpics) };
            foreach (JiraEpic epic in _filteredResolveEpics)
                labels.Add($"{epic.DisplayName} ({epic.key})");

            int selectedIndex = -1;
            if (_selectedResolveEpic != null)
            {
                selectedIndex = _filteredResolveEpics.FindIndex(epic =>
                    string.Equals(
                        epic.key,
                        _selectedResolveEpic.key,
                        StringComparison.OrdinalIgnoreCase));
            }

            bool selectionWasCleared =
                _selectedResolveEpic != null && selectedIndex < 0;
            if (selectionWasCleared)
                _selectedResolveEpic = null;

            _resolveEpicDropdown.choices = labels;
            _resolveEpicDropdown.SetValueWithoutNotify(
                selectedIndex >= 0 ? labels[selectedIndex + 1] : labels[0]);

            UpdateResolveEpicSelectedLabel();

            // Typing only filters the local list. A new API request is needed
            // only when the active epic stopped matching and was cleared.
            if (selectionWasCleared)
                LoadIssuesAsync();
        }

        private void UpdateResolveEpicSelectedLabel()
        {
            if (_resolveEpicSelectedLabel == null)
                return;

            _resolveEpicSelectedLabel.text = _selectedResolveEpic == null
                ? L.Tr(L.K.ResolveEpicAllSelected)
                : L.Tr(
                    L.K.ResolveEpicSelected,
                    _selectedResolveEpic.key,
                    _selectedResolveEpic.DisplayName);
        }

        private void SelectResolveEpicAndReload()
        {
            int index = _resolveEpicDropdown.index - 1;
            _selectedResolveEpic =
                index >= 0 && index < _filteredResolveEpics.Count
                    ? _filteredResolveEpics[index]
                    : null;

            FilterResolveEpicChoices(string.Empty);
            UpdateResolveEpicSelectedLabel();
            LoadIssuesAsync();
        }

        private async void LoadResolveStatusesAsync(bool reloadIssues)
        {
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            int version = ++_resolveStatusLoadVersion;
            string previouslySelected = _selectedResolveStatus?.name;
            _resolveStatusesLoading = true;
            RefreshResolveStatusDropdown();
            bool loadIssuesAfterSync = false;

            try
            {
                List<JiraWorkflowStatus> remoteStatuses =
                    await client.GetStatusesAsync();
                if (version != _resolveStatusLoadVersion)
                    return;

                var uniqueNames = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                _resolveStatuses.Clear();

                for (int i = 0; i < remoteStatuses.Count; i++)
                {
                    JiraWorkflowStatus remote = remoteStatuses[i];
                    if (remote == null ||
                        string.IsNullOrWhiteSpace(remote.name) ||
                        !uniqueNames.Add(remote.name.Trim()))
                    {
                        continue;
                    }

                    _resolveStatuses.Add(remote);
                }

                _resolveStatuses.Sort((left, right) =>
                {
                    int categoryOrder = ResolveStatusCategoryOrder(left)
                        .CompareTo(ResolveStatusCategoryOrder(right));
                    return categoryOrder != 0
                        ? categoryOrder
                        : string.Compare(
                            left?.name,
                            right?.name,
                            StringComparison.CurrentCultureIgnoreCase);
                });

                _selectedResolveStatus =
                    FindResolveStatus(previouslySelected);
                if (_selectedResolveStatus == null)
                {
                    _selectedResolveStatus = _resolveStatuses.Find(status =>
                        string.Equals(
                            status.statusCategory?.key,
                            "new",
                            StringComparison.OrdinalIgnoreCase));
                }
                if (_selectedResolveStatus == null &&
                    _resolveStatuses.Count > 0)
                {
                    _selectedResolveStatus = _resolveStatuses[0];
                }

                _resolveStatusesLoaded = true;
                loadIssuesAfterSync = reloadIssues || !_issuesLoaded;
            }
            catch (Exception exception)
            {
                if (version != _resolveStatusLoadVersion)
                    return;

                _resolveStatusesLoaded = true;
                if (_resolveStatuses.Count == 0)
                    _selectedResolveStatus = null;
                loadIssuesAfterSync = reloadIssues || !_issuesLoaded;
                ShowNotification(new GUIContent(
                    L.Tr(L.K.MsgResolveStatusLoadFailed, exception.Message)));
            }
            finally
            {
                if (version == _resolveStatusLoadVersion)
                {
                    _resolveStatusesLoading = false;
                    RefreshResolveStatusDropdown();
                    if (loadIssuesAfterSync)
                    {
                        _issuesLoaded = false;
                        LoadIssuesAsync();
                    }
                }
            }
        }

        private JiraWorkflowStatus FindResolveStatus(string jiraName)
        {
            if (string.IsNullOrWhiteSpace(jiraName))
                return null;

            return _resolveStatuses.Find(status =>
                    string.Equals(
                    status.name,
                    jiraName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshResolveStatusDropdown()
        {
            if (_resolveStatusDropdown == null)
                return;

            if (_resolveStatusesLoading)
            {
                string loading = L.Tr(L.K.StatusLoading);
                _resolveStatusDropdown.choices = new List<string> { loading };
                _resolveStatusDropdown.SetValueWithoutNotify(loading);
                _resolveStatusDropdown.SetEnabled(false);
                return;
            }

            var choices = new List<string> { L.Tr(L.K.FilterAll) };
            foreach (JiraWorkflowStatus status in _resolveStatuses)
                choices.Add(status.name);

            _resolveStatusDropdown.choices = choices;
            int selectedIndex = _selectedResolveStatus == null
                ? 0
                : _resolveStatuses.IndexOf(_selectedResolveStatus) + 1;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, choices.Count - 1);
            _resolveStatusDropdown.SetValueWithoutNotify(choices[selectedIndex]);
            _resolveStatusDropdown.SetEnabled(true);
            _resolveStatusDropdown.schedule.Execute(
                ApplyResolveStatusDropdownColor);
        }

        private void SelectResolveStatusFromDropdown()
        {
            if (_resolveStatusDropdown == null)
                return;

            int index = _resolveStatusDropdown.index - 1;
            JiraWorkflowStatus status =
                index >= 0 && index < _resolveStatuses.Count
                    ? _resolveStatuses[index]
                    : null;
            SetResolveStatusFilter(status);
        }

        private void SetResolveStatusFilter(JiraWorkflowStatus status)
        {
            _selectedResolveStatus = status;
            _resolveIssuePage = 0;
            RefreshResolveStatusDropdown();
            LoadIssuesAsync();
        }

        private static string ResolveStatusLabel(JiraWorkflowStatus status)
        {
            return status == null ? L.Tr(L.K.FilterAll) : status.name;
        }

        private string ResolveFilterJql()
        {
            var clauses = new List<string>();
            if (_selectedResolveStatus != null &&
                !string.IsNullOrWhiteSpace(_selectedResolveStatus.name))
            {
                string statusName = _selectedResolveStatus.name.Replace(
                    "\\",
                    "\\\\").Replace(
                    "\"",
                    "\\\"");
                clauses.Add($"status = \"{statusName}\"");
            }

            if (_selectedResolveAssignee != null &&
                !string.IsNullOrWhiteSpace(
                    _selectedResolveAssignee.accountId))
            {
                string accountId = _selectedResolveAssignee.accountId.Replace(
                    "\\",
                    "\\\\").Replace(
                    "\"",
                    "\\\"");
                clauses.Add($"assignee = \"{accountId}\"");
            }
            else if (_resolveOwnerScope == ResolveOwnerScope.Mine)
                clauses.Add("assignee = currentUser()");

            JiraEpic epic = SelectedResolveEpic();
            if (epic != null && !string.IsNullOrWhiteSpace(epic.key))
                clauses.Add($"parent = \"{epic.key.Replace("\"", "\\\"")}\"");

            switch (_resolveSprintScope)
            {
                case ResolveSprintScope.Active:
                    clauses.Add("sprint in openSprints()");
                    break;
                case ResolveSprintScope.Backlog:
                    clauses.Add("(sprint is EMPTY OR sprint not in openSprints())");
                    break;
            }

            if (clauses.Count == 0)
                clauses.Add("created is not EMPTY");

            return string.Join(" AND ", clauses) + " ORDER BY updated DESC";
        }

        private void ApplyResolveStatusDropdownColor()
        {
            if (_resolveStatusDropdown == null)
                return;

            Color color = ResolveStatusColor(_selectedResolveStatus);
            TextElement text = _resolveStatusDropdown.Q<TextElement>(
                className: "unity-base-popup-field__text");
            if (text != null)
            {
                text.style.color =
                    new StyleColor(Color.Lerp(color, Color.white, 0.18f));
                text.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            VisualElement input = _resolveStatusDropdown.Q<VisualElement>(
                className: "unity-base-popup-field__input");
            if (input == null)
                return;

            var border = new StyleColor(new Color(
                color.r,
                color.g,
                color.b,
                0.72f));
            input.style.borderLeftColor = border;
            input.style.borderRightColor = border;
            input.style.borderTopColor = border;
            input.style.borderBottomColor = border;
        }

        private static int ResolveStatusCategoryOrder(JiraWorkflowStatus status)
        {
            string categoryKey = status?.statusCategory?.key;
            if (string.Equals(
                    categoryKey,
                    "new",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(
                    categoryKey,
                    "indeterminate",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(
                    categoryKey,
                    "done",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 3;
        }

        private static string ResolveStatusCategoryLabel(
            JiraWorkflowStatus status)
        {
            switch (ResolveStatusCategoryOrder(status))
            {
                case 0:
                    return L.Tr(L.K.StatusCategoryTodo);
                case 1:
                    return L.Tr(L.K.StatusCategoryInProgress);
                case 2:
                    return L.Tr(L.K.StatusCategoryDone);
                default:
                    return L.Tr(L.K.StatusCategoryOther);
            }
        }

        private static Color ResolveStatusColor(JiraWorkflowStatus status)
        {
            string categoryKey = status?.statusCategory?.key;
            if (string.Equals(
                    categoryKey,
                    "done",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Color32(54, 179, 126, 255);
            }

            if (string.Equals(
                    categoryKey,
                    "indeterminate",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Color32(38, 132, 255, 255);
            }

            return new Color32(151, 160, 175, 255);
        }

        private async void LoadIssuesAsync()
        {
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            int version = ++_issueLoadVersion;
            _issuesLoading = true;
            _resolveIssuePage = 0;
            _issueListStatus.style.display = DisplayStyle.Flex;
            _issueListStatus.text = L.Tr(L.K.MsgLoadingIssues);
            _issueListContainer.Clear();
            if (_issuePagination != null)
                _issuePagination.style.display = DisplayStyle.None;

            string jql = ResolveFilterJql();

            try
            {
                List<JiraListIssue> issues = await client.SearchIssuesAsync(jql, 500);
                if (version != _issueLoadVersion)
                    return;

                _resolveIssues.Clear();
                _resolveIssues.AddRange(issues);
                _issuesLoaded = true;
                RenderIssueList();
            }
            catch (Exception exception)
            {
                if (version != _issueLoadVersion)
                    return;

                _resolveIssues.Clear();
                _issueListContainer.Clear();
                _issuesLoaded = false;
                _issueListStatus.text = exception.Message;
                if (_issuePagination != null)
                    _issuePagination.style.display = DisplayStyle.None;
            }
            finally
            {
                if (version == _issueLoadVersion)
                    _issuesLoading = false;
            }
        }

        private void RenderIssueList()
        {
            if (_issueListContainer == null)
                return;

            _issueListContainer.Clear();
            string query = _issueSearchField?.value?.Trim();

            var visible = new List<JiraListIssue>();
            foreach (JiraListIssue issue in _resolveIssues)
            {
                if (issue == null || string.IsNullOrWhiteSpace(issue.key))
                    continue;
                if (!string.IsNullOrWhiteSpace(query) &&
                    !IssueOrSubtaskMatchesSearch(issue, query))
                    continue;
                visible.Add(issue);
            }

            visible.Sort((a, b) =>
            {
                bool pa = JiraPreferences.IsIssuePinned(a.key);
                bool pb = JiraPreferences.IsIssuePinned(b.key);
                if (pa != pb)
                    return pa ? -1 : 1;
                return 0;
            });

            if (visible.Count == 0)
            {
                _issueListStatus.style.display = DisplayStyle.Flex;
                _issueListStatus.text = L.Tr(L.K.MsgNoIssues);
                if (_issuePagination != null)
                    _issuePagination.style.display = DisplayStyle.None;
                return;
            }

            _issueListStatus.style.display = DisplayStyle.None;
            int pageCount = Mathf.Max(
                1,
                Mathf.CeilToInt(visible.Count / (float)ResolveIssuesPerPage));
            _resolveIssuePage = Mathf.Clamp(_resolveIssuePage, 0, pageCount - 1);

            int firstIndex = _resolveIssuePage * ResolveIssuesPerPage;
            int lastIndex = Mathf.Min(
                firstIndex + ResolveIssuesPerPage,
                visible.Count);
            for (int i = firstIndex; i < lastIndex; i++)
                _issueListContainer.Add(BuildIssueRow(visible[i]));

            UpdateIssuePagination(visible.Count, pageCount);
        }

        private void ChangeIssuePage(int delta)
        {
            _resolveIssuePage = Mathf.Max(0, _resolveIssuePage + delta);
            RenderIssueList();
        }

        private void UpdateIssuePagination(int issueCount, int pageCount)
        {
            if (_issuePagination == null)
                return;

            _issuePagination.style.display =
                pageCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _issuePageLabel.text = L.Tr(
                L.K.ResolvePageFormat,
                _resolveIssuePage + 1,
                pageCount,
                issueCount);
            _issuePreviousPageButton.SetEnabled(_resolveIssuePage > 0);
            _issueNextPageButton.SetEnabled(_resolveIssuePage + 1 < pageCount);
        }

        private static bool IssueOrSubtaskMatchesSearch(
            JiraListIssue issue,
            string query)
        {
            if (ContainsIgnoreCase(issue.key, query) ||
                ContainsIgnoreCase(issue.Summary, query))
            {
                return true;
            }

            foreach (JiraSubtask subtask in issue.Subtasks)
            {
                if (subtask != null &&
                    (ContainsIgnoreCase(subtask.key, query) ||
                     ContainsIgnoreCase(subtask.Summary, query)))
                {
                    return true;
                }
            }

            return false;
        }

        private VisualElement BuildIssueRow(JiraListIssue issue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 34;
            row.style.marginBottom = 5;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color32(58, 63, 73, 255));

            bool pinned = JiraPreferences.IsIssuePinned(issue.key);
            var pin = new Button(() =>
            {
                JiraPreferences.ToggleIssuePinned(issue.key);
                RenderIssueList();
            })
            { text = pinned ? "★" : "☆" };
            pin.tooltip = L.Tr(L.K.PinTooltip);
            JiraStyles.ApplyGhostButton(pin);
            pin.style.minWidth = 28;
            pin.style.marginRight = 6;
            if (pinned)
                pin.style.color = new StyleColor(new Color32(255, 196, 0, 255));
            row.Add(pin);

            var key = new Label(issue.key);
            key.style.width = 78;
            key.style.flexShrink = 0;
            key.style.fontSize = 11;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.color = new StyleColor(new Color32(173, 181, 194, 255));
            row.Add(key);

            var select = new Button(() => SelectIssue(issue))
            {
                text = issue.Summary
            };
            select.tooltip = issue.Summary;
            JiraStyles.ApplyGhostButton(select);
            select.style.flexGrow = 1;
            select.style.flexShrink = 1;
            select.style.minWidth = 0;
            select.style.overflow = Overflow.Hidden;
            select.style.whiteSpace = WhiteSpace.NoWrap;
            select.style.textOverflow = TextOverflow.Ellipsis;
            select.style.unityTextAlign = TextAnchor.MiddleLeft;
            bool isSelected = _selectedIssue != null && _selectedIssue.key == issue.key;
            if (isSelected)
                select.style.color = new StyleColor(new Color32(38, 132, 255, 255));
            row.Add(select);

            if (issue.SubtaskCount > 0)
            {
                var subtaskCount = new Label(
                    L.Tr(L.K.ResolveSubtaskCountCompact, issue.SubtaskCount));
                subtaskCount.tooltip =
                    L.Tr(L.K.ResolveSubtaskCountTooltip, issue.SubtaskCount);
                subtaskCount.style.flexShrink = 0;
                subtaskCount.style.marginLeft = 5;
                subtaskCount.style.marginRight = 5;
                subtaskCount.style.paddingLeft = 7;
                subtaskCount.style.paddingRight = 7;
                subtaskCount.style.paddingTop = 3;
                subtaskCount.style.paddingBottom = 3;
                subtaskCount.style.borderTopLeftRadius = 9;
                subtaskCount.style.borderTopRightRadius = 9;
                subtaskCount.style.borderBottomLeftRadius = 9;
                subtaskCount.style.borderBottomRightRadius = 9;
                subtaskCount.style.backgroundColor =
                    new StyleColor(new Color32(38, 132, 255, 35));
                subtaskCount.style.color =
                    new StyleColor(new Color32(110, 177, 255, 255));
                subtaskCount.style.fontSize = 9;
                subtaskCount.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(subtaskCount);
            }

            var priority = new Button();
            priority.text = string.Empty;
            priority.clicked += () => ShowIssuePriorityMenuAsync(issue, priority);
            priority.tooltip = L.Tr(L.K.PriorityDropdownTooltip, issue.key);
            JiraStyles.ApplyPriorityButton(priority);

            var priorityIcon = new VisualElement();
            priorityIcon.style.width = 20;
            priorityIcon.style.height = 16;
            priorityIcon.style.flexShrink = 0;
            priorityIcon.tooltip = string.IsNullOrWhiteSpace(issue.PriorityName)
                ? L.Tr(L.K.PriorityNotSet)
                : $"{L.Tr(L.K.FieldPriority)}: {issue.PriorityName}";

            Sprite prioritySprite = PrioritySprite(issue.PriorityName);
            if (prioritySprite != null)
            {
                priorityIcon.style.backgroundImage = new StyleBackground(prioritySprite);
#if UNITY_2022_1_OR_NEWER
                priorityIcon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                priorityIcon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                priorityIcon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
#else
                priorityIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#endif
            }
            priority.Add(priorityIcon);
            row.Add(priority);

            var status = new Button
            {
                text = $"{issue.StatusName}  ▾"
            };
            status.clicked += () => ShowIssueStatusMenuAsync(issue, status);
            status.tooltip = L.Tr(L.K.StatusDropdownTooltip, issue.key);
            JiraStyles.ApplyGhostButton(status);
            status.style.width = 184;
            status.style.minWidth = 184;
            status.style.flexShrink = 0;
            status.style.marginLeft = 0;
            status.style.fontSize = 10;
            status.style.color = StatusColor(issue.StatusCategory);
            row.Add(status);

            return row;
        }

        private static StyleColor StatusColor(string category)
        {
            switch (category)
            {
                case "done": return new StyleColor(new Color32(54, 179, 126, 255));
                case "indeterminate": return new StyleColor(new Color32(38, 132, 255, 255));
                default: return new StyleColor(new Color32(173, 181, 194, 255));
            }
        }

        private static Sprite PrioritySprite(string priorityName)
        {
            if (_prioritySprites == null)
            {
                _prioritySprites = Resources.LoadAll<Sprite>("priority");
                Array.Sort(_prioritySprites, (a, b) =>
                    string.CompareOrdinal(a?.name, b?.name));
            }

            int index = PriorityIndex(priorityName);
            return index >= 0 && index < _prioritySprites.Length
                ? _prioritySprites[index]
                : null;
        }

        private static VisualElement BuildPriorityIcon(string priorityName)
        {
            var icon = new VisualElement();
            icon.style.width = 20;
            icon.style.minWidth = 20;
            icon.style.height = 16;
            icon.style.flexShrink = 0;

            ApplyPriorityIconSprite(icon, priorityName);
            return icon;
        }

        private static void ApplyPriorityIconSprite(
            VisualElement icon,
            string priorityName)
        {
            if (icon == null)
                return;

            Sprite sprite = PrioritySprite(priorityName);
            if (sprite != null)
            {
                icon.style.opacity = 1f;
                icon.style.backgroundImage = new StyleBackground(sprite);
#if UNITY_2022_1_OR_NEWER
                icon.style.backgroundPositionX =
                    new BackgroundPosition(BackgroundPositionKeyword.Center);
                icon.style.backgroundPositionY =
                    new BackgroundPosition(BackgroundPositionKeyword.Center);
                icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
#else
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#endif
            }
            else
            {
                icon.style.opacity = 0f;
            }
        }

        private static void ConfigurePriorityDropdownIcon(
            DropdownField dropdown)
        {
            if (dropdown == null)
                return;

            dropdown.userData = FieldPriority;
            EnsurePriorityDropdownIcon(dropdown);
            dropdown.RegisterValueChangedCallback(_ =>
                RefreshPriorityDropdownIcon(dropdown));
            dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EnsurePriorityDropdownIcon(dropdown);
                RefreshPriorityDropdownIcon(dropdown);
            });
        }

        private static void EnsurePriorityDropdownIcon(
            DropdownField dropdown)
        {
            if (dropdown == null ||
                dropdown.Q<VisualElement>(PriorityDropdownIconName) != null)
            {
                return;
            }

            VisualElement input = dropdown.Q<VisualElement>(
                className: "unity-base-popup-field__input");
            if (input == null)
                return;

            input.style.flexDirection = FlexDirection.Row;
            input.style.alignItems = Align.Center;

            var icon = BuildPriorityIcon(dropdown.value);
            icon.name = PriorityDropdownIconName;
            icon.pickingMode = PickingMode.Ignore;
            icon.style.alignSelf = Align.Center;
            icon.style.marginTop = 0;
            icon.style.marginBottom = 0;
            icon.style.marginRight = 7;
            input.Insert(0, icon);
        }

        private static void RefreshPriorityDropdownIcon(
            DropdownField dropdown)
        {
            EnsurePriorityDropdownIcon(dropdown);
            VisualElement icon =
                dropdown?.Q<VisualElement>(PriorityDropdownIconName);
            ApplyPriorityIconSprite(icon, dropdown?.value);
        }

        private static int PriorityIndex(string priorityName)
        {
            string value = priorityName?.Trim().ToLowerInvariant();
            switch (value)
            {
                case "muito baixo":
                case "mais baixo":
                case "baixíssima":
                case "baixissima":
                case "lowest":
                    return 0;
                case "baixo":
                case "low":
                    return 1;
                case "médio":
                case "medio":
                case "medium":
                    return 2;
                case "alto":
                case "high":
                    return 3;
                case "muito alto":
                case "mais alto":
                case "altíssima":
                case "altissima":
                case "highest":
                    return 4;
                default:
                    return -1;
            }
        }

        private async void ShowIssuePriorityMenuAsync(JiraListIssue issue, Button anchor)
        {
            if (issue == null ||
                anchor == null ||
                _isResolving ||
                _resolvePrioritiesLoading ||
                _priorityBusyIssues.Contains(issue.key))
                return;

            CloseStatusPopup();
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _priorityBusyIssues.Add(issue.key);
            anchor.SetEnabled(false);

            try
            {
                if (!_resolvePrioritiesLoaded && !_resolvePrioritiesLoading)
                {
                    _resolvePrioritiesLoading = true;
                    List<JiraAllowedValue> priorities = await client.GetPrioritiesAsync();
                    _resolvePriorities.Clear();
                    _resolvePriorities.AddRange(priorities);
                    _resolvePrioritiesLoaded = true;
                }

                if (anchor.panel != null)
                    OpenPriorityPopup(issue, anchor, _resolvePriorities);
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(L.Tr(L.K.MsgResolveFailed, exception.Message)));
            }
            finally
            {
                _resolvePrioritiesLoading = false;
                _priorityBusyIssues.Remove(issue.key);
                if (anchor.panel != null)
                    anchor.SetEnabled(true);
            }
        }

        private void OpenPriorityPopup(
            JiraListIssue issue,
            VisualElement anchor,
            List<JiraAllowedValue> priorities)
        {
            CloseStatusPopup();
            if (rootVisualElement == null || anchor?.panel == null)
                return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(_ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            var issueLabel = new Label(issue.key);
            JiraStyles.ApplyDropdownPopupCaption(issueLabel);
            popup.Add(issueLabel);

            var current = new Label(string.IsNullOrWhiteSpace(issue.PriorityName)
                ? L.Tr(L.K.PriorityNotSet)
                : issue.PriorityName);
            JiraStyles.ApplyDropdownPopupCurrent(
                current,
                new StyleColor(new Color32(255, 196, 0, 255)));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            var options = new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 220;
            int available = 0;
            foreach (JiraAllowedValue priority in priorities)
            {
                if (priority == null ||
                    string.IsNullOrWhiteSpace(priority.id) ||
                    string.IsNullOrWhiteSpace(priority.Display) ||
                    string.Equals(priority.id, issue.PriorityId, StringComparison.Ordinal))
                {
                    continue;
                }

                JiraAllowedValue captured = priority;
                var item = new Button(() =>
                {
                    CloseStatusPopup();
                    ApplyIssuePriorityAsync(issue, captured);
                });
                item.text = string.Empty;
                JiraStyles.ApplyDropdownPopupItem(item);
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;

                VisualElement icon = BuildPriorityIcon(captured.Display);
                icon.style.marginRight = 9;
                item.Add(icon);

                var itemLabel = new Label(captured.Display);
                itemLabel.style.flexGrow = 1;
                itemLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                item.Add(itemLabel);
                options.Add(item);
                available++;
            }

            if (available == 0)
            {
                var empty = new Label(L.Tr(L.K.PriorityNotSet));
                JiraStyles.ApplyDropdownPopupEmpty(empty);
                options.Add(empty);
            }

            popup.Add(options);
            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            PositionResolvePopup(anchor, popup, available, 210f);
        }

        private async void ApplyIssuePriorityAsync(JiraListIssue issue, JiraAllowedValue priority)
        {
            if (issue == null ||
                priority == null ||
                string.IsNullOrWhiteSpace(priority.id) ||
                _priorityBusyIssues.Contains(issue.key))
            {
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _priorityBusyIssues.Add(issue.key);
            if (_selectedIssue != null && _selectedIssue.key == issue.key)
                SetResolveStatus(L.Tr(L.K.MsgUpdatingPriority, issue.key), true);

            try
            {
                string error = await client.UpdateIssuePriorityAsync(issue.key, priority.id);
                if (error != null)
                {
                    ShowNotification(new GUIContent(L.Tr(L.K.MsgResolveFailed, error)));
                    if (_selectedIssue != null && _selectedIssue.key == issue.key)
                        SetResolveStatus(L.Tr(L.K.MsgResolveFailed, error), false);
                    return;
                }

                if (issue.fields == null)
                    issue.fields = new JiraListFields();
                if (issue.fields.priority == null)
                    issue.fields.priority = new JiraListPriority();
                issue.fields.priority.id = priority.id;
                issue.fields.priority.name = priority.Display;

                RenderIssueList();
                ShowNotification(new GUIContent(L.Tr(L.K.MsgPriorityApplied, priority.Display)));
                if (_selectedIssue != null && _selectedIssue.key == issue.key)
                    SetResolveStatus(L.Tr(L.K.MsgPriorityApplied, priority.Display), true);
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(L.Tr(L.K.MsgResolveFailed, exception.Message)));
                if (_selectedIssue != null && _selectedIssue.key == issue.key)
                    SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
            finally
            {
                _priorityBusyIssues.Remove(issue.key);
            }
        }

        private async void ShowIssueStatusMenuAsync(JiraListIssue issue, Button anchor)
        {
            if (issue == null ||
                anchor == null ||
                _isResolving ||
                _statusBusyIssues.Contains(issue.key))
                return;

            CloseStatusPopup();

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _statusBusyIssues.Add(issue.key);
            string originalText = anchor.text;
            anchor.text = L.Tr(L.K.StatusLoading);
            anchor.SetEnabled(false);

            try
            {
                List<JiraTransition> transitions = await client.GetTransitionsAsync(issue.key);
                if (anchor.panel == null)
                    return;

                OpenStatusPopup(issue, anchor, transitions);
            }
            catch (Exception exception)
            {
                SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
            finally
            {
                _statusBusyIssues.Remove(issue.key);
                if (anchor.panel != null)
                {
                    anchor.text = originalText;
                    anchor.SetEnabled(true);
                }
            }
        }

        private void OpenStatusPopup(
            JiraListIssue issue,
            VisualElement anchor,
            List<JiraTransition> transitions)
        {
            CloseStatusPopup();
            if (rootVisualElement == null || anchor?.panel == null)
                return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(_ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            var issueLabel = new Label(issue.key);
            JiraStyles.ApplyDropdownPopupCaption(issueLabel);
            popup.Add(issueLabel);

            var current = new Label(issue.StatusName);
            JiraStyles.ApplyDropdownPopupCurrent(current, StatusColor(issue.StatusCategory));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            var options = new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 236;
            int available = 0;

            foreach (JiraTransition transition in transitions)
            {
                if (transition == null || string.IsNullOrWhiteSpace(transition.id))
                    continue;

                JiraTransition captured = transition;
                string target = string.IsNullOrWhiteSpace(captured.TargetStatus)
                    ? captured.name
                    : captured.TargetStatus;
                if (string.IsNullOrWhiteSpace(target) ||
                    string.Equals(target, issue.StatusName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var item = new Button(() =>
                {
                    CloseStatusPopup();
                    ApplyIssueTransitionAsync(issue, captured);
                })
                {
                    text = target
                };
                JiraStyles.ApplyDropdownPopupItem(item);
                options.Add(item);
                available++;
            }

            if (available == 0)
            {
                var empty = new Label(L.Tr(L.K.MsgNoTransitions));
                JiraStyles.ApplyDropdownPopupEmpty(empty);
                options.Add(empty);
            }

            popup.Add(options);
            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            PositionResolvePopup(anchor, popup, available, 224f);
        }

        private void PositionResolvePopup(
            VisualElement anchor,
            VisualElement popup,
            int availableItems,
            float popupWidth)
        {
            float popupHeight = Mathf.Min(304f, 78f + Mathf.Max(1, availableItems) * 34f);
            Vector2 below = rootVisualElement.WorldToLocal(
                new Vector2(anchor.worldBound.xMin, anchor.worldBound.yMax + 4f));

            float rootWidth = rootVisualElement.resolvedStyle.width;
            float rootHeight = rootVisualElement.resolvedStyle.height;
            float left = Mathf.Clamp(below.x, 8f, Mathf.Max(8f, rootWidth - popupWidth - 8f));
            float top = below.y;

            if (top + popupHeight > rootHeight - 8f)
            {
                Vector2 above = rootVisualElement.WorldToLocal(
                    new Vector2(anchor.worldBound.xMin, anchor.worldBound.yMin));
                top = Mathf.Max(8f, above.y - popupHeight - 4f);
            }

            popup.style.left = left;
            popup.style.top = top;
            popup.style.width = popupWidth;
            popup.style.maxHeight = popupHeight;
        }

        private void OnStyledDropdownPointerDown(PointerDownEvent evt)
        {
            if (evt == null || evt.button != 0)
                return;

            VisualElement current = evt.target as VisualElement;
            DropdownField dropdown = null;
            while (current != null)
            {
                if (current is DropdownField field)
                {
                    dropdown = field;
                    break;
                }

                if (current == _createForm || current == _resolveContent)
                    break;
                current = current.parent;
            }

            if (dropdown == null || !dropdown.enabledInHierarchy)
            {
                return;
            }

            evt.StopImmediatePropagation();
            OpenStyledDropdownPopup(dropdown);
        }

        private void OpenStyledDropdownPopup(DropdownField dropdown)
        {
            if (_associatedItemDropdowns.TryGetValue(
                    dropdown,
                    out AdditionalFieldBinding associatedBinding))
            {
                OpenAssociatedItemsPopup(
                    dropdown,
                    associatedBinding);
                return;
            }

            if (dropdown == _resolveStatusDropdown)
            {
                OpenResolveStatusFilterPopup(dropdown);
                return;
            }

            if (dropdown == _resolveOwnerScopeDropdown)
            {
                OpenResolveOwnerFilterPopup(dropdown);
                return;
            }

            CloseStatusPopup();
            if (rootVisualElement == null || dropdown?.panel == null)
                return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(_ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            var caption = new Label(string.IsNullOrWhiteSpace(dropdown.label)
                ? L.Tr(L.K.CreateDestTitle)
                : dropdown.label);
            JiraStyles.ApplyDropdownPopupCaption(caption);
            popup.Add(caption);

            var current = new Label(dropdown.value ?? string.Empty);
            JiraStyles.ApplyDropdownPopupCurrent(
                current,
                new StyleColor(new Color32(38, 132, 255, 255)));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            bool showSearch = ShouldShowDropdownSearch(dropdown);
            TextField search = null;
            if (showSearch)
            {
                string searchLabel = dropdown == _resolveEpicDropdown
                    ? L.Tr(L.K.ResolveEpicSearch)
                    : L.Tr(L.K.DropdownSearchLabel);
                search = new TextField(searchLabel);
                JiraStyles.ApplyField(search);
                search.style.marginBottom = 7;
                popup.Add(search);
            }

            var options = new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 224;
            int available = dropdown.choices?.Count ?? 0;
            popup.Add(options);
            if (search != null)
            {
                search.RegisterValueChangedCallback(evt =>
                    RenderStyledDropdownOptions(
                        dropdown,
                        options,
                        evt.newValue));
            }
            RenderStyledDropdownOptions(dropdown, options, string.Empty);

            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            float width = Mathf.Clamp(dropdown.worldBound.width, 280f, 420f);
            PositionResolvePopup(
                dropdown,
                popup,
                Mathf.Min(available, 6) + (showSearch ? 2 : 0),
                width);
            if (search != null)
                search.schedule.Execute(() => search.Focus());
        }

        private void OpenAssociatedItemsPopup(
            DropdownField dropdown,
            AdditionalFieldBinding binding)
        {
            CloseStatusPopup();
            if (rootVisualElement == null ||
                dropdown?.panel == null ||
                binding == null)
            {
                return;
            }

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(
                _ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(
                evt => evt.StopPropagation());

            var caption = new Label(dropdown.label);
            JiraStyles.ApplyDropdownPopupCaption(caption);
            popup.Add(caption);

            var current = new Label(
                dropdown.value ?? L.Tr(L.K.NoneOption));
            JiraStyles.ApplyDropdownPopupCurrent(
                current,
                new StyleColor(new Color32(38, 132, 255, 255)));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            var search = new TextField(
                L.Tr(L.K.AssociatedItemsSearch));
            JiraStyles.ApplyField(search);
            search.style.marginBottom = 2;
            popup.Add(search);

            var hint = new Label(
                L.Tr(L.K.AssociatedItemsSearchHint));
            JiraStyles.ApplyFieldHint(hint);
            hint.style.marginTop = 0;
            hint.style.marginBottom = 7;
            popup.Add(hint);

            var options =
                new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 224;
            popup.Add(options);
            RenderAssociatedItemsCurrent(binding, options);
            search.RegisterValueChangedCallback(evt =>
                SearchAssociatedItemsPopupAsync(
                    binding,
                    options,
                    evt.newValue));

            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            float width = Mathf.Clamp(
                dropdown.worldBound.width,
                300f,
                460f);
            PositionResolvePopup(
                dropdown,
                popup,
                7,
                width);
            search.schedule.Execute(() => search.Focus());
        }

        private void RenderAssociatedItemsCurrent(
            AdditionalFieldBinding binding,
            VisualElement options)
        {
            if (binding == null || options == null)
                return;

            options.Clear();
            bool empty = string.IsNullOrWhiteSpace(
                binding.TextField?.value);
            var none = new Button(() =>
            {
                ClearAssociatedItems(binding);
                CloseStatusPopup();
            })
            {
                text = empty
                    ? $"✓  {L.Tr(L.K.NoneOption)}"
                    : L.Tr(L.K.NoneOption)
            };
            JiraStyles.ApplyDropdownPopupItem(none);
            options.Add(none);

            foreach (string value in SplitValues(
                         binding.TextField?.value ??
                         string.Empty))
            {
                string key = value.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                string capturedKey = key;
                var selected = new Button(() =>
                {
                    RemoveAssociatedItem(
                        binding,
                        capturedKey);
                    CloseStatusPopup();
                })
                {
                    text = $"✓  {capturedKey}",
                    tooltip = capturedKey
                };
                JiraStyles.ApplyDropdownPopupItem(selected);
                selected.style.color =
                    new StyleColor(new Color32(38, 132, 255, 255));
                options.Add(selected);
            }
        }

        private async void SearchAssociatedItemsPopupAsync(
            AdditionalFieldBinding binding,
            VisualElement options,
            string query)
        {
            if (binding == null || options == null)
                return;

            int version = ++binding.IssueSearchVersion;
            string trimmed = query?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.Length < 2)
            {
                RenderAssociatedItemsCurrent(binding, options);
                return;
            }

            options.Clear();
            var loading = new Label(
                L.Tr(L.K.AssociatedItemsSearching));
            JiraStyles.ApplyDropdownPopupEmpty(loading);
            options.Add(loading);

            await Task.Delay(250);
            if (version != binding.IssueSearchVersion ||
                options.panel == null)
            {
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            List<JiraIssuePickerIssue> issues;
            try
            {
                issues = await client.SearchIssuePickerAsync(
                    trimmed,
                    SelectedProject()?.id);
            }
            catch
            {
                issues = new List<JiraIssuePickerIssue>();
            }

            if (version != binding.IssueSearchVersion ||
                options.panel == null)
            {
                return;
            }

            options.Clear();
            if (issues.Count == 0)
            {
                var empty = new Label(
                    L.Tr(L.K.AssociatedItemsNoResults));
                JiraStyles.ApplyDropdownPopupEmpty(empty);
                options.Add(empty);
                return;
            }

            var selectedKeys = new HashSet<string>(
                ReadAssociatedItemKeys(binding),
                StringComparer.OrdinalIgnoreCase);
            foreach (JiraIssuePickerIssue issue in issues)
            {
                if (issue == null ||
                    string.IsNullOrWhiteSpace(issue.key))
                {
                    continue;
                }

                JiraIssuePickerIssue capturedIssue = issue;
                string summary = capturedIssue.DisplaySummary;
                bool selected =
                    selectedKeys.Contains(capturedIssue.key);
                var result = new Button(() =>
                {
                    ToggleAssociatedItem(
                        binding,
                        capturedIssue.key);
                    CloseStatusPopup();
                })
                {
                    text =
                        (selected ? "✓  " : string.Empty) +
                        (string.IsNullOrWhiteSpace(summary)
                            ? capturedIssue.key
                            : $"{capturedIssue.key} - {summary}"),
                    tooltip = summary
                };
                JiraStyles.ApplyDropdownPopupItem(result);
                if (selected)
                {
                    result.style.color =
                        new StyleColor(
                            new Color32(38, 132, 255, 255));
                }
                options.Add(result);
            }
        }

        private static void ToggleAssociatedItem(
            AdditionalFieldBinding binding,
            string issueKey)
        {
            if (binding?.TextField == null ||
                string.IsNullOrWhiteSpace(issueKey))
            {
                return;
            }

            var keys = ReadAssociatedItemKeys(binding);
            int existing = keys.FindIndex(key =>
                string.Equals(
                    key,
                    issueKey,
                    StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                keys.RemoveAt(existing);
            else
                keys.Add(issueKey);

            SetAssociatedItemKeys(binding, keys);
        }

        private static void RemoveAssociatedItem(
            AdditionalFieldBinding binding,
            string issueKey)
        {
            var keys = ReadAssociatedItemKeys(binding);
            keys.RemoveAll(key =>
                string.Equals(
                    key,
                    issueKey,
                    StringComparison.OrdinalIgnoreCase));
            SetAssociatedItemKeys(binding, keys);
        }

        private static void ClearAssociatedItems(
            AdditionalFieldBinding binding)
        {
            SetAssociatedItemKeys(
                binding,
                new List<string>());
        }

        private static List<string> ReadAssociatedItemKeys(
            AdditionalFieldBinding binding)
        {
            var keys = new List<string>();
            var unique = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string value in SplitValues(
                         binding?.TextField?.value ??
                         string.Empty))
            {
                string key = value.Trim();
                if (!string.IsNullOrWhiteSpace(key) &&
                    unique.Add(key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        private static void SetAssociatedItemKeys(
            AdditionalFieldBinding binding,
            List<string> keys)
        {
            if (binding?.TextField == null ||
                binding.Dropdown == null)
            {
                return;
            }

            string value = keys != null && keys.Count > 0
                ? string.Join(", ", keys)
                : string.Empty;
            binding.TextField.SetValueWithoutNotify(value);

            string display = string.IsNullOrWhiteSpace(value)
                ? L.Tr(L.K.NoneOption)
                : value;
            binding.Dropdown.choices = string.IsNullOrWhiteSpace(value)
                ? new List<string>
                {
                    L.Tr(L.K.NoneOption)
                }
                : new List<string>
            {
                L.Tr(L.K.NoneOption),
                display
            };
            binding.Dropdown.SetValueWithoutNotify(display);
        }

        private bool ShouldShowDropdownSearch(DropdownField dropdown)
        {
            return dropdown != _resolveSprintScopeDropdown &&
                   dropdown != _resolveOwnerScopeDropdown;
        }

        private void RenderStyledDropdownOptions(
            DropdownField dropdown,
            ScrollView options,
            string query)
        {
            if (dropdown == null || options == null)
                return;

            options.Clear();
            string normalizedQuery = query?.Trim() ?? string.Empty;
            int matches = 0;

            foreach (string choice in dropdown.choices ??
                     new List<string>())
            {
                if (normalizedQuery.Length > 0 &&
                    !ContainsIgnoreCase(choice, normalizedQuery))
                {
                    continue;
                }

                AddStyledDropdownOption(dropdown, options, choice);
                matches++;
            }

            if (matches > 0)
                return;

            var empty = new Label(L.Tr(L.K.DropdownNoOptions));
            JiraStyles.ApplyDropdownPopupEmpty(empty);
            options.Add(empty);
        }

        private void AddStyledDropdownOption(
            DropdownField dropdown,
            VisualElement container,
            string choice)
        {
            string captured = choice;
            bool selected = string.Equals(
                captured,
                dropdown.value,
                StringComparison.Ordinal);
            var item = new Button(() =>
            {
                CloseStatusPopup();
                dropdown.value = captured;
            });
            JiraStyles.ApplyDropdownPopupItem(item);

            bool isPriority =
                dropdown == _priorityDropdown ||
                string.Equals(
                    dropdown.userData as string,
                    FieldPriority,
                    StringComparison.Ordinal);
            if (isPriority)
            {
                item.text = string.Empty;
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;

                var check = new Label(selected ? "✓" : string.Empty);
                check.style.width = 18;
                check.style.color =
                    new StyleColor(new Color32(38, 132, 255, 255));
                item.Add(check);

                VisualElement icon = BuildPriorityIcon(captured);
                icon.style.marginRight = 9;
                item.Add(icon);

                var choiceLabel = new Label(captured);
                choiceLabel.style.flexGrow = 1;
                choiceLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                item.Add(choiceLabel);
            }
            else
            {
                item.text = selected ? $"✓  {captured}" : captured;
            }

            if (selected)
            {
                item.style.color =
                    new StyleColor(new Color32(38, 132, 255, 255));
                item.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            container.Add(item);
        }

        private void OpenResolveOwnerFilterPopup(DropdownField dropdown)
        {
            CloseStatusPopup();
            if (rootVisualElement == null || dropdown?.panel == null)
                return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(_ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(evt =>
                evt.StopPropagation());

            var caption = new Label(L.Tr(L.K.ResolveOwnerScope));
            JiraStyles.ApplyDropdownPopupCaption(caption);
            popup.Add(caption);

            var current = new Label(dropdown.value ?? string.Empty);
            JiraStyles.ApplyDropdownPopupCurrent(
                current,
                new StyleColor(new Color32(38, 132, 255, 255)));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            var search = new TextField(L.Tr(L.K.ResolveOwnerSearch));
            JiraStyles.ApplyField(search);
            search.style.marginBottom = 2;
            popup.Add(search);

            var hint = new Label(L.Tr(L.K.ResolveOwnerSearchHint));
            JiraStyles.ApplyFieldHint(hint);
            hint.style.marginTop = 0;
            hint.style.marginBottom = 7;
            popup.Add(hint);

            var options = new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 224;
            popup.Add(options);
            RenderResolveOwnerOptions(
                options,
                string.Empty,
                null,
                false);

            search.RegisterValueChangedCallback(evt =>
                SearchResolveOwnersAsync(options, evt.newValue));

            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            float width = Mathf.Clamp(dropdown.worldBound.width, 320f, 460f);
            PositionResolvePopup(dropdown, popup, 9, width);
            search.schedule.Execute(() => search.Focus());
        }

        private async void SearchResolveOwnersAsync(
            ScrollView options,
            string query)
        {
            string normalizedQuery = query?.Trim() ?? string.Empty;
            int version = ++_resolveOwnerSearchVersion;
            if (normalizedQuery.Length < 2)
            {
                RenderResolveOwnerOptions(
                    options,
                    normalizedQuery,
                    null,
                    false);
                return;
            }

            await Task.Delay(250);
            if (version != _resolveOwnerSearchVersion ||
                options?.panel == null)
            {
                return;
            }

            RenderResolveOwnerOptions(
                options,
                normalizedQuery,
                null,
                true);

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            List<JiraUser> users;
            try
            {
                users = await SearchResolveAssignableUsersAsync(
                    client,
                    normalizedQuery);
            }
            catch
            {
                users = new List<JiraUser>();
            }

            if (version != _resolveOwnerSearchVersion ||
                options?.panel == null)
            {
                return;
            }

            RenderResolveOwnerOptions(
                options,
                normalizedQuery,
                users,
                false);
        }

        private async Task<List<JiraUser>> SearchResolveAssignableUsersAsync(
            JiraClient client,
            string query)
        {
            string projectKey = _resolveProjectKey;
            if (string.IsNullOrWhiteSpace(projectKey))
                projectKey = JiraPreferences.PresetProject;

            string normalizedQuery = query?.Trim() ?? string.Empty;
            if (normalizedQuery.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(projectKey))
                {
                    bool sameProject = string.Equals(
                        _resolveAssignableProjectKey,
                        projectKey,
                        StringComparison.OrdinalIgnoreCase);
                    if (!_resolveAssignableUsersLoaded || !sameProject)
                    {
                        List<JiraUser> loadedUsers;
                        JiraProject createProject = SelectedProject();
                        if (createProject != null &&
                            string.Equals(
                                createProject.key,
                                projectKey,
                                StringComparison.OrdinalIgnoreCase) &&
                            _assignableUsers.Count > 0)
                        {
                            loadedUsers =
                                new List<JiraUser>(_assignableUsers);
                        }
                        else
                        {
                            loadedUsers =
                                await client.GetAssignableUsersAsync(
                                    projectKey);
                        }

                        _resolveAssignableUsers.Clear();
                        _resolveAssignableUsers.AddRange(loadedUsers);
                        _resolveAssignableProjectKey = projectKey;
                        _resolveAssignableUsersLoaded = true;
                    }

                    return new List<JiraUser>(
                        _resolveAssignableUsers);
                }

                return new List<JiraUser>();
            }

            var results = new List<JiraUser>();
            var accountIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            AddMatchingUsers(
                results,
                accountIds,
                _resolveAssignableUsers,
                normalizedQuery);
            AddMatchingUsers(
                results,
                accountIds,
                _assignableUsers,
                normalizedQuery);

            List<JiraUser> assignableMatches =
                string.IsNullOrWhiteSpace(projectKey)
                    ? new List<JiraUser>()
                    : await client.SearchAssignableUsersAsync(
                        projectKey,
                        normalizedQuery);
            AddMatchingUsers(
                results,
                accountIds,
                assignableMatches,
                normalizedQuery,
                true);

            List<JiraUser> pickerMatches =
                await client.SearchUserPickerAsync(normalizedQuery);
            AddMatchingUsers(
                results,
                accountIds,
                pickerMatches,
                normalizedQuery,
                true);

            if (results.Count == 0)
            {
                List<JiraUser> globalMatches =
                    await client.SearchUsersAsync(normalizedQuery);
                AddMatchingUsers(
                    results,
                    accountIds,
                    globalMatches,
                    normalizedQuery,
                    true);
            }

            return results;
        }

        private static void AddMatchingUsers(
            List<JiraUser> destination,
            HashSet<string> accountIds,
            IEnumerable<JiraUser> users,
            string query,
            bool serverMatched = false)
        {
            if (destination == null ||
                accountIds == null ||
                users == null)
            {
                return;
            }

            foreach (JiraUser user in users)
            {
                if (user == null ||
                    string.IsNullOrWhiteSpace(user.accountId) ||
                    (!serverMatched &&
                     !MatchesAssignee(user, query)) ||
                    !accountIds.Add(user.accountId))
                {
                    continue;
                }

                destination.Add(user);
                if (destination.Count >= 50)
                    return;
            }
        }

        private void RenderResolveOwnerOptions(
            ScrollView options,
            string query,
            List<JiraUser> users,
            bool loading)
        {
            if (options == null)
                return;

            options.Clear();
            if (loading)
            {
                var loadingLabel = new Label(L.Tr(L.K.ResolveOwnerSearching));
                JiraStyles.ApplyDropdownPopupEmpty(loadingLabel);
                options.Add(loadingLabel);
                return;
            }

            string normalizedQuery = query?.Trim() ?? string.Empty;
            int matches = 0;
            string mine = L.Tr(L.K.ResolveOwnerMine);
            string everyone = L.Tr(L.K.ResolveOwnerEveryone);

            if (normalizedQuery.Length == 0 ||
                ContainsIgnoreCase(mine, normalizedQuery))
            {
                AddResolveOwnerScopeOption(
                    options,
                    mine,
                    ResolveOwnerScope.Mine);
                matches++;
            }

            if (normalizedQuery.Length == 0 ||
                ContainsIgnoreCase(everyone, normalizedQuery))
            {
                AddResolveOwnerScopeOption(
                    options,
                    everyone,
                    ResolveOwnerScope.Everyone);
                matches++;
            }

            var accountIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (_selectedResolveAssignee != null &&
                (normalizedQuery.Length == 0 ||
                 MatchesAssignee(
                     _selectedResolveAssignee,
                     normalizedQuery)))
            {
                AddResolveOwnerUserOption(
                    options,
                    _selectedResolveAssignee);
                accountIds.Add(_selectedResolveAssignee.accountId);
                matches++;
            }

            if (users != null)
            {
                foreach (JiraUser user in users)
                {
                    if (user == null ||
                        string.IsNullOrWhiteSpace(user.accountId) ||
                        !accountIds.Add(user.accountId))
                    {
                        continue;
                    }

                    AddResolveOwnerUserOption(options, user);
                    matches++;
                }
            }

            if (matches > 0)
                return;

            string message = normalizedQuery.Length < 2
                ? L.Tr(L.K.ResolveOwnerSearchHint)
                : L.Tr(L.K.ResolveOwnerNoResults);
            var empty = new Label(message);
            JiraStyles.ApplyDropdownPopupEmpty(empty);
            options.Add(empty);
        }

        private void AddResolveOwnerScopeOption(
            VisualElement container,
            string label,
            ResolveOwnerScope scope)
        {
            bool selected =
                _selectedResolveAssignee == null &&
                _resolveOwnerScope == scope;
            var item = new Button(() =>
            {
                CloseStatusPopup();
                _selectedResolveAssignee = null;
                _resolveOwnerScope = scope;
                RefreshResolveOwnerDropdown();
                LoadIssuesAsync();
            })
            {
                text = selected ? $"✓  {label}" : label
            };
            JiraStyles.ApplyDropdownPopupItem(item);
            if (selected)
            {
                item.style.color =
                    new StyleColor(new Color32(38, 132, 255, 255));
                item.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            container.Add(item);
        }

        private void AddResolveOwnerUserOption(
            VisualElement container,
            JiraUser user)
        {
            bool selected =
                _selectedResolveAssignee != null &&
                string.Equals(
                    _selectedResolveAssignee.accountId,
                    user.accountId,
                    StringComparison.OrdinalIgnoreCase);
            string label = AssigneeDisplay(user);
            var item = new Button(() =>
            {
                CloseStatusPopup();
                _selectedResolveAssignee = user;
                _resolveOwnerScope = ResolveOwnerScope.Everyone;
                RefreshResolveOwnerDropdown();
                LoadIssuesAsync();
            })
            {
                text = selected ? $"✓  {label}" : label,
                tooltip = label
            };
            JiraStyles.ApplyDropdownPopupItem(item);
            if (selected)
            {
                item.style.color =
                    new StyleColor(new Color32(38, 132, 255, 255));
                item.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            container.Add(item);
        }

        private void OpenResolveStatusFilterPopup(DropdownField dropdown)
        {
            CloseStatusPopup();
            if (rootVisualElement == null || dropdown?.panel == null)
                return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.RegisterCallback<PointerDownEvent>(_ => CloseStatusPopup());

            var popup = new VisualElement();
            JiraStyles.ApplyDropdownPopup(popup);
            popup.RegisterCallback<PointerDownEvent>(evt =>
                evt.StopPropagation());

            var caption = new Label(L.Tr(L.K.ResolveStatusFilter));
            JiraStyles.ApplyDropdownPopupCaption(caption);
            popup.Add(caption);

            Color selectedColor = ResolveStatusColor(_selectedResolveStatus);
            var current = new Label(ResolveStatusLabel(_selectedResolveStatus));
            JiraStyles.ApplyDropdownPopupCurrent(
                current,
                new StyleColor(
                    Color.Lerp(selectedColor, Color.white, 0.18f)));
            popup.Add(current);

            var divider = new VisualElement();
            JiraStyles.ApplyDropdownPopupDivider(divider);
            popup.Add(divider);

            var search = new TextField(L.Tr(L.K.StatusSearchLabel));
            JiraStyles.ApplyField(search);
            search.style.marginBottom = 2;
            popup.Add(search);

            var hint = new Label(L.Tr(L.K.StatusSearchExample));
            JiraStyles.ApplyFieldHint(hint);
            hint.style.marginTop = 0;
            hint.style.marginBottom = 7;
            popup.Add(hint);

            var options = new ScrollView(ScrollViewMode.Vertical);
            options.style.maxHeight = 224;
            popup.Add(options);

            search.RegisterValueChangedCallback(evt =>
                RenderResolveStatusPopupOptions(options, evt.newValue));
            RenderResolveStatusPopupOptions(options, string.Empty);

            overlay.Add(popup);
            rootVisualElement.Add(overlay);
            _statusPopupOverlay = overlay;

            float width = Mathf.Clamp(dropdown.worldBound.width, 300f, 440f);
            PositionResolvePopup(
                dropdown,
                popup,
                Mathf.Min(_resolveStatuses.Count + 4, 9),
                width);
            search.schedule.Execute(() => search.Focus());
        }

        private void RenderResolveStatusPopupOptions(
            ScrollView options,
            string query)
        {
            if (options == null)
                return;

            options.Clear();
            string normalizedQuery = query?.Trim() ?? string.Empty;
            int matches = 0;

            string allLabel = L.Tr(L.K.FilterAll);
            if (normalizedQuery.Length == 0 ||
                ContainsIgnoreCase(allLabel, normalizedQuery))
            {
                AddResolveStatusPopupItem(options, null);
                matches++;
            }

            int previousCategory = -1;
            foreach (JiraWorkflowStatus status in _resolveStatuses)
            {
                if (status == null ||
                    (normalizedQuery.Length > 0 &&
                     !ContainsIgnoreCase(status.name, normalizedQuery)))
                {
                    continue;
                }

                int category = ResolveStatusCategoryOrder(status);
                if (category != previousCategory)
                {
                    var group = new Label(
                        ResolveStatusCategoryLabel(status).ToUpperInvariant());
                    JiraStyles.ApplyDropdownPopupCaption(group);
                    group.style.marginTop = matches > 0 ? 7 : 2;
                    group.style.marginBottom = 3;
                    options.Add(group);
                    previousCategory = category;
                }

                AddResolveStatusPopupItem(options, status);
                matches++;
            }

            if (matches > 0)
                return;

            var empty = new Label(L.Tr(L.K.DropdownNoOptions));
            JiraStyles.ApplyDropdownPopupEmpty(empty);
            options.Add(empty);
        }

        private void AddResolveStatusPopupItem(
            VisualElement container,
            JiraWorkflowStatus status)
        {
            bool selected = status == _selectedResolveStatus;
            string label = ResolveStatusLabel(status);
            Color color = ResolveStatusColor(status);
            JiraWorkflowStatus captured = status;

            var item = new Button(() =>
            {
                CloseStatusPopup();
                SetResolveStatusFilter(captured);
            })
            {
                text = string.Empty,
                tooltip = label
            };
            JiraStyles.ApplyDropdownPopupItem(item);
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;

            var check = new Label(selected ? "✓" : string.Empty);
            check.style.width = 18;
            check.style.flexShrink = 0;
            check.style.color = new StyleColor(color);
            item.Add(check);

            var dot = new VisualElement();
            dot.style.width = 9;
            dot.style.height = 9;
            dot.style.minWidth = 9;
            dot.style.marginRight = 9;
            dot.style.flexShrink = 0;
            dot.style.backgroundColor = new StyleColor(color);
            dot.style.borderTopLeftRadius = 5;
            dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5;
            dot.style.borderBottomRightRadius = 5;
            item.Add(dot);

            var text = new Label(label);
            text.style.flexGrow = 1;
            text.style.unityTextAlign = TextAnchor.MiddleLeft;
            item.Add(text);

            if (selected)
            {
                item.style.color = new StyleColor(
                    Color.Lerp(color, Color.white, 0.18f));
                item.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            container.Add(item);
        }

        private void CloseStatusPopup()
        {
            _resolveOwnerSearchVersion++;
            if (_statusPopupOverlay == null)
                return;

            if (_statusPopupOverlay.parent != null)
                _statusPopupOverlay.RemoveFromHierarchy();
            _statusPopupOverlay = null;
        }

        private async void ApplyIssueTransitionAsync(JiraListIssue issue, JiraTransition transition)
        {
            if (issue == null || transition == null || _statusBusyIssues.Contains(issue.key))
                return;

            bool isSelectedChild = IsChildOfSelectedIssue(issue.key);
            if (!isSelectedChild)
                SelectIssue(issue);

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            _statusBusyIssues.Add(issue.key);
            SetResolveStatus(L.Tr(L.K.MsgUpdatingStatus, issue.key), true);

            try
            {
                string error = await client.ApplyTransitionAsync(issue.key, transition.id, null);
                if (error != null)
                {
                    SetResolveStatus(L.Tr(L.K.MsgResolveFailed, error), false);
                    return;
                }

                string target = string.IsNullOrWhiteSpace(transition.TargetStatus)
                    ? transition.name
                    : transition.TargetStatus;
                if (issue.fields == null)
                    issue.fields = new JiraListFields();
                if (issue.fields.status == null)
                    issue.fields.status = new JiraFullStatus();
                issue.fields.status.name = target;
                if (transition.to?.statusCategory != null)
                    issue.fields.status.statusCategory = transition.to.statusCategory;

                SetResolveStatus(L.Tr(L.K.MsgTransitionApplied, target), true);
                if (isSelectedChild)
                    RenderSelectedIssueSubtasks();
                else
                    LoadIssuesAsync();
            }
            catch (Exception exception)
            {
                SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
            finally
            {
                _statusBusyIssues.Remove(issue.key);
            }
        }

        private bool IsChildOfSelectedIssue(string issueKey)
        {
            if (_selectedIssue == null || string.IsNullOrWhiteSpace(issueKey))
                return false;

            foreach (JiraListIssue child in _resolveSelectedChildren)
            {
                if (child != null &&
                    string.Equals(
                        child.key,
                        issueKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SelectIssue(JiraListIssue issue)
        {
            SelectIssue(issue, false);
        }

        private void SelectIssue(JiraListIssue issue, bool keepParentContext)
        {
            if (issue == null || _isResolving)
                return;

            if (!keepParentContext)
            {
                _resolveParentIssue = null;
                _resolveParentStack.Clear();
            }

            _selectedIssue = issue;
            SetEmbeddedResolveChildren(issue);
            RefreshResolveChildrenUi(issue);
            int detailVersion = ++_issueDetailLoadVersion;
            _mentionSelected.Clear();
            RenderMentionChips();
            ClearResolveAttachment();
            ResetResolveSubtaskForm();
            _resolveCommentField.SetValueWithoutNotify(string.Empty);
            _mentionSearchField.SetValueWithoutNotify(string.Empty);
            _mentionResults.Clear();
            _resolveSummaryField.SetValueWithoutNotify(issue.Summary);
            _resolveDescriptionField.SetValueWithoutNotify(string.Empty);
            _resolveOriginalSummary = issue.Summary;
            _resolveOriginalDescription = string.Empty;
            _resolveParentTeamId = string.Empty;
            _resolveParentTeamFieldId = null;
            _resolveSummaryField.SetEnabled(false);
            _resolveDescriptionField.SetEnabled(false);
            _resolveEditPriorityDropdown?.SetEnabled(false);
            _resolveAddSubtaskButton?.SetEnabled(
                CanHaveDirectChildren(issue.fields?.issuetype));
            if (_resolveWeightContainer != null)
                _resolveWeightContainer.style.display = DisplayStyle.None;
            if (_resolveAddSubtaskForm != null)
                _resolveAddSubtaskForm.style.display = DisplayStyle.None;
            _resolveSaveChangesButton.SetEnabled(false);
            HideResolveStatus();
            RenderIssueList();
            RenderSelectedIssueSubtasks();
            UpdateParentNavigation();

            _resolveDetailHeader.text = $"{issue.key} — {issue.Summary}";
            _resolveDetailHeader.tooltip = issue.Summary;
            SetDetailInteractable(true);
            _gitTypeUserPicked = false;
            RefreshResolveGitCard();
            SetResolveStatus(L.Tr(L.K.MsgLoadingIssueEdit), true);
            LoadSelectedIssueForEditAsync(issue, detailVersion);
        }

        private void CloseSelectedIssue()
        {
            if (_isResolving)
                return;

            _selectedIssue = null;
            _resolveParentIssue = null;
            _resolveParentStack.Clear();
            _resolveSelectedChildren.Clear();
            _resolveAvailableChildTypes.Clear();
            _resolveParentTeamId = string.Empty;
            _resolveParentTeamFieldId = null;
            _issueDetailLoadVersion++;
            _mentionSearchVersion++;
            _mentionSelected.Clear();
            RenderMentionChips();
            ClearResolveAttachment();
            ResetResolveSubtaskForm();

            _resolveSummaryField?.SetValueWithoutNotify(string.Empty);
            _resolveDescriptionField?.SetValueWithoutNotify(string.Empty);
            _resolveOriginalSummary = string.Empty;
            _resolveOriginalDescription = string.Empty;
            _resolveOriginalPriorityId = string.Empty;
            _resolveOriginalWeight = string.Empty;
            _resolveWeightFieldId = null;
            _resolveParentTeamId = string.Empty;
            _resolveWeightField?.SetValueWithoutNotify(string.Empty);
            if (_resolveWeightContainer != null)
                _resolveWeightContainer.style.display = DisplayStyle.None;
            if (_resolveAddSubtaskForm != null)
                _resolveAddSubtaskForm.style.display = DisplayStyle.None;
            _resolveCommentField?.SetValueWithoutNotify(string.Empty);
            _mentionSearchField?.SetValueWithoutNotify(string.Empty);
            _mentionResults?.Clear();
            RenderSelectedIssueSubtasks();
            UpdateParentNavigation();

            if (_resolveDetailHeader != null)
            {
                _resolveDetailHeader.text = L.Tr(L.K.SelectIssueHint);
                _resolveDetailHeader.tooltip = string.Empty;
            }

            HideResolveStatus();
            CloseStatusPopup();
            SetDetailInteractable(false);
            RenderIssueList();
        }

        private async void LoadSelectedIssueForEditAsync(JiraListIssue issue, int detailVersion)
        {
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            try
            {
                await EnsureResolvePrioritiesAsync(client);

                JiraFieldMeta weightMeta = null;
                JiraFieldMeta teamMeta = null;
                string projectKey = ProjectKeyFromIssueKey(issue.key);
                string issueTypeId = issue.fields?.issuetype?.id;

                try
                {
                    List<JiraFieldMeta> editFields =
                        await client.GetEditFieldsAsync(issue.key);
                    weightMeta = FindActivityWeightField(editFields);
                    teamMeta = FindTeamField(editFields);
                }
                catch
                {
                    weightMeta = null;
                    teamMeta = null;
                }

                // Older Jira configurations may not expose edit metadata.
                // Keep create metadata as a compatibility fallback.
                if ((weightMeta == null || teamMeta == null) &&
                    !string.IsNullOrWhiteSpace(projectKey) &&
                    !string.IsNullOrWhiteSpace(issueTypeId))
                {
                    try
                    {
                        List<JiraFieldMeta> createFields =
                            await client.GetCreateFieldsAsync(
                                projectKey,
                                issueTypeId);
                        if (weightMeta == null)
                        {
                            weightMeta =
                                FindActivityWeightField(createFields);
                        }
                        if (teamMeta == null)
                        {
                            teamMeta =
                                FindTeamField(createFields);
                        }
                    }
                    catch
                    {
                        // Keep any metadata already returned by editmeta.
                    }
                }

                JiraIssueEditResponse response =
                    await client.GetIssueForEditAsync(
                        issue.key,
                        weightMeta?.fieldId,
                        teamMeta?.fieldId);
                if (detailVersion != _issueDetailLoadVersion ||
                    _selectedIssue == null ||
                    _selectedIssue.key != issue.key)
                {
                    return;
                }

                string summary = response?.fields?.summary ?? issue.Summary;
                string description = JiraAdf.ExtractPlainText(response?.fields?.description);
                _resolveSummaryField.SetValueWithoutNotify(summary);
                _resolveDescriptionField.SetValueWithoutNotify(description);
                _resolveOriginalSummary = summary;
                _resolveOriginalDescription = description;
                if (issue.fields == null)
                    issue.fields = new JiraListFields();
                if (response?.fields?.priority != null)
                    issue.fields.priority = response.fields.priority;
                if (response?.fields?.issuetype != null)
                    issue.fields.issuetype = response.fields.issuetype;
                if (response?.fields?.subtasks != null)
                    issue.fields.subtasks = response.fields.subtasks;
                _resolveOriginalPriorityId = issue.PriorityId;
                RefreshResolvePriorityFields(issue);
                RefreshResolveChildrenUi(issue);

                _resolveWeightFieldId = weightMeta?.fieldId;
                _resolveOriginalWeight = response?.weightValue ?? string.Empty;
                _resolveWeightField.SetValueWithoutNotify(
                    _resolveOriginalWeight);
                _resolveWeightContainer.style.display =
                    string.IsNullOrWhiteSpace(_resolveWeightFieldId)
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                _resolveParentTeamId =
                    response?.teamValue ?? string.Empty;
                _resolveParentTeamFieldId = teamMeta?.fieldId;

                bool canHaveChildren =
                    CanHaveDirectChildren(issue.fields.issuetype);
                _resolveAddSubtaskButton?.SetEnabled(canHaveChildren);
                if (!canHaveChildren && _resolveAddSubtaskForm != null)
                {
                    _resolveAddSubtaskForm.style.display =
                        DisplayStyle.None;
                }
                await LoadSelectedIssueChildrenAsync(
                    client,
                    issue,
                    detailVersion);
                if (detailVersion != _issueDetailLoadVersion ||
                    _selectedIssue == null ||
                    _selectedIssue.key != issue.key)
                {
                    return;
                }
                _resolveSummaryField.SetEnabled(true);
                _resolveDescriptionField.SetEnabled(true);
                _resolveEditPriorityDropdown.SetEnabled(
                    _resolvePriorities.Count > 0);
                _resolveSaveChangesButton.SetEnabled(true);
                HideResolveStatus();
            }
            catch (Exception exception)
            {
                if (detailVersion != _issueDetailLoadVersion ||
                    _selectedIssue == null ||
                    _selectedIssue.key != issue.key)
                {
                    return;
                }

                SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
        }

        private void RefreshResolveChildrenUi(JiraListIssue issue)
        {
            bool childActivities =
                IsHigherLevelIssueType(issue?.fields?.issuetype);
            if (_resolveChildrenTitle != null)
            {
                _resolveChildrenTitle.text = L.Tr(
                    childActivities
                        ? L.K.ResolveChildActivitiesTitle
                        : L.K.ResolveSubtasksTitle);
            }
            if (_resolveAddSubtaskButton != null)
            {
                _resolveAddSubtaskButton.text = L.Tr(
                    childActivities
                        ? L.K.BtnAddChildActivity
                        : L.K.BtnAddQuickSubtask);
            }
            if (_resolveNewSubtaskTitle != null)
            {
                _resolveNewSubtaskTitle.label = RequiredLabel(
                    L.Tr(
                        childActivities
                            ? L.K.FieldChildActivityTitle
                            : L.K.FieldQuickSubtaskTitle));
            }
            if (_resolveNewSubtaskDescription != null)
            {
                _resolveNewSubtaskDescription.label = L.Tr(
                    childActivities
                        ? L.K.FieldChildActivityDescription
                        : L.K.FieldQuickSubtaskDescription);
            }
            if (_resolveNewChildAttachmentTitle != null)
            {
                _resolveNewChildAttachmentTitle.text = L.Tr(
                    childActivities
                        ? L.K.FieldChildActivityAttachment
                        : L.K.FieldSubtaskAttachment);
            }
            if (_resolveCreateSubtaskButton != null)
            {
                _resolveCreateSubtaskButton.text = L.Tr(
                    childActivities
                        ? L.K.BtnCreateChildActivity
                        : L.K.BtnCreateSubtask);
            }
            if (!childActivities &&
                _resolveNewChildTypeDropdown != null)
            {
                _resolveNewChildTypeDropdown.style.display =
                    DisplayStyle.None;
            }

            RenderSelectedIssueSubtasks();
        }

        private async Task LoadSelectedIssueChildrenAsync(
            JiraClient client,
            JiraListIssue issue,
            int detailVersion)
        {
            if (client == null || issue == null ||
                !CanHaveDirectChildren(issue.fields?.issuetype))
            {
                _resolveSelectedChildren.Clear();
                RenderSelectedIssueSubtasks();
                return;
            }

            if (_resolveSubtasksList != null)
                _resolveSubtasksList.Clear();
            if (_resolveSubtasksCount != null)
                _resolveSubtasksCount.text = string.Empty;
            if (_resolveSubtasksStatus != null)
            {
                _resolveSubtasksStatus.text = L.Tr(L.K.StatusLoading);
                _resolveSubtasksStatus.style.display = DisplayStyle.Flex;
            }

            try
            {
                List<JiraListIssue> children =
                    await client.GetDirectChildIssuesAsync(issue.key);
                if (detailVersion != _issueDetailLoadVersion ||
                    _selectedIssue == null ||
                    !string.Equals(
                        _selectedIssue.key,
                        issue.key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _resolveSelectedChildren.Clear();
                foreach (JiraListIssue child in children)
                {
                    if (child != null &&
                        !string.IsNullOrWhiteSpace(child.key))
                    {
                        _resolveSelectedChildren.Add(child);
                    }
                }
            }
            catch
            {
                // Keep the embedded subtasks as a compatibility fallback.
            }

            RenderSelectedIssueSubtasks();
        }

        private void SetEmbeddedResolveChildren(JiraListIssue issue)
        {
            _resolveSelectedChildren.Clear();
            if (issue == null)
                return;

            foreach (JiraSubtask subtask in issue.Subtasks)
            {
                if (subtask != null &&
                    !string.IsNullOrWhiteSpace(subtask.key))
                {
                    _resolveSelectedChildren.Add(
                        BuildSubtaskTransitionIssue(subtask));
                }
            }
        }

        private void RenderSelectedIssueSubtasks()
        {
            if (_resolveSubtasksList == null ||
                _resolveSubtasksStatus == null ||
                _resolveSubtasksCount == null)
            {
                return;
            }

            _resolveSubtasksList.Clear();
            _resolveSubtasksCount.text = _resolveSelectedChildren.Count > 0
                ? L.Tr(
                    L.K.ResolveSubtaskCount,
                    _resolveSelectedChildren.Count)
                : string.Empty;

            if (_resolveSelectedChildren.Count == 0)
            {
                _resolveSubtasksStatus.text =
                    IsHigherLevelIssueType(
                        _selectedIssue?.fields?.issuetype)
                        ? L.Tr(L.K.ResolveNoChildActivities)
                        : L.Tr(L.K.ResolveNoSubtasks);
                _resolveSubtasksStatus.style.display = DisplayStyle.Flex;
                return;
            }

            _resolveSubtasksStatus.style.display = DisplayStyle.None;
            foreach (JiraListIssue child in _resolveSelectedChildren)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.key))
                    continue;

                _resolveSubtasksList.Add(BuildResolveChildRow(child));
            }
        }

        private async void ToggleResolveAddSubtaskForm()
        {
            if (_resolveAddSubtaskForm == null || _selectedIssue == null)
                return;

            JiraIssueType issueType = _selectedIssue.fields?.issuetype;
            if (!CanHaveDirectChildren(issueType))
            {
                SetResolveStatus(
                    L.Tr(L.K.MsgIssueCannotHaveChildren),
                    false);
                return;
            }

            bool opening =
                _resolveAddSubtaskForm.resolvedStyle.display ==
                DisplayStyle.None;
            _resolveAddSubtaskForm.style.display = opening
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (opening)
            {
                await PrepareResolveSubtaskFieldsAsync();
                _resolveNewSubtaskTitle?.Focus();
            }
        }

        private async void OnResolveChildTypeChanged()
        {
            int index = _resolveNewChildTypeDropdown?.index ?? -1;
            if (_resolveSubtaskFieldsLoading ||
                index < 0 ||
                index >= _resolveAvailableChildTypes.Count)
            {
                return;
            }

            await PrepareResolveSubtaskFieldsAsync(
                _resolveAvailableChildTypes[index].id);
        }

        private async Task PrepareResolveSubtaskFieldsAsync(
            string preferredTypeId = null)
        {
            if (_selectedIssue == null || _resolveSubtaskFieldsLoading)
                return;

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            string parentKey = _selectedIssue.key;
            string projectKey = ProjectKeyFromIssueKey(parentKey);
            if (string.IsNullOrWhiteSpace(projectKey))
                return;

            int loadVersion = ++_resolveSubtaskFieldLoadVersion;
            _resolveSubtaskFieldsLoading = true;
            _resolveCreateSubtaskButton?.SetEnabled(false);
            _resolveNewSubtaskType = null;
            _resolveNewSubtaskDescriptionMeta = null;
            _resolveNewSubtaskPriorityMeta = null;
            _resolveNewSubtaskTeamMeta = null;
            _resolveNewSubtaskAssigneeMeta = null;
            _resolveNewSubtaskStartDateMeta = null;
            _resolveNewSubtaskDueDateMeta = null;
            SetResolveStatus(L.Tr(L.K.MsgLoadingFields), true);
            try
            {
                List<JiraIssueType> issueTypes =
                    await client.GetIssueTypesAsync(projectKey);
                JiraIssueType parentType =
                    _selectedIssue.fields?.issuetype;
                List<JiraIssueType> childTypes =
                    FindDirectChildTypes(parentType, issueTypes);
                if (childTypes.Count == 0)
                {
                    SetResolveStatus(
                        IsHigherLevelIssueType(parentType)
                            ? L.Tr(L.K.MsgChildTypeUnavailable)
                            : L.Tr(L.K.MsgSubtaskTypeUnavailable),
                        false);
                    return;
                }

                if (loadVersion != _resolveSubtaskFieldLoadVersion ||
                    _selectedIssue == null ||
                    !string.Equals(
                        _selectedIssue.key,
                        parentKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _resolveAvailableChildTypes.Clear();
                _resolveAvailableChildTypes.AddRange(childTypes);
                int childTypeIndex =
                    FindPreferredChildTypeIndex(
                        childTypes,
                        preferredTypeId);
                JiraIssueType childType =
                    childTypes[Mathf.Clamp(
                        childTypeIndex,
                        0,
                        childTypes.Count - 1)];
                bool isHigherLevelParent =
                    IsHigherLevelIssueType(parentType);
                _resolveNewChildTypeDropdown.choices =
                    childTypes.ConvertAll(type => type.name);
                _resolveNewChildTypeDropdown.SetValueWithoutNotify(
                    childType.name);
                _resolveNewChildTypeDropdown.style.display =
                    isHigherLevelParent
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                // The child type is enough to create the basic Jira fields.
                // Optional metadata enriches the form, but a metadata failure
                // must not prevent a title-only child from being created.
                _resolveNewSubtaskType = childType;
                List<JiraFieldMeta> fields;
                try
                {
                    fields = await client.GetCreateFieldsAsync(
                        projectKey,
                        childType.id);
                }
                catch
                {
                    fields = new List<JiraFieldMeta>();
                }

                List<JiraUser> users;
                try
                {
                    users = await client.GetAssignableUsersAsync(projectKey);
                }
                catch
                {
                    users = new List<JiraUser>();
                }

                if (loadVersion != _resolveSubtaskFieldLoadVersion ||
                    _selectedIssue == null ||
                    !string.Equals(
                        _selectedIssue.key,
                        parentKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _resolveNewSubtaskPriorityMeta =
                    FindById(fields, FieldPriority);
                _resolveNewSubtaskTeamMeta =
                    FindById(
                        fields,
                        _resolveParentTeamFieldId) ??
                    FindTeamField(fields);
                _resolveNewSubtaskDescriptionMeta =
                    FindById(fields, "description");
                _resolveNewSubtaskAssigneeMeta =
                    FindById(fields, FieldAssignee);
                _resolveNewSubtaskStartDateMeta =
                    FindStartDate(fields);
                _resolveNewSubtaskDueDateMeta =
                    FindById(fields, FieldDueDate);

                await EnsureResolvePrioritiesAsync(client);
                if (loadVersion != _resolveSubtaskFieldLoadVersion ||
                    _selectedIssue == null ||
                    !string.Equals(
                        _selectedIssue.key,
                        parentKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                RefreshResolvePriorityFields(_selectedIssue);
                _resolveNewSubtaskDescription.label = FieldLabel(
                    _resolveNewSubtaskDescriptionMeta,
                    IsHigherLevelIssueType(
                        _selectedIssue.fields?.issuetype)
                        ? L.Tr(L.K.FieldChildActivityDescription)
                        : L.Tr(L.K.FieldQuickSubtaskDescription));
                _resolveNewSubtaskPriority.label = FieldLabel(
                    _resolveNewSubtaskPriorityMeta,
                    L.Tr(L.K.FieldQuickSubtaskPriority));
                _resolveNewSubtaskPriority.style.display =
                    _resolveNewSubtaskPriorityMeta != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                if (_resolveNewSubtaskTeamMeta != null)
                {
                    string teamLabel = FieldLabel(
                        _resolveNewSubtaskTeamMeta,
                        L.Tr(L.K.FieldTeam));
                    _resolveNewSubtaskTeam.label = teamLabel;
                    _resolveNewSubtaskTeamText.label = teamLabel;
                    if (_resolveNewSubtaskTeamMeta.HasAllowedValues)
                    {
                        PopulateTeamDropdown(
                            _resolveNewSubtaskTeam,
                            _resolveNewSubtaskTeamMeta,
                            _resolveParentTeamId);
                        _resolveNewSubtaskTeam.style.display =
                            DisplayStyle.Flex;
                        _resolveNewSubtaskTeamText.style.display =
                            DisplayStyle.None;
                    }
                    else
                    {
                        _resolveNewSubtaskTeamText.SetValueWithoutNotify(
                            _resolveParentTeamId ?? string.Empty);
                        _resolveNewSubtaskTeam.style.display =
                            DisplayStyle.None;
                        _resolveNewSubtaskTeamText.style.display =
                            DisplayStyle.Flex;
                    }
                    _resolveNewSubtaskTeamContainer.style.display =
                        DisplayStyle.Flex;
                }
                else
                {
                    _resolveNewSubtaskTeamContainer.style.display =
                        DisplayStyle.None;
                }

                if (_myself != null &&
                    !users.Exists(user =>
                        user != null &&
                        string.Equals(
                            user.accountId,
                            _myself.accountId,
                            StringComparison.Ordinal)))
                {
                    users.Insert(0, _myself);
                }
                _resolveNewSubtaskAssignee.label = FieldLabel(
                    _resolveNewSubtaskAssigneeMeta,
                    L.Tr(L.K.FieldAssignee));
                PopulateAssigneeDropdown(
                    _resolveNewSubtaskAssignee,
                    users,
                    _selectedIssue.fields?.assignee?.accountId);
                _resolveNewSubtaskAssigneeContainer.style.display =
                    _resolveNewSubtaskAssigneeMeta != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                _resolveNewSubtaskStartDate.label = FieldLabel(
                    _resolveNewSubtaskStartDateMeta,
                    L.Tr(L.K.FieldStartDate));
                _resolveNewSubtaskStartDate.style.display =
                    _resolveNewSubtaskStartDateMeta != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                _resolveNewSubtaskDueDate.label = FieldLabel(
                    _resolveNewSubtaskDueDateMeta,
                    L.Tr(L.K.FieldDueDate));
                _resolveNewSubtaskDueDate.style.display =
                    _resolveNewSubtaskDueDateMeta != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                _resolveNewSubtaskDatesContainer.style.display =
                    _resolveNewSubtaskStartDateMeta != null ||
                    _resolveNewSubtaskDueDateMeta != null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

                HideResolveStatus();
            }
            catch (Exception exception)
            {
                SetResolveStatus(
                    L.Tr(L.K.MsgResolveFailed, exception.Message),
                    false);
            }
            finally
            {
                if (loadVersion == _resolveSubtaskFieldLoadVersion)
                {
                    _resolveSubtaskFieldsLoading = false;
                    bool ready =
                        _selectedIssue != null &&
                        string.Equals(
                            _selectedIssue.key,
                            parentKey,
                            StringComparison.OrdinalIgnoreCase) &&
                        _resolveNewSubtaskType != null;
                    _resolveCreateSubtaskButton?.SetEnabled(ready);
                }
            }
        }

        private async void CreateResolveSubtaskAsync()
        {
            if (_isResolving || _resolveSubtaskFieldsLoading ||
                _selectedIssue == null)
                return;

            string title = _resolveNewSubtaskTitle?.value?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                SetResolveStatus(
                    L.Tr(L.K.MsgQuickSubtaskTitleRequired),
                    false);
                return;
            }
            string description =
                _resolveNewSubtaskDescription?.value?.Trim();
            if (_resolveNewSubtaskDescriptionMeta?.required == true &&
                string.IsNullOrWhiteSpace(description))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskDescriptionMeta.name),
                    false);
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            JiraListIssue parent = _selectedIssue;
            bool creatingChildActivity =
                IsHigherLevelIssueType(parent.fields?.issuetype);
            string projectKey = ProjectKeyFromIssueKey(parent.key);
            if (string.IsNullOrWhiteSpace(projectKey))
            {
                SetResolveStatus(
                    L.Tr(L.K.MsgResolveProjectUnknown),
                    false);
                return;
            }

            string assigneeAccountId =
                SelectedDropdownAssigneeAccountId(
                    _resolveNewSubtaskAssignee);
            string teamId = SelectedTeamId(
                _resolveNewSubtaskTeamMeta,
                _resolveNewSubtaskTeam,
                _resolveNewSubtaskTeamText);
            string startDate =
                _resolveNewSubtaskStartDate?.value?.Trim();
            string dueDate =
                _resolveNewSubtaskDueDate?.value?.Trim();
            int priorityIndex =
                _resolveNewSubtaskPriority?.index ?? -1;
            string priorityId =
                priorityIndex >= 0 &&
                priorityIndex < _resolvePriorities.Count
                    ? _resolvePriorities[priorityIndex].id
                    : null;
            if (_resolveNewSubtaskPriorityMeta?.required == true &&
                string.IsNullOrWhiteSpace(priorityId))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskPriorityMeta.name),
                    false);
                return;
            }
            if (_resolveNewSubtaskAssigneeMeta?.required == true &&
                string.IsNullOrWhiteSpace(assigneeAccountId))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskAssigneeMeta.name),
                    false);
                return;
            }
            if (_resolveNewSubtaskTeamMeta?.required == true &&
                string.IsNullOrWhiteSpace(teamId))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskTeamMeta.name),
                    false);
                return;
            }
            if (_resolveNewSubtaskStartDateMeta?.required == true &&
                string.IsNullOrWhiteSpace(startDate))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskStartDateMeta.name),
                    false);
                return;
            }
            if (_resolveNewSubtaskDueDateMeta?.required == true &&
                string.IsNullOrWhiteSpace(dueDate))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgRequiredField,
                        _resolveNewSubtaskDueDateMeta.name),
                    false);
                return;
            }
            if (!TryNormalizeDateInput(startDate, out startDate))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgInvalidDate,
                        _resolveNewSubtaskStartDateMeta?.name ??
                        L.Tr(L.K.FieldStartDate)),
                    false);
                return;
            }
            if (!TryNormalizeDateInput(dueDate, out dueDate))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgInvalidDate,
                        _resolveNewSubtaskDueDateMeta?.name ??
                        L.Tr(L.K.FieldDueDate)),
                    false);
                return;
            }

            SetResolveBusy(true);
            _resolveCreateSubtaskButton?.SetEnabled(false);
            SetResolveStatus(
                L.Tr(
                    creatingChildActivity
                        ? L.K.MsgCreatingChildActivity
                        : L.K.MsgCreatingSubtask),
                true);
            try
            {
                JiraIssueType childType = _resolveNewSubtaskType;
                if (childType == null)
                {
                    List<JiraIssueType> issueTypes =
                        await client.GetIssueTypesAsync(projectKey);
                    List<JiraIssueType> childTypes =
                        FindDirectChildTypes(
                            parent.fields?.issuetype,
                            issueTypes);
                    if (childTypes.Count > 0)
                    {
                        childType = childTypes[
                            FindPreferredChildTypeIndex(
                                childTypes,
                                null)];
                    }
                }
                if (childType == null)
                {
                    SetResolveStatus(
                        L.Tr(
                            creatingChildActivity
                                ? L.K.MsgChildTypeUnavailable
                                : L.K.MsgSubtaskTypeUnavailable),
                        false);
                    return;
                }

                var draft = new JiraIssueDraft
                {
                    ProjectKey = projectKey,
                    IssueTypeId = childType.id,
                    ParentKey = parent.key,
                    Summary = title,
                    Description = description
                };
                if (!string.IsNullOrWhiteSpace(priorityId) &&
                    _resolveNewSubtaskPriorityMeta != null)
                {
                    draft.SetFieldId(
                        _resolveNewSubtaskPriorityMeta.fieldId,
                        priorityId);
                }
                ApplyTeamField(
                    draft,
                    _resolveNewSubtaskTeamMeta,
                    teamId);
                if (_resolveNewSubtaskAssigneeMeta != null)
                {
                    draft.SetFieldObject(
                        _resolveNewSubtaskAssigneeMeta.fieldId,
                        "accountId",
                        assigneeAccountId);
                }
                if (_resolveNewSubtaskStartDateMeta != null)
                {
                    draft.SetFieldString(
                        _resolveNewSubtaskStartDateMeta.fieldId,
                        startDate);
                }
                if (_resolveNewSubtaskDueDateMeta != null)
                {
                    draft.SetFieldString(
                        _resolveNewSubtaskDueDateMeta.fieldId,
                        dueDate);
                }

                JiraCreateIssueResult result =
                    await client.CreateIssueAsync(draft);
                if (!result.Success)
                {
                    SetResolveStatus(result.Message, false);
                    return;
                }

                string message = L.Tr(
                    creatingChildActivity
                        ? L.K.MsgChildActivityCreated
                        : L.K.MsgSubtaskCreated,
                    result.IssueKey);
                if (!string.IsNullOrWhiteSpace(
                        _resolveNewSubtaskAttachmentPath))
                {
                    message +=
                        await UploadAttachmentAndEmbedImageAsync(
                            client,
                            result.IssueKey,
                            _resolveNewSubtaskAttachmentPath,
                            description);
                }

                _resolveNewSubtaskTitle.SetValueWithoutNotify(string.Empty);
                _resolveNewSubtaskDescription.SetValueWithoutNotify(
                    string.Empty);
                _resolveNewSubtaskStartDate?.SetValueWithoutNotify(
                    string.Empty);
                _resolveNewSubtaskDueDate?.SetValueWithoutNotify(
                    string.Empty);
                ClearResolveSubtaskAttachment();
                _resolveAddSubtaskForm.style.display = DisplayStyle.None;
                SetResolveStatus(message, true);

                int detailVersion = ++_issueDetailLoadVersion;
                LoadSelectedIssueForEditAsync(parent, detailVersion);
            }
            catch (Exception exception)
            {
                SetResolveStatus(
                    L.Tr(L.K.MsgResolveFailed, exception.Message),
                    false);
            }
            finally
            {
                SetResolveBusy(false);
                _resolveCreateSubtaskButton?.SetEnabled(true);
            }
        }

        private void RefreshResolvePriorityFields(JiraListIssue issue)
        {
            var labels = new List<string>();
            int selectedIndex = 0;
            for (int i = 0; i < _resolvePriorities.Count; i++)
            {
                JiraAllowedValue priority = _resolvePriorities[i];
                labels.Add(priority.Display);
                if (issue != null &&
                    string.Equals(
                        priority.id,
                        issue.PriorityId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (_resolveEditPriorityDropdown != null)
            {
                _resolveEditPriorityDropdown.choices = labels;
                if (labels.Count > 0)
                {
                    _resolveEditPriorityDropdown.SetValueWithoutNotify(
                        labels[Mathf.Clamp(
                            selectedIndex,
                            0,
                            labels.Count - 1)]);
                }
                RefreshPriorityDropdownIcon(_resolveEditPriorityDropdown);
                _resolveEditPriorityDropdown.SetEnabled(labels.Count > 0);
            }

            if (_resolveNewSubtaskPriority != null)
            {
                _resolveNewSubtaskPriority.choices = labels;
                if (labels.Count > 0)
                {
                    _resolveNewSubtaskPriority.SetValueWithoutNotify(
                        labels[Mathf.Clamp(
                            selectedIndex,
                            0,
                            labels.Count - 1)]);
                }
                RefreshPriorityDropdownIcon(_resolveNewSubtaskPriority);
                _resolveNewSubtaskPriority.SetEnabled(labels.Count > 0);
            }
        }

        private async Task EnsureResolvePrioritiesAsync(JiraClient client)
        {
            if (_resolvePrioritiesLoaded)
                return;

            _resolvePrioritiesLoading = true;
            try
            {
                List<JiraAllowedValue> priorities =
                    await client.GetPrioritiesAsync();
                _resolvePriorities.Clear();
                _resolvePriorities.AddRange(priorities);
                _resolvePrioritiesLoaded = true;
            }
            finally
            {
                _resolvePrioritiesLoading = false;
            }
        }

        private VisualElement BuildResolveChildRow(JiraListIssue child)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 38;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor =
                new StyleColor(new Color32(58, 63, 73, 255));

            var key = new Label(child.key);
            key.style.width = 82;
            key.style.flexShrink = 0;
            key.style.fontSize = 10;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.color = new StyleColor(new Color32(110, 177, 255, 255));
            row.Add(key);

            var title = new Button(() => OpenChildAsIssue(child))
            {
                text = $"{child.Summary}  ›"
            };
            JiraStyles.ApplyGhostButton(title);
            title.style.flexGrow = 1;
            title.style.flexShrink = 1;
            title.style.minWidth = 0;
            title.style.marginRight = 8;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.color = new StyleColor(new Color32(220, 224, 232, 255));
            title.tooltip = L.Tr(
                L.K.OpenSubtaskTooltip,
                child.key,
                child.Summary);
            row.Add(title);

            var status = new Button
            {
                text = $"{child.StatusName}  ▾"
            };
            status.clicked += () =>
                ShowIssueStatusMenuAsync(child, status);
            status.tooltip = L.Tr(L.K.StatusDropdownTooltip, child.key);
            JiraStyles.ApplyGhostButton(status);
            status.style.width = 164;
            status.style.minWidth = 164;
            status.style.flexShrink = 0;
            status.style.fontSize = 10;
            status.style.color = StatusColor(child.StatusCategory);
            row.Add(status);

            return row;
        }

        private void OpenChildAsIssue(JiraListIssue child)
        {
            if (child == null ||
                string.IsNullOrWhiteSpace(child.key) ||
                _selectedIssue == null ||
                _isResolving)
            {
                return;
            }

            JiraListIssue parent = _selectedIssue;
            _resolveParentStack.Add(parent);
            _resolveParentIssue = parent;
            SelectIssue(child, true);
        }

        private void ReturnToParentIssue()
        {
            if (_resolveParentIssue == null || _isResolving)
                return;

            JiraListIssue parent = _resolveParentIssue;
            if (_resolveParentStack.Count > 0)
            {
                _resolveParentStack.RemoveAt(
                    _resolveParentStack.Count - 1);
            }
            _resolveParentIssue = _resolveParentStack.Count > 0
                ? _resolveParentStack[
                    _resolveParentStack.Count - 1]
                : null;
            SelectIssue(parent, true);
        }

        private void UpdateParentNavigation()
        {
            if (_resolveParentButton == null)
                return;

            bool hasParent = _resolveParentIssue != null &&
                             !string.IsNullOrWhiteSpace(_resolveParentIssue.key);
            _resolveParentButton.style.display = hasParent
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (hasParent)
            {
                _resolveParentButton.text =
                    L.Tr(L.K.BtnBackToParent, _resolveParentIssue.key);
                _resolveParentButton.tooltip =
                    L.Tr(L.K.BackToParentTooltip, _resolveParentIssue.Summary);
            }
            else
            {
                _resolveParentButton.text = string.Empty;
                _resolveParentButton.tooltip = string.Empty;
            }
        }

        private static JiraListIssue BuildSubtaskTransitionIssue(JiraSubtask subtask)
        {
            if (subtask.fields == null)
                subtask.fields = new JiraSubtaskFields();
            if (subtask.fields.status == null)
                subtask.fields.status = new JiraFullStatus();

            return new JiraListIssue
            {
                id = subtask.id,
                key = subtask.key,
                fields = new JiraListFields
                {
                    summary = subtask.fields.summary,
                    status = subtask.fields.status,
                    priority = subtask.fields.priority,
                    issuetype = subtask.fields.issuetype
                }
            };
        }

        private async void SaveIssueChangesAsync()
        {
            if (_isResolving || _selectedIssue == null)
                return;

            string summary = _resolveSummaryField.value?.Trim();
            if (string.IsNullOrWhiteSpace(summary))
            {
                SetResolveStatus(L.Tr(L.K.MsgIssueSummaryRequired), false);
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            JiraListIssue issue = _selectedIssue;
            string description = _resolveDescriptionField.value ?? string.Empty;
            bool summaryChanged = !string.Equals(
                summary,
                _resolveOriginalSummary,
                StringComparison.Ordinal);
            bool descriptionChanged = !string.Equals(
                description,
                _resolveOriginalDescription,
                StringComparison.Ordinal);
            JiraAllowedValue selectedPriority =
                _resolveEditPriorityDropdown != null &&
                _resolveEditPriorityDropdown.index >= 0 &&
                _resolveEditPriorityDropdown.index < _resolvePriorities.Count
                    ? _resolvePriorities[
                        _resolveEditPriorityDropdown.index]
                    : null;
            bool priorityChanged =
                selectedPriority != null &&
                !string.Equals(
                    selectedPriority.id,
                    _resolveOriginalPriorityId,
                    StringComparison.OrdinalIgnoreCase);

            string weight = _resolveWeightField?.value?.Trim() ??
                            string.Empty;
            if (!TryNormalizeNumber(weight, out string normalizedWeight))
            {
                SetResolveStatus(
                    L.Tr(
                        L.K.MsgInvalidNumber,
                        L.Tr(L.K.FieldActivityWeight)),
                    false);
                return;
            }
            bool weightChanged =
                !string.IsNullOrWhiteSpace(_resolveWeightFieldId) &&
                !string.Equals(
                    normalizedWeight,
                    _resolveOriginalWeight,
                    StringComparison.Ordinal);

            if (!summaryChanged &&
                !descriptionChanged &&
                !priorityChanged &&
                !weightChanged)
            {
                SetResolveStatus(L.Tr(L.K.MsgNoIssueChanges), true);
                return;
            }

            SetResolveBusy(true);
            _resolveSaveChangesButton.SetEnabled(false);
            SetResolveStatus(L.Tr(L.K.MsgSavingIssueEdit), true);

            try
            {
                string error = null;
                if (summaryChanged || descriptionChanged)
                {
                    error = await client.UpdateIssueAsync(
                        issue.key,
                        summary,
                        description,
                        summaryChanged,
                        descriptionChanged);
                }
                if (error != null)
                {
                    SetResolveStatus(L.Tr(L.K.MsgResolveFailed, error), false);
                    return;
                }

                if (priorityChanged)
                {
                    error = await client.UpdateIssuePriorityAsync(
                        issue.key,
                        selectedPriority.id);
                    if (error != null)
                    {
                        SetResolveStatus(
                            L.Tr(L.K.MsgResolveFailed, error),
                            false);
                        return;
                    }
                }

                if (weightChanged)
                {
                    error = await client.UpdateIssueNumberAsync(
                        issue.key,
                        _resolveWeightFieldId,
                        normalizedWeight);
                    if (error != null)
                    {
                        SetResolveStatus(
                            L.Tr(L.K.MsgResolveFailed, error),
                            false);
                        return;
                    }
                }

                if (issue.fields == null)
                    issue.fields = new JiraListFields();
                issue.fields.summary = summary;
                if (priorityChanged)
                {
                    if (issue.fields.priority == null)
                        issue.fields.priority = new JiraListPriority();
                    issue.fields.priority.id = selectedPriority.id;
                    issue.fields.priority.name = selectedPriority.Display;
                }
                _resolveOriginalSummary = summary;
                _resolveOriginalDescription = description;
                _resolveOriginalPriorityId =
                    selectedPriority?.id ?? _resolveOriginalPriorityId;
                _resolveOriginalWeight = normalizedWeight;
                _resolveDetailHeader.text = $"{issue.key} — {summary}";
                _resolveDetailHeader.tooltip = summary;
                UpdateGitPreviews();
                RenderIssueList();
                SetResolveStatus(L.Tr(L.K.MsgIssueEditSaved), true);
            }
            catch (Exception exception)
            {
                SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
            finally
            {
                SetResolveBusy(false);
                if (_selectedIssue != null && _selectedIssue.key == issue.key)
                    _resolveSaveChangesButton.SetEnabled(true);
            }
        }

        private void OpenSelectedIssue()
        {
            JiraClient client = BuildClientOrNull();
            if (client == null || _selectedIssue == null)
                return;
            Application.OpenURL($"{client.BaseUrl}/browse/{_selectedIssue.key}");
        }

        // --- Mentions ------------------------------------------------------

        private async void OnMentionSearchChanged(string query)
        {
            _mentionResults.Clear();
            string trimmed = query?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 2)
                return;

            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            int version = ++_mentionSearchVersion;
            await Task.Delay(250);
            if (version != _mentionSearchVersion)
                return;

            List<JiraUser> users;
            try
            {
                users = await SearchResolveAssignableUsersAsync(
                    client,
                    trimmed);
            }
            catch { return; }

            if (version != _mentionSearchVersion)
                return;

            RenderMentionResults(users);
        }

        private void RenderMentionResults(List<JiraUser> users)
        {
            _mentionResults.Clear();
            int shown = 0;

            foreach (JiraUser user in users)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.accountId))
                    continue;
                if (_mentionSelected.Exists(u => u.accountId == user.accountId))
                    continue;

                var button = new Button(() => AddMention(user)) { text = AssigneeDisplay(user) };
                JiraStyles.ApplyGhostButton(button);
                button.style.marginBottom = 3;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                _mentionResults.Add(button);

                if (++shown >= 6)
                    break;
            }
        }

        private void AddMention(JiraUser user)
        {
            if (!_mentionSelected.Exists(u => u.accountId == user.accountId))
                _mentionSelected.Add(user);

            _mentionSearchField.SetValueWithoutNotify(string.Empty);
            _mentionResults.Clear();
            RenderMentionChips();
        }

        private void RenderMentionChips()
        {
            _mentionChips.Clear();
            foreach (JiraUser user in _mentionSelected)
            {
                JiraUser captured = user;
                var chip = new Button(() =>
                {
                    _mentionSelected.RemoveAll(u => u.accountId == captured.accountId);
                    RenderMentionChips();
                })
                { text = $"@{user.displayName}  ✕" };
                JiraStyles.ApplyGhostButton(chip);
                chip.style.marginRight = 4;
                chip.style.marginBottom = 4;
                chip.style.color = new StyleColor(new Color32(38, 132, 255, 255));
                _mentionChips.Add(chip);
            }
        }

        private void SelectResolveAttachment()
        {
            string path = EditorUtility.OpenFilePanel(L.Tr(L.K.BtnSelectFile), string.Empty, string.Empty);
            if (string.IsNullOrEmpty(path))
                return;

            _resolveAttachmentPath = path;
            _resolveAttachmentLabel.text = Path.GetFileName(path);
        }

        private void ClearResolveAttachment()
        {
            _resolveAttachmentPath = string.Empty;
            if (_resolveAttachmentLabel != null)
                _resolveAttachmentLabel.text = L.Tr(L.K.AttachFixHint);
        }

        private async void UpdateActivityAsync()
        {
            if (_isResolving || _selectedIssue == null)
                return;

            string comment = _resolveCommentField.value?.Trim();
            bool hasComment = !string.IsNullOrWhiteSpace(comment) || _mentionSelected.Count > 0;
            bool hasAttachment = !string.IsNullOrWhiteSpace(_resolveAttachmentPath);
            if (!hasComment && !hasAttachment)
            {
                SetResolveStatus(L.Tr(L.K.MsgActivityRequired), false);
                return;
            }

            JiraClient client = BuildClientOrNull();
            if (client == null)
            {
                SetResolveStatus(L.Tr(L.K.MsgNoCredentials), false);
                return;
            }

            SetResolveBusy(true);
            SetResolveStatus(L.Tr(L.K.MsgResolving), true);

            try
            {
                if (hasComment)
                {
                    string adf = JiraAdf.BuildCommentBody(_resolveCommentField.value, _mentionSelected);
                    string error = await client.AddCommentAsync(_selectedIssue.key, adf);
                    if (error != null)
                    {
                        SetResolveStatus(L.Tr(L.K.MsgResolveFailed, error), false);
                        return;
                    }
                }

                string message = L.Tr(L.K.MsgActivityUpdated);
                message += await UploadResolveAttachment(client);

                SetResolveStatus(message, true);
                _resolveCommentField.SetValueWithoutNotify(string.Empty);
                _mentionSelected.Clear();
                RenderMentionChips();
                ClearResolveAttachment();
            }
            catch (Exception exception)
            {
                SetResolveStatus(L.Tr(L.K.MsgResolveFailed, exception.Message), false);
            }
            finally
            {
                SetResolveBusy(false);
            }
        }

        private async Task<string> UploadResolveAttachment(JiraClient client)
        {
            if (string.IsNullOrEmpty(_resolveAttachmentPath))
                return string.Empty;

            return await UploadAttachmentAndEmbedImageAsync(
                client,
                _selectedIssue.key,
                _resolveAttachmentPath,
                _resolveDescriptionField?.value);
        }

        private void SetResolveBusy(bool busy)
        {
            _isResolving = busy;
            _resolveCloseButton?.SetEnabled(!busy);
        }

        private void SetResolveStatus(string message, bool success)
        {
            _resolveStatus.text = message;
            _resolveStatus.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyStatus(_resolveStatus, success);
        }

        private void HideResolveStatus()
        {
            if (_resolveStatus != null)
                _resolveStatus.style.display = DisplayStyle.None;
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
            _connectionFormCard = new VisualElement();
            JiraStyles.ApplyCard(_connectionFormCard);

            var sectionTitle = new Label(L.Tr(L.K.ConnSectionTitle));
            JiraStyles.ApplySectionTitle(sectionTitle);
            _connectionFormCard.Add(sectionTitle);

            var helper = new Label(L.Tr(L.K.ConnHelper));
            JiraStyles.ApplyMuted(helper);
            helper.style.marginBottom = 14;
            _connectionFormCard.Add(helper);

            _urlField = new TextField(L.Tr(L.K.FieldUrl)) { value = JiraPreferences.BaseUrl };
            _urlField.tooltip = L.Tr(L.K.FieldUrlTooltip);
            JiraStyles.ApplyField(_urlField);
            _connectionFormCard.Add(_urlField);

            _emailField = new TextField(L.Tr(L.K.FieldEmail)) { value = JiraPreferences.Email };
            JiraStyles.ApplyField(_emailField);
            _connectionFormCard.Add(_emailField);

            _tokenField = new TextField(L.Tr(L.K.FieldToken))
            {
                value = JiraPreferences.Token,
                isPasswordField = true
            };
            JiraStyles.ApplyField(_tokenField);
            _connectionFormCard.Add(_tokenField);

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
            _connectionFormCard.Add(actions);

            _statusLabel = new Label();
            _statusLabel.style.display = DisplayStyle.None;
            _connectionFormCard.Add(_statusLabel);

            return _connectionFormCard;
        }

        private VisualElement BuildConnectedCard()
        {
            _connectedCard = new VisualElement();
            JiraStyles.ApplyCard(_connectedCard);
            _connectedCard.style.display = DisplayStyle.None;

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
            _createForm.Add(BuildDynamicFieldsLoadingPanel());
            _createForm.Add(BuildAdditionalFieldsCard());
            _createForm.Add(BuildFooter());
            _createForm.RegisterCallback<PointerDownEvent>(
                OnStyledDropdownPointerDown,
                TrickleDown.TrickleDown);
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

            _destinationLoader = BuildModuleLoader(L.Tr(L.K.MsgLoadingCreateDestination));
            _destinationLoader.style.display = DisplayStyle.None;
            card.Add(_destinationLoader);

            _destinationContent = new VisualElement();

            _projectDropdown = new DropdownField(L.Tr(L.K.FieldProject));
            JiraStyles.ApplyDropdown(_projectDropdown);
            _projectDropdown.RegisterValueChangedCallback(_ => OnProjectSelected());

            _typeDropdown = new DropdownField(L.Tr(L.K.FieldIssueType));
            JiraStyles.ApplyDropdown(_typeDropdown);
            _typeDropdown.RegisterValueChangedCallback(_ => OnTypeSelected());

            _destinationContent.Add(JiraStyles.Row(_projectDropdown, _typeDropdown));

            _parentContainer = new VisualElement();
            _parentField = new TextField(L.Tr(L.K.FieldParent));
            _parentField.tooltip = L.Tr(L.K.FieldParentTooltip);
            JiraStyles.ApplyField(_parentField);
            var parentHint = new Label(L.Tr(L.K.ParentHint));
            JiraStyles.ApplyFieldHint(parentHint);
            _parentContainer.Add(_parentField);
            _parentContainer.Add(parentHint);
            _parentContainer.style.display = DisplayStyle.None;
            _destinationContent.Add(_parentContainer);

            _epicContainer = new VisualElement();
            _epicDropdown = new DropdownField(L.Tr(L.K.FieldEpic));
            _epicDropdown.tooltip = L.Tr(L.K.FieldEpicTooltip);
            JiraStyles.ApplyDropdown(_epicDropdown);
            _epicDropdown.RegisterValueChangedCallback(_ => OnEpicSelected());
            _epicContainer.Add(_epicDropdown);

            _sprintDropdown = new DropdownField(L.Tr(L.K.FieldSprint));
            _sprintDropdown.tooltip = L.Tr(L.K.FieldSprintTooltip);
            JiraStyles.ApplyDropdown(_sprintDropdown);

            _destinationContent.Add(JiraStyles.Row(_epicContainer, _sprintDropdown));
            _destinationContent.Add(BuildEpicProgress());

            var refreshButton = new Button(() => ReloadProjectsAsync()) { text = L.Tr(L.K.BtnReloadProjects) };
            JiraStyles.ApplyGhostButton(refreshButton);
            _destinationContent.Add(refreshButton);

            _fieldsStatusLabel = new Label();
            JiraStyles.ApplyMuted(_fieldsStatusLabel);
            _fieldsStatusLabel.style.marginTop = 8;
            _fieldsStatusLabel.style.display = DisplayStyle.None;
            _destinationContent.Add(_fieldsStatusLabel);
            card.Add(_destinationContent);

            return card;
        }

        private VisualElement BuildDynamicFieldsLoadingPanel()
        {
            _dynamicFieldsLoadingPanel = new VisualElement();
            _dynamicFieldsLoadingPanel.Add(BuildLoadingCard(
                L.Tr(L.K.CreateAdditionalFieldsTitle),
                L.Tr(L.K.MsgLoadingModule)));
            _dynamicFieldsLoadingPanel.style.display = DisplayStyle.None;
            return _dynamicFieldsLoadingPanel;
        }

        private VisualElement BuildLoadingCard(string titleText, string message)
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(titleText);
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);
            card.Add(BuildModuleLoader(message));
            return card;
        }

        private VisualElement BuildModuleLoader(string message)
        {
            var row = new VisualElement();
            JiraStyles.ApplyLoaderRow(row);

            var spinner = new VisualElement();
            JiraStyles.ApplyLoaderSpinner(spinner);
            _loaderSpinners.Add(spinner);

            var label = new Label(message);
            JiraStyles.ApplyMuted(label);
            label.style.marginLeft = 10;

            row.Add(spinner);
            row.Add(label);
            return row;
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

            _summaryField =
                new TextField(RequiredLabel(L.Tr(L.K.FieldSummary)));
            JiraStyles.ApplyField(_summaryField);
            card.Add(_summaryField);

            _descriptionField = new TextField(L.Tr(L.K.FieldDescription));
            JiraStyles.ApplyMultiline(_descriptionField);
            _descriptionField.style.marginBottom = 20;
            card.Add(_descriptionField);

            card.Add(BuildAttachmentSection());

            _classifyLoader = BuildModuleLoader(L.Tr(L.K.MsgLoadingModule));
            _classifyLoader.style.display = DisplayStyle.None;
            card.Add(_classifyLoader);

            _classifyContent = new VisualElement();
            _classifyContent.style.marginBottom = 4;
            card.Add(_classifyContent);

            _datesContent = new VisualElement();
            _datesContent.style.display = DisplayStyle.None;
            _datesContent.style.marginBottom = 4;
            card.Add(_datesContent);

            _quickSubtaskContainer = new VisualElement();
            _quickSubtaskContainer.style.marginTop = 4;
            _quickSubtaskContainer.style.paddingTop = 14;
            _quickSubtaskContainer.style.borderTopWidth = 1;
            _quickSubtaskContainer.style.borderTopColor =
                new StyleColor(new Color32(67, 73, 84, 255));

            var quickSubtaskHeader = new VisualElement();
            quickSubtaskHeader.style.flexDirection = FlexDirection.Row;
            quickSubtaskHeader.style.alignItems = Align.Center;
            quickSubtaskHeader.style.marginBottom = 8;

            var quickSubtaskTitle = new Label(L.Tr(L.K.FieldQuickSubtask));
            JiraStyles.ApplyDynamicFieldLabel(quickSubtaskTitle);
            quickSubtaskTitle.style.flexGrow = 1;
            quickSubtaskTitle.style.flexShrink = 1;
            quickSubtaskTitle.style.minWidth = 0;
            quickSubtaskTitle.style.marginBottom = 0;
            quickSubtaskHeader.Add(quickSubtaskTitle);

            var addQuickSubtaskButton = new Button(AddQuickSubtask)
            {
                text = L.Tr(L.K.BtnAddQuickSubtask)
            };
            JiraStyles.ApplyCompactButton(addQuickSubtaskButton, false);
            addQuickSubtaskButton.style.flexShrink = 0;
            addQuickSubtaskButton.style.marginLeft = 12;
            quickSubtaskHeader.Add(addQuickSubtaskButton);
            _quickSubtaskContainer.Add(quickSubtaskHeader);

            var quickSubtaskHint = new Label(L.Tr(L.K.QuickSubtaskHint));
            JiraStyles.ApplyFieldHint(quickSubtaskHint);
            quickSubtaskHint.style.marginTop = 0;
            quickSubtaskHint.style.marginBottom = 12;
            _quickSubtaskContainer.Add(quickSubtaskHint);

            _quickSubtasksList = new VisualElement();
            _quickSubtaskContainer.Add(_quickSubtasksList);

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
                Title = new TextField(
                    RequiredLabel(
                        L.Tr(L.K.FieldQuickSubtaskTitle))),
                Description = new TextField(
                    FieldLabel(
                        _quickSubtaskDescriptionMeta,
                        L.Tr(L.K.FieldQuickSubtaskDescription)))
            };
            JiraStyles.ApplyNestedCard(binding.Root);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.minHeight = 30;
            headerRow.style.marginBottom = 8;

            JiraStyles.ApplyDynamicFieldLabel(binding.Header);
            binding.Header.style.flexGrow = 1;
            binding.Header.style.marginBottom = 0;
            headerRow.Add(binding.Header);

            var removeButton = new Button(() => RemoveQuickSubtask(binding))
            {
                text = "−",
                tooltip = L.Tr(L.K.BtnRemoveQuickSubtask)
            };
            JiraStyles.ApplyCompactButton(removeButton, true);
            removeButton.style.width = 28;
            removeButton.style.minWidth = 28;
            removeButton.style.height = 28;
            removeButton.style.paddingLeft = 0;
            removeButton.style.paddingRight = 0;
            removeButton.style.marginLeft = 10;
            removeButton.style.flexShrink = 0;
            removeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            headerRow.Add(removeButton);
            binding.Root.Add(headerRow);

            JiraStyles.ApplyField(binding.Title);
            binding.Root.Add(binding.Title);

            JiraStyles.ApplyMultiline(binding.Description);
            binding.Description.style.minHeight = 64;
            binding.Description.style.marginBottom = 8;
            binding.Root.Add(binding.Description);

            if (_quickSubtaskPriorityMeta != null &&
                _quickSubtaskPriorityMeta.HasAllowedValues)
            {
                JiraAllowedValue parentPriority = AllowedAt(
                    _priorityMeta,
                    _priorityDropdown?.index ?? -1);
                string initialPriorityId =
                    parentPriority?.id ?? JiraPreferences.PresetPriorityId;

                binding.Priority = BuildAllowedDropdown(
                    L.Tr(L.K.FieldQuickSubtaskPriority),
                    _quickSubtaskPriorityMeta,
                    initialPriorityId,
                    preferMedium: true);
                binding.Priority.style.marginTop = 4;
                binding.Root.Add(binding.Priority);
            }

            if (_quickSubtaskTeamMeta != null)
            {
                string parentTeamId = SelectedCreateTeamId(
                    _quickSubtaskTeamMeta);
                if (_quickSubtaskTeamMeta.HasAllowedValues)
                {
                    binding.Team = BuildTeamDropdown(
                        _quickSubtaskTeamMeta,
                        parentTeamId);
                    binding.Root.Add(binding.Team);
                }
                else
                {
                    binding.TeamText = new TextField(
                        FieldLabel(
                            _quickSubtaskTeamMeta,
                            L.Tr(L.K.FieldTeam)));
                    binding.TeamText.SetValueWithoutNotify(
                        parentTeamId ?? string.Empty);
                    JiraStyles.ApplyField(binding.TeamText);
                    binding.Root.Add(binding.TeamText);

                    var teamHint = new Label(L.Tr(L.K.TeamIdHint));
                    JiraStyles.ApplyFieldHint(teamHint);
                    binding.Root.Add(teamHint);
                }
            }

            if (_quickSubtaskAssigneeMeta != null)
            {
                var assigneeContainer = new VisualElement();
                binding.Assignee = new DropdownField(
                    FieldLabel(
                        _quickSubtaskAssigneeMeta,
                        L.Tr(L.K.FieldAssignee)));
                JiraStyles.ApplyDropdown(binding.Assignee);
                PopulateAssigneeDropdown(
                    binding.Assignee,
                    _assignableUsers,
                    SelectedAssigneeAccountId());
                assigneeContainer.Add(binding.Assignee);

                var assignSelfButton = new Button(() =>
                    AssignDropdownToSelf(binding.Assignee))
                {
                    text = L.Tr(L.K.BtnAssignSelf)
                };
                JiraStyles.ApplyGhostButton(assignSelfButton);
                assignSelfButton.style.marginBottom = 10;
                assigneeContainer.Add(assignSelfButton);
                binding.Root.Add(assigneeContainer);
            }

            if (_quickSubtaskStartDateMeta != null)
            {
                binding.StartDate = new TextField(
                    FieldLabel(
                        _quickSubtaskStartDateMeta,
                        L.Tr(L.K.FieldStartDate)));
                binding.StartDate.tooltip = L.Tr(L.K.DateHint);
                JiraStyles.ApplyField(binding.StartDate);
            }
            if (_quickSubtaskDueDateMeta != null)
            {
                binding.DueDate = new TextField(
                    FieldLabel(
                        _quickSubtaskDueDateMeta,
                        L.Tr(L.K.FieldDueDate)));
                binding.DueDate.tooltip = L.Tr(L.K.DateHint);
                JiraStyles.ApplyField(binding.DueDate);
            }
            if (binding.StartDate != null && binding.DueDate != null)
            {
                binding.Root.Add(JiraStyles.Row(
                    binding.StartDate,
                    binding.DueDate));
            }
            else if (binding.StartDate != null)
            {
                binding.Root.Add(binding.StartDate);
            }
            else if (binding.DueDate != null)
            {
                binding.Root.Add(binding.DueDate);
            }
            if (binding.StartDate != null || binding.DueDate != null)
            {
                var dateHint = new Label(L.Tr(L.K.DateHint));
                JiraStyles.ApplyFieldHint(dateHint);
                binding.Root.Add(dateHint);
            }

            var attachmentTitle = new Label(
                L.Tr(L.K.FieldSubtaskAttachment));
            JiraStyles.ApplyDynamicFieldLabel(attachmentTitle);
            binding.Root.Add(attachmentTitle);
            var attachmentRow = new VisualElement();
            attachmentRow.style.flexDirection = FlexDirection.Row;
            attachmentRow.style.flexWrap = Wrap.Wrap;
            var selectAttachmentButton = new Button(() =>
                SelectQuickSubtaskAttachment(binding))
            {
                text = L.Tr(L.K.BtnSelectFile)
            };
            JiraStyles.ApplyGhostButton(selectAttachmentButton);
            selectAttachmentButton.style.marginRight = 8;
#if UNITY_EDITOR_WIN
            var clipAttachmentButton = new Button(() =>
                StartQuickSubtaskScreenClip(binding))
            {
                text = L.Tr(L.K.BtnCaptureScreenArea)
            };
            JiraStyles.ApplyGhostButton(clipAttachmentButton);
            clipAttachmentButton.style.marginRight = 8;
#endif
            var clearAttachmentButton = new Button(() =>
                ClearQuickSubtaskAttachment(binding))
            {
                text = L.Tr(L.K.BtnRemoveFile)
            };
            JiraStyles.ApplyGhostButton(clearAttachmentButton);
            attachmentRow.Add(selectAttachmentButton);
#if UNITY_EDITOR_WIN
            attachmentRow.Add(clipAttachmentButton);
#endif
            attachmentRow.Add(clearAttachmentButton);
            binding.Root.Add(attachmentRow);

            binding.AttachmentLabel =
                new Label(L.Tr(L.K.NoFileSelected));
            JiraStyles.ApplyFieldHint(binding.AttachmentLabel);
            binding.AttachmentLabel.style.marginTop = 7;
            binding.Root.Add(binding.AttachmentLabel);
            binding.AttachmentPreview =
                CreateInlineAttachmentPreview();
            binding.Root.Add(binding.AttachmentPreview.Root);
            var inlineImageHint = new Label(
                L.Tr(L.K.AttachmentInlineDescriptionHint));
            JiraStyles.ApplyFieldHint(inlineImageHint);
            binding.Root.Add(inlineImageHint);

            _quickSubtasks.Add(binding);
            _quickSubtasksList.Add(binding.Root);
            RefreshQuickSubtaskHeaders();
        }

        private void RemoveQuickSubtask(QuickSubtaskBinding binding)
        {
            if (binding == null || !_quickSubtasks.Remove(binding))
                return;

            ClearInlineAttachmentPreview(
                binding.AttachmentPreview);
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
            foreach (QuickSubtaskBinding binding in _quickSubtasks)
            {
                ClearInlineAttachmentPreview(
                    binding?.AttachmentPreview);
            }
            _quickSubtasks.Clear();
            _quickSubtasksList?.Clear();
        }

        private void ClearQuickSubtaskMetadata()
        {
            _quickSubtaskType = null;
            _quickSubtaskDescriptionMeta = null;
            _quickSubtaskPriorityMeta = null;
            _quickSubtaskTeamMeta = null;
            _quickSubtaskAssigneeMeta = null;
            _quickSubtaskStartDateMeta = null;
            _quickSubtaskDueDateMeta = null;
        }

        private void ConfigureQuickSubtaskMetadata(
            JiraIssueType subtaskType,
            List<JiraFieldMeta> fields)
        {
            _quickSubtaskType = subtaskType;
            fields = fields ?? new List<JiraFieldMeta>();
            _quickSubtaskDescriptionMeta =
                FindById(fields, "description");
            _quickSubtaskPriorityMeta = FindById(fields, FieldPriority);
            _quickSubtaskTeamMeta = FindTeamField(fields);
            _quickSubtaskAssigneeMeta = FindById(fields, FieldAssignee);
            _quickSubtaskStartDateMeta = FindStartDate(fields);
            _quickSubtaskDueDateMeta = FindById(fields, FieldDueDate);
        }

        private static void PopulateAssigneeDropdown(
            DropdownField dropdown,
            IList<JiraUser> users,
            string preferredAccountId)
        {
            if (dropdown == null)
                return;

            var availableUsers = new List<JiraUser>();
            var labels = new List<string> { L.Tr(L.K.AssigneeNone) };
            int selectedIndex = 0;

            if (users != null)
            {
                foreach (JiraUser user in users)
                {
                    if (user == null ||
                        string.IsNullOrWhiteSpace(user.accountId))
                    {
                        continue;
                    }

                    availableUsers.Add(user);
                    labels.Add(AssigneeDisplay(user));
                    if (string.Equals(
                            user.accountId,
                            preferredAccountId,
                            StringComparison.Ordinal))
                    {
                        selectedIndex = availableUsers.Count;
                    }
                }
            }

            dropdown.userData = availableUsers;
            dropdown.choices = labels;
            dropdown.SetValueWithoutNotify(labels[selectedIndex]);
            dropdown.SetEnabled(labels.Count > 1);
        }

        private void AssignDropdownToSelf(DropdownField dropdown)
        {
            if (dropdown == null || _myself == null ||
                string.IsNullOrWhiteSpace(_myself.accountId))
            {
                return;
            }

            var users = dropdown.userData as List<JiraUser>;
            if (users == null)
            {
                users = new List<JiraUser>();
                dropdown.userData = users;
            }

            int index = users.FindIndex(user =>
                user != null &&
                string.Equals(
                    user.accountId,
                    _myself.accountId,
                    StringComparison.Ordinal));
            if (index < 0)
            {
                users.Insert(0, _myself);
                var labels =
                    new List<string> { L.Tr(L.K.AssigneeNone) };
                foreach (JiraUser user in users)
                    labels.Add(AssigneeDisplay(user));
                dropdown.choices = labels;
                index = 0;
            }

            dropdown.index = index + 1;
            dropdown.SetEnabled(true);
        }

        private static string SelectedDropdownAssigneeAccountId(
            DropdownField dropdown)
        {
            if (dropdown == null || dropdown.index <= 0)
                return null;

            var users = dropdown.userData as List<JiraUser>;
            int userIndex = dropdown.index - 1;
            return users != null &&
                   userIndex >= 0 &&
                   userIndex < users.Count
                ? users[userIndex]?.accountId
                : null;
        }

        private AttachmentPreviewBinding CreateInlineAttachmentPreview()
        {
            var preview = new AttachmentPreviewBinding
            {
                Root = new VisualElement(),
                Image = new Image
                {
                    scaleMode = ScaleMode.ScaleToFit
                },
                Info = new Label()
            };
            preview.Root.style.display = DisplayStyle.None;
            preview.Root.style.marginTop = 6;
            preview.Root.style.marginBottom = 10;
            preview.Root.style.paddingLeft = 8;
            preview.Root.style.paddingRight = 8;
            preview.Root.style.paddingTop = 8;
            preview.Root.style.paddingBottom = 8;
            preview.Root.style.backgroundColor =
                new StyleColor(new Color32(30, 33, 39, 255));
            preview.Root.style.borderLeftWidth = 1;
            preview.Root.style.borderRightWidth = 1;
            preview.Root.style.borderTopWidth = 1;
            preview.Root.style.borderBottomWidth = 1;
            Color border = new Color32(67, 73, 84, 255);
            preview.Root.style.borderLeftColor = border;
            preview.Root.style.borderRightColor = border;
            preview.Root.style.borderTopColor = border;
            preview.Root.style.borderBottomColor = border;
            preview.Root.style.borderTopLeftRadius = 6;
            preview.Root.style.borderTopRightRadius = 6;
            preview.Root.style.borderBottomLeftRadius = 6;
            preview.Root.style.borderBottomRightRadius = 6;

            var title = new Label(L.Tr(L.K.AttachmentPreviewTitle));
            JiraStyles.ApplyDynamicFieldLabel(title);
            preview.Root.Add(title);

            preview.Image.style.width = Length.Percent(100);
            preview.Image.style.minHeight = 100;
            preview.Image.style.maxHeight = 240;
            preview.Root.Add(preview.Image);

            JiraStyles.ApplyFieldHint(preview.Info);
            preview.Info.style.marginTop = 7;
            preview.Info.style.marginBottom = 0;
            preview.Root.Add(preview.Info);
            return preview;
        }

        private void RefreshInlineAttachmentPreview(
            AttachmentPreviewBinding preview,
            string path)
        {
            ClearInlineAttachmentPreview(preview);
            if (preview == null ||
                string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    DestroyImmediate(texture);
                    return;
                }

                preview.Texture = texture;
                preview.Image.image = texture;
                float aspect = texture.width > 0
                    ? (float)texture.height / texture.width
                    : 0.5625f;
                preview.Image.style.height =
                    Mathf.Clamp(480f * aspect, 100f, 240f);
                preview.Info.text = L.Tr(
                    L.K.AttachmentPreviewInfo,
                    texture.width,
                    texture.height,
                    Path.GetFileName(path));
                preview.Root.style.display = DisplayStyle.Flex;
            }
            catch
            {
                ClearInlineAttachmentPreview(preview);
            }
        }

        private void ClearInlineAttachmentPreview(
            AttachmentPreviewBinding preview)
        {
            if (preview == null)
                return;

            if (preview.Image != null)
                preview.Image.image = null;
            if (preview.Root != null)
                preview.Root.style.display = DisplayStyle.None;
            if (preview.Info != null)
                preview.Info.text = string.Empty;
            if (preview.Texture != null)
            {
                DestroyImmediate(preview.Texture);
                preview.Texture = null;
            }
        }

        private void SelectQuickSubtaskAttachment(
            QuickSubtaskBinding binding)
        {
            if (binding == null)
                return;

            string path = EditorUtility.OpenFilePanel(
                L.Tr(L.K.BtnSelectFile),
                string.Empty,
                string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                return;

            SetQuickSubtaskAttachment(
                binding,
                path,
                Path.GetFileName(path));
        }

        private void SetQuickSubtaskAttachment(
            QuickSubtaskBinding binding,
            string path,
            string displayText)
        {
            if (binding == null)
                return;

            binding.AttachmentPath = path;
            if (binding.AttachmentLabel != null)
            {
                binding.AttachmentLabel.text =
                    displayText ?? Path.GetFileName(path);
            }
            RefreshInlineAttachmentPreview(
                binding.AttachmentPreview,
                path);
        }

        private void ClearQuickSubtaskAttachment(
            QuickSubtaskBinding binding)
        {
            if (binding == null)
                return;

            binding.AttachmentPath = null;
            if (binding.AttachmentLabel != null)
            {
                binding.AttachmentLabel.text =
                    L.Tr(L.K.NoFileSelected);
            }
            ClearInlineAttachmentPreview(
                binding.AttachmentPreview);
        }

        private void SelectResolveSubtaskAttachment()
        {
            string path = EditorUtility.OpenFilePanel(
                L.Tr(L.K.BtnSelectFile),
                string.Empty,
                string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                return;

            SetResolveSubtaskAttachment(
                path,
                Path.GetFileName(path));
        }

        private void SetResolveSubtaskAttachment(
            string path,
            string displayText)
        {
            _resolveNewSubtaskAttachmentPath = path;
            if (_resolveNewSubtaskAttachmentLabel != null)
            {
                _resolveNewSubtaskAttachmentLabel.text =
                    displayText ?? Path.GetFileName(path);
            }
            RefreshInlineAttachmentPreview(
                _resolveNewSubtaskAttachmentPreview,
                path);
        }

        private void ClearResolveSubtaskAttachment()
        {
            _resolveNewSubtaskAttachmentPath = null;
            if (_resolveNewSubtaskAttachmentLabel != null)
            {
                _resolveNewSubtaskAttachmentLabel.text =
                    L.Tr(L.K.NoFileSelected);
            }
            ClearInlineAttachmentPreview(
                _resolveNewSubtaskAttachmentPreview);
        }

        private void ResetResolveSubtaskForm()
        {
            _resolveSubtaskFieldLoadVersion++;
            _resolveSubtaskFieldsLoading = false;
            _resolveAvailableChildTypes.Clear();
            _resolveNewSubtaskType = null;
            _resolveNewSubtaskDescriptionMeta = null;
            _resolveNewSubtaskPriorityMeta = null;
            _resolveNewSubtaskTeamMeta = null;
            _resolveNewSubtaskAssigneeMeta = null;
            _resolveNewSubtaskStartDateMeta = null;
            _resolveNewSubtaskDueDateMeta = null;
            _resolveNewSubtaskTitle?.SetValueWithoutNotify(string.Empty);
            _resolveNewSubtaskDescription?.SetValueWithoutNotify(
                string.Empty);
            _resolveNewSubtaskStartDate?.SetValueWithoutNotify(
                string.Empty);
            _resolveNewSubtaskDueDate?.SetValueWithoutNotify(
                string.Empty);
            _resolveNewSubtaskTeamText?.SetValueWithoutNotify(
                string.Empty);
            if (_resolveNewSubtaskTeam != null)
            {
                _resolveNewSubtaskTeam.choices = new List<string>();
                _resolveNewSubtaskTeam.SetValueWithoutNotify(
                    string.Empty);
            }
            if (_resolveNewChildTypeDropdown != null)
            {
                _resolveNewChildTypeDropdown.choices =
                    new List<string>();
                _resolveNewChildTypeDropdown.SetValueWithoutNotify(
                    string.Empty);
                _resolveNewChildTypeDropdown.style.display =
                    DisplayStyle.None;
            }
            ClearResolveSubtaskAttachment();
            if (_resolveNewSubtaskAssigneeContainer != null)
            {
                _resolveNewSubtaskAssigneeContainer.style.display =
                    DisplayStyle.None;
            }
            if (_resolveNewSubtaskTeamContainer != null)
            {
                _resolveNewSubtaskTeamContainer.style.display =
                    DisplayStyle.None;
            }
            if (_resolveNewSubtaskDatesContainer != null)
            {
                _resolveNewSubtaskDatesContainer.style.display =
                    DisplayStyle.None;
            }
        }

        private VisualElement BuildAttachmentSection()
        {
            var section = new VisualElement();
            JiraStyles.ApplyNestedCard(section);
            section.style.marginBottom = 16;

            var title = new Label(L.Tr(L.K.CreateAttachmentTitle));
            JiraStyles.ApplyDynamicFieldLabel(title);
            section.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
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

#if UNITY_EDITOR_WIN
            var pasteImageButton = new Button(PasteWindowsClipboardImageAsync)
            {
                text = L.Tr(L.K.BtnPasteClipboardImage)
            };
            JiraStyles.ApplyGhostButton(pasteImageButton);
            pasteImageButton.style.marginRight = 8;
            row.Add(pasteImageButton);

            var screenClipButton = new Button(StartWindowsScreenClip)
            {
                text = L.Tr(L.K.BtnCaptureScreenArea)
            };
            JiraStyles.ApplyGhostButton(screenClipButton);
            screenClipButton.style.marginRight = 8;
            row.Add(screenClipButton);
#endif

            var removeButton = new Button(ClearAttachment) { text = L.Tr(L.K.BtnRemoveFile) };
            JiraStyles.ApplyGhostButton(removeButton);
            row.Add(removeButton);

            section.Add(row);

            _attachmentLabel = new Label(L.Tr(L.K.NoFileSelected));
            JiraStyles.ApplyFieldHint(_attachmentLabel);
            _attachmentLabel.style.marginTop = 8;
            section.Add(_attachmentLabel);

            _attachmentPreviewContainer = new VisualElement();
            _attachmentPreviewContainer.style.display = DisplayStyle.None;
            _attachmentPreviewContainer.style.marginTop = 6;
            _attachmentPreviewContainer.style.marginBottom = 10;
            _attachmentPreviewContainer.style.paddingLeft = 8;
            _attachmentPreviewContainer.style.paddingRight = 8;
            _attachmentPreviewContainer.style.paddingTop = 8;
            _attachmentPreviewContainer.style.paddingBottom = 8;
            _attachmentPreviewContainer.style.backgroundColor =
                new StyleColor(new Color32(30, 33, 39, 255));
            _attachmentPreviewContainer.style.borderLeftWidth = 1;
            _attachmentPreviewContainer.style.borderRightWidth = 1;
            _attachmentPreviewContainer.style.borderTopWidth = 1;
            _attachmentPreviewContainer.style.borderBottomWidth = 1;
            Color previewBorder = new Color32(67, 73, 84, 255);
            _attachmentPreviewContainer.style.borderLeftColor = previewBorder;
            _attachmentPreviewContainer.style.borderRightColor = previewBorder;
            _attachmentPreviewContainer.style.borderTopColor = previewBorder;
            _attachmentPreviewContainer.style.borderBottomColor = previewBorder;
            _attachmentPreviewContainer.style.borderTopLeftRadius = 6;
            _attachmentPreviewContainer.style.borderTopRightRadius = 6;
            _attachmentPreviewContainer.style.borderBottomLeftRadius = 6;
            _attachmentPreviewContainer.style.borderBottomRightRadius = 6;

            var previewTitle =
                new Label(L.Tr(L.K.AttachmentPreviewTitle));
            JiraStyles.ApplyDynamicFieldLabel(previewTitle);
            _attachmentPreviewContainer.Add(previewTitle);

            _attachmentPreviewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            _attachmentPreviewImage.style.width = Length.Percent(100);
            _attachmentPreviewImage.style.minHeight = 120;
            _attachmentPreviewImage.style.maxHeight = 280;
            _attachmentPreviewContainer.Add(_attachmentPreviewImage);

            _attachmentPreviewInfo = new Label();
            JiraStyles.ApplyFieldHint(_attachmentPreviewInfo);
            _attachmentPreviewInfo.style.marginTop = 7;
            _attachmentPreviewInfo.style.marginBottom = 0;
            _attachmentPreviewContainer.Add(_attachmentPreviewInfo);
            section.Add(_attachmentPreviewContainer);

            if (!string.IsNullOrWhiteSpace(_attachmentPath))
            {
                _attachmentLabel.text = Path.GetFileName(_attachmentPath);
                RefreshAttachmentPreview(_attachmentPath);
            }

            var inlineImageHint = new Label(
                L.Tr(L.K.AttachmentInlineDescriptionHint));
            JiraStyles.ApplyFieldHint(inlineImageHint);
            section.Add(inlineImageHint);

            var screenshotHint = new Label(L.Tr(L.K.CaptureGameViewHint));
            JiraStyles.ApplyFieldHint(screenshotHint);
            section.Add(screenshotHint);

#if UNITY_EDITOR_WIN
            var screenClipHint = new Label(L.Tr(L.K.CaptureScreenAreaHint));
            JiraStyles.ApplyFieldHint(screenClipHint);
            section.Add(screenClipHint);
#endif

            return section;
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

            if (connected && !_projectsLoaded && !_projectsLoading)
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

            SetAttachment(
                path,
                System.IO.Path.GetFileName(path));
        }

#if UNITY_EDITOR_WIN
        private void StartWindowsScreenClip()
        {
            StartWindowsScreenClip(
                SetAttachment,
                _attachmentLabel);
        }

        private void StartQuickSubtaskScreenClip(
            QuickSubtaskBinding binding)
        {
            if (binding == null)
                return;

            StartWindowsScreenClip(
                (path, text) => SetQuickSubtaskAttachment(
                    binding,
                    path,
                    text),
                binding.AttachmentLabel);
        }

        private void StartResolveSubtaskScreenClip()
        {
            StartWindowsScreenClip(
                SetResolveSubtaskAttachment,
                _resolveNewSubtaskAttachmentLabel);
        }

        private void StartWindowsScreenClip(
            Action<string, string> targetSetter,
            Label targetLabel)
        {
            const byte virtualKeyWindows = 0x5B;
            const byte virtualKeyShift = 0x10;
            const byte virtualKeyS = 0x53;
            const uint keyUp = 0x0002;

            _windowsSnipClipboardSequence = GetClipboardSequenceNumber();
            _windowsSnipRequestedAt = EditorApplication.timeSinceStartup;
            _waitingForWindowsSnip = true;
            _windowsSnipTargetSetter = targetSetter;
            _windowsSnipTargetLabel = targetLabel;
            _attachmentLabelBeforeWindowsSnip =
                targetLabel?.text;
            if (targetLabel != null)
            {
                targetLabel.text =
                    L.Tr(L.K.MsgScreenClipWaiting);
            }

            keybd_event(virtualKeyWindows, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKeyShift, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKeyS, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKeyS, 0, keyUp, UIntPtr.Zero);
            keybd_event(virtualKeyShift, 0, keyUp, UIntPtr.Zero);
            keybd_event(virtualKeyWindows, 0, keyUp, UIntPtr.Zero);
        }

        private async void ImportWindowsSnipAsync(uint previousSequence)
        {
            await Task.Delay(300);
            if (GetClipboardSequenceNumber() == previousSequence)
            {
                string message = L.Tr(L.K.MsgScreenClipCancelled);
                RestoreAttachmentLabelAfterWindowsSnip();
                ShowNotification(new GUIContent(message));
                return;
            }

            await ImportWindowsClipboardImageAsync(
                L.K.MsgScreenClipImported,
                L.K.MsgScreenClipFailed);
        }

        private async void PasteWindowsClipboardImageAsync()
        {
            _windowsSnipTargetSetter = SetAttachment;
            _windowsSnipTargetLabel = _attachmentLabel;
            _attachmentLabelBeforeWindowsSnip =
                _attachmentLabel.text;
            _attachmentLabel.text =
                L.Tr(L.K.MsgClipboardImageImporting);

            await ImportWindowsClipboardImageAsync(
                L.K.MsgClipboardImageImported,
                L.K.MsgClipboardNoImage);
        }

        private async Task ImportWindowsClipboardImageAsync(
            string successMessageKey,
            string failureMessageKey)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "OxenteGames",
                "JiraCommunication");
            string path = Path.Combine(
                directory,
                $"windows-snip-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.png");

            try
            {
                Directory.CreateDirectory(directory);
                bool saved = await Task.Run(() =>
                    SaveWindowsClipboardImage(path));
                if (!saved || !File.Exists(path))
                {
                    string message = L.Tr(failureMessageKey);
                    RestoreAttachmentLabelAfterWindowsSnip();
                    ShowNotification(new GUIContent(message));
                    return;
                }

                string displayText = L.Tr(
                    successMessageKey,
                    Path.GetFileName(path));
                Action<string, string> targetSetter =
                    _windowsSnipTargetSetter;
                ClearWindowsSnipTarget();
                targetSetter?.Invoke(path, displayText);
            }
            catch (Exception exception)
            {
                string message = L.Tr(
                    L.K.MsgScreenClipFailedWithReason,
                    exception.Message);
                RestoreAttachmentLabelAfterWindowsSnip();
                ShowNotification(new GUIContent(message));
            }
        }

        private void RestoreAttachmentLabelAfterWindowsSnip()
        {
            if (_windowsSnipTargetLabel != null)
            {
                _windowsSnipTargetLabel.text =
                    !string.IsNullOrWhiteSpace(
                        _attachmentLabelBeforeWindowsSnip)
                        ? _attachmentLabelBeforeWindowsSnip
                        : L.Tr(L.K.NoFileSelected);
            }
            ClearWindowsSnipTarget();
        }

        private void ClearWindowsSnipTarget()
        {
            _attachmentLabelBeforeWindowsSnip = null;
            _windowsSnipTargetSetter = null;
            _windowsSnipTargetLabel = null;
        }

        private static bool SaveWindowsClipboardImage(string path)
        {
            string escapedPath = path.Replace("'", "''");
            string script =
                "$ErrorActionPreference='Stop';" +
                "Add-Type -AssemblyName System.Windows.Forms;" +
                "Add-Type -AssemblyName System.Drawing;" +
                "$image=[System.Windows.Forms.Clipboard]::GetImage();" +
                "if($null -eq $image){exit 2};" +
                "try{$image.Save('" + escapedPath +
                "',[System.Drawing.Imaging.ImageFormat]::Png)}" +
                "finally{$image.Dispose()}";
            string encoded = Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(script));
            string systemDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.System);
            string executable = Path.Combine(
                systemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            if (!File.Exists(executable))
                executable = "powershell.exe";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                Arguments =
                    "-NoProfile -NonInteractive -STA -EncodedCommand " +
                    encoded,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };

            using (System.Diagnostics.Process process =
                   System.Diagnostics.Process.Start(startInfo))
            {
                if (process == null)
                    return false;

                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); }
                    catch { }
                    return false;
                }

                return process.ExitCode == 0;
            }
        }
#endif

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

                SetAttachment(
                    path,
                    L.Tr(L.K.MsgScreenshotCaptured, fileName));
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
            ClearAttachmentPreview();
        }

        private void SetAttachment(string path, string displayText)
        {
            _attachmentPath = path ?? string.Empty;
            _attachmentLabel.text = displayText ?? Path.GetFileName(path);
            RefreshAttachmentPreview(_attachmentPath);
        }

        private void RefreshAttachmentPreview(string path)
        {
            ClearAttachmentPreview();
            if (_attachmentPreviewContainer == null ||
                _attachmentPreviewImage == null ||
                string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    DestroyImmediate(texture);
                    return;
                }

                _attachmentPreviewTexture = texture;
                _attachmentPreviewImage.image = texture;
                float aspect = texture.width > 0
                    ? (float)texture.height / texture.width
                    : 0.5625f;
                _attachmentPreviewImage.style.height =
                    Mathf.Clamp(520f * aspect, 120f, 280f);
                _attachmentPreviewInfo.text = L.Tr(
                    L.K.AttachmentPreviewInfo,
                    texture.width,
                    texture.height,
                    Path.GetFileName(path));
                _attachmentPreviewContainer.style.display =
                    DisplayStyle.Flex;
            }
            catch
            {
                ClearAttachmentPreview();
            }
        }

        private void ClearAttachmentPreview()
        {
            if (_attachmentPreviewImage != null)
                _attachmentPreviewImage.image = null;
            if (_attachmentPreviewContainer != null)
                _attachmentPreviewContainer.style.display =
                    DisplayStyle.None;
            if (_attachmentPreviewInfo != null)
                _attachmentPreviewInfo.text = string.Empty;
            ReleaseAttachmentPreviewTexture();
        }

        private void ReleaseAttachmentPreviewTexture()
        {
            if (_attachmentPreviewTexture == null)
                return;

            DestroyImmediate(_attachmentPreviewTexture);
            _attachmentPreviewTexture = null;
        }

        // --- Settings panel -------------------------------------------------

        // --- Agent panel ---------------------------------------------------

        private VisualElement BuildAgentPanel()
        {
            // Repaint for live chat updates. Configuration is not built here any more:
            // the toolbar's configure button jumps to the settings tab, where
            // AgentSettingsView owns those fields.
            _agentConsole = new AgentConsoleView(Repaint, () => SelectTab(Tab.Settings));
            return _agentConsole.Build();
        }

        /// <summary>
        /// The ai-jira section of the settings tab: install it, and diagnose it.
        /// </summary>
        /// <remarks>
        /// It lives here rather than in a tab of its own because it is setup, and
        /// setup is done once. The work it enables happens in the agent chat, typed as
        /// <c>/jira-card</c> and friends — so a tab would have been a permanent
        /// fixture in the window for a panel most developers open twice.
        /// <para>
        /// Built unconditionally, including on a machine with no ai-jira: this is
        /// where it gets installed, so hiding it when it is absent would hide the
        /// install button behind the thing it installs.
        /// </para>
        /// </remarks>
        private VisualElement BuildAiJiraCard()
        {
            _aiJiraView = new AiJiraView(Repaint, () => SelectTab(Tab.Agent));
            return _aiJiraView.Build();
        }

        /// <summary>
        /// Hands the currently open issue to the agent tab.
        /// </summary>
        /// <remarks>
        /// This is the workflow the feature exists for: the developer is already
        /// looking at an issue here, so the agent should start with its key, summary,
        /// description and the branch name the team convention produces, instead of
        /// having that retyped.
        /// </remarks>
        private void SendCurrentIssueToAgent()
        {
            if (_selectedIssue == null)
                return;

            string issueKey = _selectedIssue.key ?? string.Empty;
            string summary = _selectedIssue.Summary;

            // The description lives in the edit field rather than on the list model,
            // which also means any local edit is what gets sent.
            string description = _resolveDescriptionField?.value ?? string.Empty;

            string branch = GitConventions.BuildBranch(
                JiraPreferences.GitBranchTemplate, CurrentGitType(), issueKey, summary);

            SelectTab(Tab.Agent);
            _agentConsole?.SetIssueContext(issueKey, summary, description, branch);
        }

        private void OnDisable()
        {
            _agentConsole?.Dispose();
            _agentConsole = null;
        }

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
            panel.Add(BuildAgentSettingsCard());
            panel.Add(BuildAiJiraCard());
            panel.Add(BuildGitSettingsCard());

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
            panel.RegisterCallback<PointerDownEvent>(
                OnStyledDropdownPointerDown,
                TrickleDown.TrickleDown);
            return panel;
        }

        /// <summary>
        /// The local agent's configuration: CLI, model, project instructions, env file
        /// and token budget.
        /// </summary>
        /// <remarks>
        /// Built by its own view rather than inline here. These are the fields the
        /// agent tab used to carry, and they were crowding out the conversation.
        /// </remarks>
        private VisualElement BuildAgentSettingsCard()
        {
            _agentSettings = new AgentSettingsView(Repaint, () =>
            {
                _activeTab = Tab.Settings;
                CreateGUI();
            });

            return _agentSettings.Build();
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

        // --- Git / GitHub integration --------------------------------------

        private VisualElement BuildGitSettingsCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var title = new Label(L.Tr(L.K.GitSettingsTitle));
            JiraStyles.ApplySectionTitle(title);
            card.Add(title);

            var note = new Label(L.Tr(L.K.GitSettingsNote));
            JiraStyles.ApplyMuted(note);
            note.style.marginBottom = 12;
            note.style.whiteSpace = WhiteSpace.Normal;
            card.Add(note);

            var enableToggle = new Toggle(L.Tr(L.K.GitEnableToggle)) { value = JiraPreferences.GitEnabled };
            enableToggle.style.marginBottom = 8;
            enableToggle.RegisterValueChangedCallback(evt =>
            {
                JiraPreferences.GitEnabled = evt.newValue;
                RefreshResolveGitCard();
            });
            card.Add(enableToggle);

            var repoField = new TextField(L.Tr(L.K.GitRepoPathLabel)) { value = JiraPreferences.GitRepoPath };
            JiraStyles.ApplyField(repoField);
            repoField.RegisterValueChangedCallback(evt =>
            {
                JiraPreferences.GitRepoPath = evt.newValue?.Trim();
                _gitRepoRootCache = null;
            });
            card.Add(repoField);

            var detectStatus = new Label();
            JiraStyles.ApplyFieldHint(detectStatus);
            detectStatus.style.whiteSpace = WhiteSpace.Normal;

            var detectButton = new Button(async () =>
            {
                detectStatus.text = L.Tr(L.K.MsgGitWorking);
                string root = await ResolveGitRepoRootAsync(true);
                detectStatus.text = string.IsNullOrEmpty(root)
                    ? L.Tr(L.K.MsgGitRepoNotFound)
                    : L.Tr(L.K.MsgGitRepoDetected, root);
            })
            { text = L.Tr(L.K.BtnDetectRepo) };
            JiraStyles.ApplyGhostButton(detectButton);
            card.Add(detectButton);
            card.Add(detectStatus);

            var baseField = new TextField(L.Tr(L.K.GitBaseBranchLabel)) { value = JiraPreferences.GitBaseBranch };
            JiraStyles.ApplyField(baseField);
            baseField.RegisterValueChangedCallback(evt => JiraPreferences.GitBaseBranch = evt.newValue?.Trim());
            card.Add(baseField);

            var branchTemplateField = new TextField(L.Tr(L.K.GitBranchTemplateLabel))
            {
                value = JiraPreferences.GitBranchTemplate
            };
            JiraStyles.ApplyField(branchTemplateField);
            branchTemplateField.RegisterValueChangedCallback(evt => JiraPreferences.GitBranchTemplate = evt.newValue);
            card.Add(branchTemplateField);

            var commitTemplateField = new TextField(L.Tr(L.K.GitCommitTemplateLabel))
            {
                value = JiraPreferences.GitCommitTemplate
            };
            JiraStyles.ApplyField(commitTemplateField);
            commitTemplateField.RegisterValueChangedCallback(evt => JiraPreferences.GitCommitTemplate = evt.newValue);
            card.Add(commitTemplateField);

            var templateHint = new Label(L.Tr(L.K.GitTemplateHint));
            JiraStyles.ApplyFieldHint(templateHint);
            card.Add(templateHint);

            var linkNote = new Label(L.Tr(L.K.GitNativeLinkNote));
            JiraStyles.ApplyMuted(linkNote);
            linkNote.style.marginTop = 12;
            linkNote.style.marginBottom = 8;
            linkNote.style.whiteSpace = WhiteSpace.Normal;
            card.Add(linkNote);

            var installButton = new Button(
                () => Application.OpenURL("https://github.com/marketplace/jira-software-github"))
            {
                text = L.Tr(L.K.BtnInstallGithubJira)
            };
            JiraStyles.ApplySecondaryButton(installButton);
            card.Add(installButton);

            return card;
        }

        private VisualElement BuildResolveGitCard()
        {
            _gitCard = new VisualElement();
            JiraStyles.ApplyNestedCard(_gitCard);
            _gitCard.style.marginTop = 10;
            _gitCard.style.marginBottom = 10;

            var title = new Label(L.Tr(L.K.GitCardTitle));
            JiraStyles.ApplyDynamicFieldLabel(title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _gitCard.Add(title);

            _gitCurrentBranchLabel = new Label();
            JiraStyles.ApplyFieldHint(_gitCurrentBranchLabel);
            _gitCurrentBranchLabel.style.marginTop = 0;
            _gitCard.Add(_gitCurrentBranchLabel);

            _gitTypeDropdown = new DropdownField(L.Tr(L.K.GitTypeLabel))
            {
                choices = new List<string>(GitConventions.Types)
            };
            JiraStyles.ApplyDropdown(_gitTypeDropdown);
            _gitTypeDropdown.SetValueWithoutNotify(GitConventions.Types[0]);
            _gitTypeDropdown.RegisterValueChangedCallback(_ =>
            {
                _gitTypeUserPicked = true;
                UpdateGitPreviews();
            });
            _gitCard.Add(_gitTypeDropdown);

            _gitBranchPreview = new Label();
            _gitBranchPreview.style.whiteSpace = WhiteSpace.Normal;
            _gitBranchPreview.style.marginTop = 6;
            _gitBranchPreview.style.color = new StyleColor(new Color32(238, 240, 244, 255));
            _gitCard.Add(_gitBranchPreview);

            _gitCommitPreview = new Label();
            _gitCommitPreview.style.whiteSpace = WhiteSpace.Normal;
            _gitCommitPreview.style.marginTop = 2;
            _gitCommitPreview.style.marginBottom = 8;
            _gitCommitPreview.style.color = new StyleColor(new Color32(238, 240, 244, 255));
            _gitCard.Add(_gitCommitPreview);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            var createBtn = new Button(CreateOrCheckoutBranchClickedAsync) { text = L.Tr(L.K.BtnGitCreateBranch) };
            JiraStyles.ApplySecondaryButton(createBtn);
            createBtn.style.marginRight = 8;

            var copyCommit = new Button(CopyGitCommit) { text = L.Tr(L.K.BtnGitCopyCommit) };
            JiraStyles.ApplyGhostButton(copyCommit);
            copyCommit.style.marginRight = 8;

            var copyBranch = new Button(CopyGitBranch) { text = L.Tr(L.K.BtnGitCopyBranch) };
            JiraStyles.ApplyGhostButton(copyBranch);

            row.Add(createBtn);
            row.Add(copyCommit);
            row.Add(copyBranch);
            _gitCard.Add(row);

            _gitStatus = new Label();
            _gitStatus.style.display = DisplayStyle.None;
            _gitCard.Add(_gitStatus);

            _gitCard.style.display = DisplayStyle.None;
            return _gitCard;
        }

        private void RefreshResolveGitCard()
        {
            if (_gitCard == null)
                return;

            bool show = JiraPreferences.GitEnabled && _selectedIssue != null;
            _gitCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            if (!_gitTypeUserPicked)
            {
                string suggested = GitConventions.DefaultTypeFor(_selectedIssue.fields?.issuetype);
                int index = Array.IndexOf(GitConventions.Types, suggested);
                _gitTypeDropdown.SetValueWithoutNotify(
                    index >= 0 ? GitConventions.Types[index] : GitConventions.Types[0]);
            }

            HideGitStatus();
            UpdateGitPreviews();
            RefreshGitCurrentBranchAsync();
        }

        private void UpdateGitPreviews()
        {
            if (_selectedIssue == null || _gitBranchPreview == null || _gitCommitPreview == null)
                return;

            string type = CurrentGitType();
            _gitBranchPreview.text = "» " + GitConventions.BuildBranch(
                JiraPreferences.GitBranchTemplate, type, _selectedIssue.key, _selectedIssue.Summary);
            _gitCommitPreview.text = "» " + GitConventions.BuildCommit(
                JiraPreferences.GitCommitTemplate, type, _selectedIssue.key, _selectedIssue.Summary);
        }

        private string CurrentGitType()
        {
            string value = _gitTypeDropdown?.value;
            return string.IsNullOrWhiteSpace(value) ? GitConventions.Types[0] : value;
        }

        private async void RefreshGitCurrentBranchAsync()
        {
            if (_gitCurrentBranchLabel == null)
                return;

            string root = await ResolveGitRepoRootAsync(false);
            if (string.IsNullOrEmpty(root))
            {
                _gitCurrentBranchLabel.text = L.Tr(L.K.MsgGitRepoNotFound);
                return;
            }

            string branch = await GitClient.GetCurrentBranchAsync(root);
            _gitCurrentBranchLabel.text = string.IsNullOrEmpty(branch)
                ? string.Empty
                : L.Tr(L.K.GitCurrentBranch, branch);
        }

        private async Task<string> ResolveGitRepoRootAsync(bool forceRefresh)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_gitRepoRootCache))
                return _gitRepoRootCache;

            string overridePath = JiraPreferences.GitRepoPath;
            string startDir = string.IsNullOrWhiteSpace(overridePath)
                ? Path.GetDirectoryName(Application.dataPath) // project root (parent of /Assets)
                : overridePath;

            string root = await GitClient.GetRepoRootAsync(startDir);
            _gitRepoRootCache = root;
            return root;
        }

        private async void CreateOrCheckoutBranchClickedAsync()
        {
            if (_gitBusy || _selectedIssue == null)
                return;

            string root = await ResolveGitRepoRootAsync(true);
            if (string.IsNullOrEmpty(root))
            {
                SetGitStatus(L.Tr(L.K.MsgGitRepoNotFound), false);
                return;
            }

            string branch = GitConventions.BuildBranch(
                JiraPreferences.GitBranchTemplate, CurrentGitType(), _selectedIssue.key, _selectedIssue.Summary);

            _gitBusy = true;
            SetGitStatus(L.Tr(L.K.MsgGitWorking), true);
            try
            {
                GitResult result = await GitClient.CreateOrCheckoutBranchAsync(
                    root, branch, JiraPreferences.GitBaseBranch);

                if (GitClient.IsGitMissing(result))
                {
                    SetGitStatus(L.Tr(L.K.MsgGitNotInstalled), false);
                    return;
                }

                if (result.Success)
                {
                    SetGitStatus(L.Tr(L.K.MsgGitBranchReady, branch), true);
                    RefreshGitCurrentBranchAsync();
                }
                else
                {
                    SetGitStatus(L.Tr(L.K.MsgGitBranchFailed, result.ShortMessage), false);
                }
            }
            finally
            {
                _gitBusy = false;
            }
        }

        private void CopyGitCommit()
        {
            if (_selectedIssue == null)
                return;

            EditorGUIUtility.systemCopyBuffer = GitConventions.BuildCommit(
                JiraPreferences.GitCommitTemplate, CurrentGitType(), _selectedIssue.key, _selectedIssue.Summary);
            SetGitStatus(L.Tr(L.K.MsgGitCopiedCommit), true);
        }

        private void CopyGitBranch()
        {
            if (_selectedIssue == null)
                return;

            EditorGUIUtility.systemCopyBuffer = GitConventions.BuildBranch(
                JiraPreferences.GitBranchTemplate, CurrentGitType(), _selectedIssue.key, _selectedIssue.Summary);
            SetGitStatus(L.Tr(L.K.MsgGitCopiedBranch), true);
        }

        private void SetGitStatus(string message, bool success)
        {
            if (_gitStatus == null)
                return;

            _gitStatus.text = message;
            _gitStatus.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyStatus(_gitStatus, success);
        }

        private void HideGitStatus()
        {
            if (_gitStatus != null)
                _gitStatus.style.display = DisplayStyle.None;
        }

        private void ClearPresets()
        {
            JiraPreferences.ClearPresets();
            ShowStatus(L.Tr(L.K.MsgPresetsCleared), true);
        }

        private void ClearConnectionData()
        {
            _connectionValidationVersion++;
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
            if (client == null || !_isConnected)
                return;

            int version = ++_projectLoadVersion;
            _projectsLoading = true;
            _projectsLoaded = false;
            bool delegatedToProjectSelection = false;
            SetDestinationLoading(true);
            SetCreateStatus(L.Tr(L.K.MsgLoadingProjects), true);
            try
            {
                List<JiraProject> projects = await client.GetProjectsAsync();
                if (version != _projectLoadVersion || !_isConnected)
                    return;

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
                delegatedToProjectSelection = true;
                OnProjectSelected();
            }
            catch (Exception exception)
            {
                if (version != _projectLoadVersion)
                    return;

                _projectsLoaded = false;
                SetCreateStatus(exception.Message, false);
            }
            finally
            {
                if (version == _projectLoadVersion)
                {
                    _projectsLoading = false;
                    if (!delegatedToProjectSelection)
                        SetDestinationLoading(false);
                }
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

            int selectionVersion = ++_projectSelectionVersion;
            string projectKey = project.key;
            SetDestinationLoading(true);
            _openIssueButton.style.display = DisplayStyle.None;

            try
            {
                List<JiraIssueType> types = await client.GetIssueTypesAsync(projectKey);
                if (selectionVersion != _projectSelectionVersion ||
                    SelectedProject()?.key != projectKey)
                {
                    return;
                }

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
                {
                    HideCreateStatus();
                    OnTypeSelected();
                }
            }
            catch (Exception exception)
            {
                if (selectionVersion != _projectSelectionVersion)
                    return;
                SetCreateStatus(exception.Message, false);
            }

            try
            {
                _assignableUsers.Clear();
                _assignableUsers.AddRange(await client.GetAssignableUsersAsync(projectKey));
                if (selectionVersion != _projectSelectionVersion)
                    return;

                if (_assigneeDropdown != null)
                    RefreshAssigneeChoices(string.Empty, SelectedAssigneeAccountId());
            }
            catch { }

            if (_myself == null)
            {
                try { _myself = await client.GetMyselfAsync(); } catch { }
            }

            if (selectionVersion == _projectSelectionVersion)
                await LoadBoardDataAsync(client, projectKey);

            if (selectionVersion == _projectSelectionVersion)
                SetDestinationLoading(false);
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

            bool canCreate = CanHaveSubtasks(issueType) &&
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

        private static bool CanHaveSubtasks(JiraIssueType issueType)
        {
            // Some Jira projects omit hierarchyLevel in search/edit responses,
            // so an unknown type should not disable the action before the
            // create metadata can confirm whether a subtask type is available.
            if (issueType == null)
                return true;

            if (issueType.subtask || issueType.hierarchyLevel != 0)
            {
                return false;
            }

            string name = issueType.name?.Trim();
            return !string.Equals(
                       name,
                       "Epic",
                       StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(
                       name,
                       "Épico",
                       StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(
                       name,
                       "Epico",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanHaveDirectChildren(JiraIssueType issueType)
        {
            if (issueType == null)
                return true;

            return !issueType.subtask &&
                   issueType.hierarchyLevel >= 0;
        }

        private static bool IsHigherLevelIssueType(
            JiraIssueType issueType)
        {
            if (issueType == null)
                return false;
            if (issueType.hierarchyLevel > 0)
                return true;

            string name = issueType.name?.Trim();
            return string.Equals(
                       name,
                       "Epic",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       name,
                       "Épico",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       name,
                       "Epico",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static List<JiraIssueType> FindDirectChildTypes(
            JiraIssueType parentType,
            IEnumerable<JiraIssueType> issueTypes)
        {
            var matches = new List<JiraIssueType>();
            if (issueTypes == null)
                return matches;

            bool higherLevelParent =
                IsHigherLevelIssueType(parentType);
            int parentLevel = higherLevelParent
                ? Math.Max(1, parentType?.hierarchyLevel ?? 1)
                : 0;
            int childLevel = parentLevel - 1;
            var ids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (JiraIssueType issueType in issueTypes)
            {
                if (issueType == null ||
                    string.IsNullOrWhiteSpace(issueType.id))
                {
                    continue;
                }

                bool directChild = parentLevel > 0
                    ? !issueType.subtask &&
                      issueType.hierarchyLevel == childLevel
                    : issueType.subtask ||
                      issueType.hierarchyLevel < 0;
                if (directChild && ids.Add(issueType.id))
                    matches.Add(issueType);
            }

            // Some create-metadata responses omit hierarchyLevel. For an
            // Epic-level parent, standard non-subtask types are level zero.
            if (matches.Count == 0 && parentLevel > 0 &&
                childLevel == 0)
            {
                foreach (JiraIssueType issueType in issueTypes)
                {
                    if (issueType == null ||
                        issueType.subtask ||
                        IsHigherLevelIssueType(issueType) ||
                        string.IsNullOrWhiteSpace(issueType.id) ||
                        !ids.Add(issueType.id))
                    {
                        continue;
                    }

                    matches.Add(issueType);
                }
            }

            return matches;
        }

        private static int FindPreferredChildTypeIndex(
            IReadOnlyList<JiraIssueType> issueTypes,
            string preferredTypeId)
        {
            if (issueTypes == null || issueTypes.Count == 0)
                return 0;

            if (!string.IsNullOrWhiteSpace(preferredTypeId))
            {
                for (int i = 0; i < issueTypes.Count; i++)
                {
                    if (string.Equals(
                        issueTypes[i]?.id,
                        preferredTypeId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            for (int i = 0; i < issueTypes.Count; i++)
            {
                if (IsStoryIssueType(issueTypes[i]))
                    return i;
            }

            return 0;
        }

        private JiraIssueType FindQuickSubtaskType()
        {
            return FindSubtaskType(_issueTypes);
        }

        private static JiraIssueType FindSubtaskType(
            IEnumerable<JiraIssueType> issueTypes)
        {
            JiraIssueType fallback = null;
            if (issueTypes == null)
                return null;

            foreach (JiraIssueType issueType in issueTypes)
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

        private static string ProjectKeyFromIssueKey(string issueKey)
        {
            if (string.IsNullOrWhiteSpace(issueKey))
                return string.Empty;

            int separator = issueKey.IndexOf('-');
            return separator > 0
                ? issueKey.Substring(0, separator)
                : string.Empty;
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
                try
                {
                    _sprints.AddRange(
                        await client.GetAvailableSprintsAsync(
                            _activeBoardId));
                }
                catch { }
            }

            PopulateEpicChoices();
            PopulateSprintChoices();
            RefreshAdditionalSprintFields();
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

        private void RefreshAdditionalSprintFields()
        {
            foreach (AdditionalFieldBinding binding in
                     _additionalFields)
            {
                if (binding?.Dropdown == null ||
                    !IsSprintField(binding.Meta))
                {
                    continue;
                }

                JiraSprint selected =
                    SelectedAdditionalSprint(binding);
                PopulateAdditionalSprintDropdown(
                    binding.Dropdown,
                    binding.Meta.required,
                    selected?.id);
            }
        }

        private void PopulateAdditionalSprintDropdown(
            DropdownField dropdown,
            bool required,
            int? preferredSprintId)
        {
            if (dropdown == null)
                return;

            var available = new List<JiraSprint>(_sprints);
            dropdown.userData = available;
            if (available.Count == 0)
            {
                var unavailable = new List<string>
                {
                    L.Tr(L.K.DropdownNoOptions)
                };
                dropdown.choices = unavailable;
                dropdown.SetValueWithoutNotify(unavailable[0]);
                dropdown.SetEnabled(false);
                return;
            }

            var labels = new List<string>();
            int selectedIndex = 0;
            if (!required)
                labels.Add(L.Tr(L.K.NoneOption));

            for (int i = 0; i < available.Count; i++)
            {
                JiraSprint sprint = available[i];
                labels.Add(sprint.name);
                if (preferredSprintId.HasValue &&
                    sprint.id == preferredSprintId.Value)
                {
                    selectedIndex = i + (required ? 0 : 1);
                }
            }

            dropdown.choices = labels;
            dropdown.SetValueWithoutNotify(
                labels[Mathf.Clamp(
                    selectedIndex,
                    0,
                    labels.Count - 1)]);
            dropdown.SetEnabled(true);
        }

        private static JiraSprint SelectedAdditionalSprint(
            AdditionalFieldBinding binding)
        {
            if (binding?.Dropdown == null ||
                !IsSprintField(binding.Meta))
            {
                return null;
            }

            var sprints =
                binding.Dropdown.userData as List<JiraSprint>;
            int index = binding.Dropdown.index -
                        (binding.Meta.required ? 0 : 1);
            return sprints != null &&
                   index >= 0 &&
                   index < sprints.Count
                ? sprints[index]
                : null;
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

            ResetQuickSubtasks();
            ClearQuickSubtaskMetadata();
            _parentContainer.style.display = isSubtask ? DisplayStyle.Flex : DisplayStyle.None;
            _epicContainer.style.display = isSubtask ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateQuickSubtaskVisibility(type);
            _parentField.label = isSubtask
                ? RequiredLabel(L.Tr(L.K.FieldParent))
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

                JiraIssueType quickSubtaskType =
                    CanHaveSubtasks(type)
                        ? FindQuickSubtaskType()
                        : null;
                List<JiraFieldMeta> quickSubtaskFields =
                    new List<JiraFieldMeta>();
                if (quickSubtaskType != null)
                {
                    try
                    {
                        quickSubtaskFields =
                            await client.GetCreateFieldsAsync(
                                projectKey,
                                quickSubtaskType.id);
                    }
                    catch
                    {
                        quickSubtaskFields.Clear();
                    }
                }

                if (loadVersion != _fieldLoadVersion)
                    return;

                JiraFieldMeta priorityMeta =
                    FindById(fields, FieldPriority);
                JiraFieldMeta quickPriorityMeta =
                    FindById(quickSubtaskFields, FieldPriority);
                bool needsPriorities =
                    (priorityMeta != null &&
                     !priorityMeta.HasAllowedValues) ||
                    (quickPriorityMeta != null &&
                     !quickPriorityMeta.HasAllowedValues);
                if (needsPriorities)
                {
                    List<JiraAllowedValue> priorities =
                        await client.GetPrioritiesAsync();

                    if (loadVersion != _fieldLoadVersion)
                        return;

                    if (priorities.Count > 0)
                    {
                        if (priorityMeta != null &&
                            !priorityMeta.HasAllowedValues)
                        {
                            priorityMeta.allowedValues =
                                priorities.ToArray();
                        }
                        if (quickPriorityMeta != null &&
                            !quickPriorityMeta.HasAllowedValues)
                        {
                            quickPriorityMeta.allowedValues =
                                priorities.ToArray();
                        }
                    }
                }

                ConfigureQuickSubtaskMetadata(
                    quickSubtaskType,
                    quickSubtaskFields);
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
            _modulesAreLoading = loading;
            if (_dynamicFieldsLoadingPanel != null)
            {
                _dynamicFieldsLoadingPanel.style.display = loading
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            if (_classifyLoader != null)
            {
                _classifyLoader.style.display = loading
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            UpdateLoaderAnimationState();
            if (_createButton != null)
                _createButton.SetEnabled(!_isCreating && !_areFieldsLoading);
            if (_quickSubtaskContainer != null)
                _quickSubtaskContainer.SetEnabled(!loading && !_isCreating);
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
            _assigneeResults = null; _assigneeSelectedLabel = null;
            _filteredAssignableUsers.Clear();
            _startDateField = null; _startDateMeta = null;
            _dueDateField = null; _dueDateMeta = null;
            _descriptionMeta = null;
            _descriptionField.label = L.Tr(L.K.FieldDescription);
            _additionalFields.Clear();
            _associatedItemDropdowns.Clear();

            _classifyContent.Clear();
            _datesContent.Clear();
            _additionalFieldsContent.Clear();
            _datesContent.style.display = DisplayStyle.None;
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

            _datesContent.style.display = _datesContent.childCount > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

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
            if (string.Equals(
                    meta.fieldId,
                    FieldPriority,
                    StringComparison.Ordinal))
            {
                ConfigurePriorityDropdownIcon(dropdown);
            }
            return dropdown;
        }

        private static DropdownField BuildTeamDropdown(
            JiraFieldMeta meta,
            string preferredTeamId)
        {
            var dropdown = new DropdownField(
                FieldLabel(meta, L.Tr(L.K.FieldTeam)));
            PopulateTeamDropdown(dropdown, meta, preferredTeamId);
            JiraStyles.ApplyDropdown(dropdown);
            return dropdown;
        }

        private static void PopulateTeamDropdown(
            DropdownField dropdown,
            JiraFieldMeta meta,
            string preferredTeamId)
        {
            if (dropdown == null || meta == null)
                return;

            var labels = new List<string>();
            int selectedIndex = 0;

            if (!meta.required)
                labels.Add(L.Tr(L.K.NoneOption));

            for (int i = 0; i < meta.allowedValues.Length; i++)
            {
                JiraAllowedValue value = meta.allowedValues[i];
                labels.Add(value.Display);
                if (string.Equals(
                        AllowedValueIdentifier(value),
                        preferredTeamId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + (meta.required ? 0 : 1);
                }
            }

            dropdown.choices = labels;
            if (labels.Count > 0)
            {
                dropdown.SetValueWithoutNotify(
                    labels[Mathf.Clamp(
                        selectedIndex,
                        0,
                        labels.Count - 1)]);
            }
            dropdown.SetEnabled(labels.Count > 0);
        }

        private string SelectedCreateTeamId(
            JiraFieldMeta childTeamMeta)
        {
            AdditionalFieldBinding fallback = null;
            foreach (AdditionalFieldBinding binding in _additionalFields)
            {
                if (binding?.Meta == null ||
                    !IsTeamField(binding.Meta))
                {
                    continue;
                }

                if (string.Equals(
                        binding.Meta.fieldId,
                        childTeamMeta?.fieldId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SelectedTeamId(
                        binding.Meta,
                        binding.Dropdown,
                        binding.TextField);
                }

                if (fallback == null)
                    fallback = binding;
            }

            return fallback != null
                ? SelectedTeamId(
                    fallback.Meta,
                    fallback.Dropdown,
                    fallback.TextField)
                : null;
        }

        private static string SelectedTeamId(
            JiraFieldMeta meta,
            DropdownField dropdown,
            TextField textField)
        {
            if (meta == null)
                return null;

            if (dropdown != null)
            {
                int allowedIndex = meta.required
                    ? dropdown.index
                    : dropdown.index - 1;
                return AllowedValueIdentifier(
                    AllowedAt(meta, allowedIndex));
            }

            return textField?.value?.Trim();
        }

        private VisualElement BuildAssigneeWidget(string label, string presetAccountId)
        {
            var container = new VisualElement();

            EnsureMyselfInAssignable();

            _assigneeSearchField = new TextField(L.Tr(L.K.FieldAssigneeSearch));
            JiraStyles.ApplyField(_assigneeSearchField);
            _assigneeSearchField.style.marginBottom = 4;
            container.Add(_assigneeSearchField);

            // Inline clickable results — no need to open a dropdown to pick.
            _assigneeResults = new VisualElement();
            container.Add(_assigneeResults);

            _assigneeSelectedLabel = new Label();
            JiraStyles.ApplyFieldHint(_assigneeSelectedLabel);
            _assigneeSelectedLabel.style.marginTop = 2;
            container.Add(_assigneeSelectedLabel);

            // Hidden backing store read by ApplyDynamicFields / SavePresets.
            _assigneeDropdown = new DropdownField(label);
            _assigneeDropdown.style.display = DisplayStyle.None;
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

        private void RenderAssigneeResults(string query)
        {
            if (_assigneeResults == null)
                return;

            _assigneeResults.Clear();
            if (string.IsNullOrWhiteSpace(query))
                return;

            int shown = 0;
            for (int i = 0; i < _filteredAssignableUsers.Count; i++)
            {
                int captured = i;
                var button = new Button(() => SelectAssignee(captured))
                {
                    text = AssigneeDisplay(_filteredAssignableUsers[i])
                };
                JiraStyles.ApplyGhostButton(button);
                button.style.marginBottom = 3;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                _assigneeResults.Add(button);

                if (++shown >= 6)
                    break;
            }
        }

        private void SelectAssignee(int filteredIndex)
        {
            if (_assigneeDropdown == null ||
                filteredIndex < 0 || filteredIndex >= _filteredAssignableUsers.Count)
                return;

            _assigneeDropdown.index = filteredIndex + 1; // +1 for the "None" entry
            JiraUser user = _filteredAssignableUsers[filteredIndex];
            _assigneeSearchField?.SetValueWithoutNotify(user.displayName ?? string.Empty);
            _assigneeResults?.Clear();
            UpdateAssigneeSelectedLabel();
        }

        private void UpdateAssigneeSelectedLabel()
        {
            if (_assigneeSelectedLabel == null)
                return;

            string accountId = SelectedAssigneeAccountId();
            if (string.IsNullOrWhiteSpace(accountId))
            {
                _assigneeSelectedLabel.text = L.Tr(L.K.AssigneeNone);
                return;
            }

            JiraUser user = _assignableUsers.Find(u => u != null && u.accountId == accountId);
            _assigneeSelectedLabel.text = user != null ? AssigneeDisplay(user) : accountId;
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

            RenderAssigneeResults(normalizedQuery);
            UpdateAssigneeSelectedLabel();
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

            if (IsSprintField(meta))
            {
                binding.Dropdown = new DropdownField(label);
                JiraStyles.ApplyDropdown(binding.Dropdown);
                PopulateAdditionalSprintDropdown(
                    binding.Dropdown,
                    meta.required,
                    null);
                _additionalFieldsContent.Add(binding.Dropdown);

                var sprintHint =
                    new Label(L.Tr(L.K.SprintFieldHint));
                JiraStyles.ApplyFieldHint(sprintHint);
                _additionalFieldsContent.Add(sprintHint);
            }
            else if (IsIssueAssociationField(meta))
            {
                binding.TextField = new TextField();
                binding.TextField.style.display = DisplayStyle.None;
                _additionalFieldsContent.Add(binding.TextField);

                binding.Dropdown = new DropdownField(label)
                {
                    choices = new List<string>
                    {
                        L.Tr(L.K.NoneOption)
                    }
                };
                binding.Dropdown.SetValueWithoutNotify(
                    L.Tr(L.K.NoneOption));
                JiraStyles.ApplyDropdown(binding.Dropdown);
                _associatedItemDropdowns[binding.Dropdown] =
                    binding;
                _additionalFieldsContent.Add(binding.Dropdown);

                var associatedHint = new Label(
                    L.Tr(L.K.AssociatedItemsSearchHint));
                JiraStyles.ApplyFieldHint(associatedHint);
                _additionalFieldsContent.Add(associatedHint);
            }
            else if (meta.HasAllowedValues && type == "array")
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
                else if (IsTeamField(meta))
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

            _assigneeResults?.Clear();
            UpdateAssigneeSelectedLabel();
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

        private static JiraFieldMeta FindActivityWeightField(
            List<JiraFieldMeta> fields)
        {
            if (fields == null)
                return null;

            foreach (JiraFieldMeta field in fields)
            {
                if (field == null ||
                    !string.Equals(
                        field.schema?.type,
                        "number",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = field.name ?? string.Empty;
                string custom = field.schema?.custom ?? string.Empty;
                if (ContainsIgnoreCase(name, "story point") ||
                    ContainsIgnoreCase(name, "ponto") ||
                    ContainsIgnoreCase(name, "peso") ||
                    ContainsIgnoreCase(name, "weight") ||
                    ContainsIgnoreCase(custom, "story-point"))
                {
                    return field;
                }
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
            return meta != null && meta.required
                ? RequiredLabel(label)
                : label;
        }

        private static string RequiredLabel(string label)
        {
            return (label ?? string.Empty) +
                   " <color=#FF5656>*</color>";
        }

        private bool TryCollectQuickSubtasks(
            out List<QuickSubtaskInput> inputs,
            out string error)
        {
            inputs = new List<QuickSubtaskInput>();
            error = null;

            if (_quickSubtasks.Count == 0)
                return true;

            foreach (QuickSubtaskBinding binding in _quickSubtasks)
            {
                string title = binding.Title?.value?.Trim();
                string description = binding.Description?.value?.Trim();
                string assigneeAccountId =
                    SelectedDropdownAssigneeAccountId(
                        binding.Assignee);
                string teamId = SelectedTeamId(
                    _quickSubtaskTeamMeta,
                    binding.Team,
                    binding.TeamText);
                string startDate = binding.StartDate?.value?.Trim();
                string dueDate = binding.DueDate?.value?.Trim();
                if (string.IsNullOrWhiteSpace(title) &&
                    string.IsNullOrWhiteSpace(description) &&
                    string.IsNullOrWhiteSpace(assigneeAccountId) &&
                    string.IsNullOrWhiteSpace(startDate) &&
                    string.IsNullOrWhiteSpace(dueDate) &&
                    string.IsNullOrWhiteSpace(binding.AttachmentPath))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    error = L.Tr(L.K.MsgQuickSubtaskTitleRequired);
                    return false;
                }
                if (_quickSubtaskDescriptionMeta?.required == true &&
                    string.IsNullOrWhiteSpace(description))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskDescriptionMeta.name);
                    return false;
                }
                if (!TryNormalizeDateInput(
                        startDate,
                        out startDate))
                {
                    error = L.Tr(
                        L.K.MsgInvalidDate,
                        _quickSubtaskStartDateMeta?.name ??
                        L.Tr(L.K.FieldStartDate));
                    return false;
                }
                if (!TryNormalizeDateInput(
                        dueDate,
                        out dueDate))
                {
                    error = L.Tr(
                        L.K.MsgInvalidDate,
                        _quickSubtaskDueDateMeta?.name ??
                        L.Tr(L.K.FieldDueDate));
                    return false;
                }

                inputs.Add(new QuickSubtaskInput
                {
                    Title = title,
                    Description = description,
                    PriorityId = AllowedAt(
                        _quickSubtaskPriorityMeta,
                        binding.Priority?.index ?? -1)?.id,
                    TeamId = teamId,
                    AssigneeAccountId = assigneeAccountId,
                    StartDate = startDate,
                    DueDate = dueDate,
                    AttachmentPath = binding.AttachmentPath
                });

                QuickSubtaskInput input = inputs[inputs.Count - 1];
                if (_quickSubtaskPriorityMeta?.required == true &&
                    string.IsNullOrWhiteSpace(input.PriorityId))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskPriorityMeta.name);
                    return false;
                }
                if (_quickSubtaskAssigneeMeta?.required == true &&
                    string.IsNullOrWhiteSpace(
                        input.AssigneeAccountId))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskAssigneeMeta.name);
                    return false;
                }
                if (_quickSubtaskTeamMeta?.required == true &&
                    string.IsNullOrWhiteSpace(input.TeamId))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskTeamMeta.name);
                    return false;
                }
                if (_quickSubtaskStartDateMeta?.required == true &&
                    string.IsNullOrWhiteSpace(input.StartDate))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskStartDateMeta.name);
                    return false;
                }
                if (_quickSubtaskDueDateMeta?.required == true &&
                    string.IsNullOrWhiteSpace(input.DueDate))
                {
                    error = L.Tr(
                        L.K.MsgRequiredField,
                        _quickSubtaskDueDateMeta.name);
                    return false;
                }
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

            JiraIssueType quickSubtaskType = null;
            if (quickSubtasks.Count > 0)
            {
                quickSubtaskType =
                    _quickSubtaskType ?? FindQuickSubtaskType();
                if (quickSubtaskType == null)
                {
                    try
                    {
                        List<JiraIssueType> currentTypes =
                            await client.GetIssueTypesAsync(project.key);
                        quickSubtaskType =
                            FindSubtaskType(currentTypes);
                    }
                    catch
                    {
                        quickSubtaskType = null;
                    }
                }

                if (quickSubtaskType == null)
                {
                    SetCreateStatus(
                        L.Tr(L.K.MsgSubtaskTypeUnavailable),
                        false);
                    return;
                }
            }

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
                bool allQuickSubtasksCreated = true;

                if (quickSubtasks.Count > 0 &&
                    quickSubtaskType != null)
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
                        subtaskDraft.SetFieldId(
                            FieldPriority,
                            quickSubtask.PriorityId);
                        ApplyTeamField(
                            subtaskDraft,
                            _quickSubtaskTeamMeta,
                            quickSubtask.TeamId);
                        if (_quickSubtaskAssigneeMeta != null)
                        {
                            subtaskDraft.SetFieldObject(
                                _quickSubtaskAssigneeMeta.fieldId,
                                "accountId",
                                quickSubtask.AssigneeAccountId);
                        }
                        if (_quickSubtaskStartDateMeta != null)
                        {
                            subtaskDraft.SetFieldString(
                                _quickSubtaskStartDateMeta.fieldId,
                                quickSubtask.StartDate);
                        }
                        if (_quickSubtaskDueDateMeta != null)
                        {
                            subtaskDraft.SetFieldString(
                                _quickSubtaskDueDateMeta.fieldId,
                                quickSubtask.DueDate);
                        }

                        JiraCreateIssueResult subtaskResult =
                            await client.CreateIssueAsync(subtaskDraft);
                        if (subtaskResult.Success)
                        {
                            message += L.Tr(
                                L.K.MsgQuickSubtaskCreated,
                                subtaskResult.IssueKey);
                            if (!string.IsNullOrWhiteSpace(
                                    quickSubtask.AttachmentPath))
                            {
                                message +=
                                    await UploadAttachmentAndEmbedImageAsync(
                                        client,
                                        subtaskResult.IssueKey,
                                        quickSubtask.AttachmentPath,
                                        quickSubtask.Description);
                            }
                        }
                        else
                        {
                            allQuickSubtasksCreated = false;
                            message += L.Tr(
                                L.K.MsgQuickSubtaskFailed,
                                quickSubtask.Title,
                                subtaskResult.Message);
                        }
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
                    message += await UploadAttachmentAndEmbedImageAsync(
                        client,
                        result.IssueKey,
                        _attachmentPath,
                        draft.Description);
                }

                SetCreateStatus(message, allQuickSubtasksCreated);
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

        private static async Task<string>
            UploadAttachmentAndEmbedImageAsync(
                JiraClient client,
                string issueKey,
                string filePath,
                string description)
        {
            JiraAttachmentUploadResult upload =
                await client.UploadAttachmentWithResultAsync(
                    issueKey,
                    filePath);
            if (!upload.Success)
            {
                return L.Tr(
                    L.K.MsgAttachmentFailed,
                    upload.Error);
            }

            string message = L.Tr(L.K.MsgAttachmentAdded);
            bool localImage = IsImageFile(filePath);
            JiraAttachmentInfo attachment = upload.Attachment;
            bool uploadedImage =
                attachment?.IsImage == true || localImage;
            if (!uploadedImage)
                return message;

            if (attachment == null ||
                string.IsNullOrWhiteSpace(attachment.content))
            {
                return message + L.Tr(
                    L.K.MsgImageEmbedFailed,
                    "o Jira não retornou a URL do anexo");
            }

            string adf = JiraAdf.BuildTextDocumentWithImages(
                description,
                new[] { attachment });
            string updateError =
                await client.UpdateIssueDescriptionAdfAsync(
                    issueKey,
                    adf);
            return updateError == null
                ? message + L.Tr(L.K.MsgImageEmbedded)
                : message + L.Tr(
                    L.K.MsgImageEmbedFailed,
                    updateError);
        }

        private static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(
                       extension,
                       ".png",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       ".jpg",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       ".jpeg",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       ".gif",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       ".webp",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       extension,
                       ".bmp",
                       StringComparison.OrdinalIgnoreCase);
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
            {
                if (TryNormalizeDateInput(
                        _startDateField.value,
                        out string startDate))
                {
                    draft.SetFieldString(
                        _startDateMeta.fieldId,
                        startDate);
                }
            }

            if (_dueDateMeta != null && _dueDateField != null)
            {
                if (TryNormalizeDateInput(
                        _dueDateField.value,
                        out string dueDate))
                {
                    draft.SetFieldString(
                        _dueDateMeta.fieldId,
                        dueDate);
                }
            }

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

            if (!ValidateDateField(
                    _startDateMeta,
                    _startDateField,
                    out error) ||
                !ValidateDateField(
                    _dueDateMeta,
                    _dueDateField,
                    out error))
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

                if (binding.TextField != null &&
                    IsDateField(binding.Meta) &&
                    !string.IsNullOrWhiteSpace(binding.TextField.value) &&
                    !TryNormalizeDateInput(
                        binding.TextField.value,
                        out _))
                {
                    error = L.Tr(
                        L.K.MsgInvalidDate,
                        binding.Meta.name);
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

        private static bool ValidateDateField(
            JiraFieldMeta meta,
            TextField field,
            out string error)
        {
            if (!ValidateTextField(meta, field, out error))
                return false;

            if (field != null &&
                !string.IsNullOrWhiteSpace(field.value) &&
                !TryNormalizeDateInput(field.value, out _))
            {
                error = L.Tr(
                    L.K.MsgInvalidDate,
                    meta?.name ?? field.label);
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
            {
                if (IsSprintField(binding.Meta))
                    return SelectedAdditionalSprint(binding) != null;

                return binding.Meta.required
                    ? binding.Dropdown.index >= 0
                    : binding.Dropdown.index > 0;
            }

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
                if (IsIssueAssociationField(meta))
                {
                    List<string> issueKeys =
                        ReadAssociatedItemKeys(binding);
                    if (issueKeys.Count > 0)
                    {
                        draft.SetFieldStringArray(
                            meta.fieldId,
                            issueKeys);
                    }
                    return;
                }

                if (IsSprintField(meta))
                {
                    JiraSprint sprint =
                        SelectedAdditionalSprint(binding);
                    if (sprint != null)
                    {
                        draft.SetFieldNumber(
                            meta.fieldId,
                            sprint.id.ToString(
                                CultureInfo.InvariantCulture));
                    }
                    return;
                }

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
            else if (IsDateField(meta))
            {
                if (TryNormalizeDateInput(text, out string date))
                    draft.SetFieldString(meta.fieldId, date);
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

        private static void ApplyTeamField(
            JiraIssueDraft draft,
            JiraFieldMeta meta,
            string teamId)
        {
            if (draft == null ||
                meta == null ||
                string.IsNullOrWhiteSpace(teamId))
            {
                return;
            }

            if (IsAtlassianTeamField(meta))
            {
                draft.SetFieldString(meta.fieldId, teamId);
                return;
            }

            JiraAllowedValue selected = null;
            if (meta.allowedValues != null)
            {
                foreach (JiraAllowedValue value in meta.allowedValues)
                {
                    if (string.Equals(
                            AllowedValueIdentifier(value),
                            teamId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selected = value;
                        break;
                    }
                }
            }

            if (selected != null)
            {
                ApplyAllowedValue(draft, meta.fieldId, selected);
            }
            else if (meta.HasAllowedValues)
            {
                draft.SetFieldId(meta.fieldId, teamId);
            }
            else if (string.Equals(
                         meta.schema?.type,
                         "option",
                         StringComparison.OrdinalIgnoreCase))
            {
                draft.SetFieldValueObject(meta.fieldId, teamId);
            }
            else
            {
                draft.SetFieldString(meta.fieldId, teamId);
            }
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
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return true;

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

            return false;
        }

        private static bool TryNormalizeDateInput(
            string text,
            out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string inputFormat =
                L.Current == L.En
                    ? "yyyy-MM-dd"
                    : "dd-MM-yyyy";
            if (!DateTime.TryParseExact(
                    text.Trim(),
                    inputFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
            {
                return false;
            }

            normalized = date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
            return true;
        }

        private static bool IsDateField(JiraFieldMeta meta)
        {
            string type = meta?.schema?.type;
            return string.Equals(
                       type,
                       "date",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       type,
                       "datetime",
                       StringComparison.OrdinalIgnoreCase);
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

        private static bool IsTeamField(JiraFieldMeta meta)
        {
            if (IsAtlassianTeamField(meta))
                return true;
            if (meta == null ||
                string.IsNullOrWhiteSpace(meta.fieldId) ||
                !meta.fieldId.StartsWith(
                    "customfield_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string name = meta.name?.Trim();
            return string.Equals(
                       name,
                       "Time",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       name,
                       "Team",
                       StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(name) &&
                    (name.StartsWith(
                         "Time ",
                         StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith(
                         "Team ",
                         StringComparison.OrdinalIgnoreCase)));
        }

        private static JiraFieldMeta FindTeamField(
            IEnumerable<JiraFieldMeta> fields)
        {
            if (fields == null)
                return null;

            JiraFieldMeta fallback = null;
            foreach (JiraFieldMeta field in fields)
            {
                if (!IsTeamField(field))
                    continue;

                if (field.required)
                    return field;

                if (fallback == null)
                    fallback = field;
            }

            return fallback;
        }

        private static bool IsSprintField(JiraFieldMeta meta)
        {
            return meta != null &&
                   (ContainsIgnoreCase(
                        meta.schema?.custom,
                        "gh-sprint") ||
                    ContainsIgnoreCase(
                        meta.schema?.items,
                        "sprint") ||
                    ContainsIgnoreCase(
                        meta.name,
                        "sprint"));
        }

        private static bool IsIssueAssociationField(
            JiraFieldMeta meta)
        {
            if (meta == null)
                return false;

            return string.Equals(
                       meta.fieldId,
                       "issuelinks",
                       StringComparison.OrdinalIgnoreCase) ||
                   ContainsIgnoreCase(
                       meta.schema?.system,
                       "issuelinks") ||
                   string.Equals(
                       meta.schema?.items,
                       "issue",
                       StringComparison.OrdinalIgnoreCase) ||
                   ContainsIgnoreCase(
                       meta.schema?.items,
                       "issuelink") ||
                   ContainsIgnoreCase(
                       meta.name,
                       "associad") ||
                   ContainsIgnoreCase(
                       meta.name,
                       "linked issue") ||
                   ContainsIgnoreCase(
                       meta.name,
                       "linked work") ||
                   ContainsIgnoreCase(
                       meta.name,
                       "associated item");
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

        private JiraEpic SelectedResolveEpic()
        {
            return _selectedResolveEpic;
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
            JiraPreferences.Token = token;

            int validationVersion = ++_connectionValidationVersion;
            SetBusy(true);
            ShowStatus(L.Tr(L.K.MsgValidating), true);

            try
            {
                var auth = new JiraBasicTokenAuthProvider(email, token);
                var client = new JiraClient(baseUrl, auth);
                JiraConnectionResult result = await client.TestConnectionAsync();
                if (validationVersion != _connectionValidationVersion)
                    return;

                if (!result.Success)
                {
                    ShowStatus(result.Message, false);
                    _connectedCard.style.display = DisplayStyle.None;
                    SetConnectionAvailability(false);
                    SelectTab(Tab.Connection);
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
                if (validationVersion != _connectionValidationVersion)
                    return;

                ShowStatus(exception.Message, false);
                _connectedCard.style.display = DisplayStyle.None;
                SetConnectionAvailability(false);
                SelectTab(Tab.Connection);
            }
            finally
            {
                if (validationVersion == _connectionValidationVersion)
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
            _connectionValidationVersion++;
            JiraPreferences.ClearToken();
            _tokenField.value = string.Empty;
            _connectedCard.style.display = DisplayStyle.None;
            _projectsLoaded = false;
            SetConnectionAvailability(false);
            ShowStatus(L.Tr(L.K.MsgTokenRemoved), true);
            SelectTab(Tab.Connection);
        }

        private async void RefreshConnectionState(Tab tabAfterValidation)
        {
            int validationVersion = ++_connectionValidationVersion;
            SetConnectionAvailability(false);
            _connectedCard.style.display = DisplayStyle.None;
            SelectTab(Tab.Connection);

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
                if (validationVersion != _connectionValidationVersion)
                    return;

                if (!result.Success)
                {
                    ShowStatus(result.Message, false);
                    SetConnectionAvailability(false);
                    SelectTab(Tab.Connection);
                    return;
                }

                _myself = result.User;
                ShowConnectedUser(result.User);
                ShowStatus(result.Message, true);
                SetConnectionAvailability(true);
                SelectTab(tabAfterValidation);
            }
            catch (Exception exception)
            {
                if (validationVersion != _connectionValidationVersion)
                    return;

                ShowStatus(exception.Message, false);
                _connectedCard.style.display = DisplayStyle.None;
                SetConnectionAvailability(false);
                SelectTab(Tab.Connection);
            }
            finally
            {
                if (validationVersion == _connectionValidationVersion)
                    SetBusy(false);
            }
        }

        private void SetConnectionAvailability(bool connected)
        {
            bool becameConnected = connected && !_isConnected;
            _isConnected = connected;

            if (!connected && _connectedCard != null)
                _connectedCard.style.display = DisplayStyle.None;
            if (_connectionFormCard != null)
            {
                _connectionFormCard.style.display = connected
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (!connected)
                ResetRemoteDataState(true);
            else if (becameConnected)
                ResetRemoteDataState(false);

            if (_createTab != null)
                _createTab.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;
            if (_resolveTab != null)
                _resolveTab.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected && _activeTab == Tab.Resolve && _resolvePanel != null)
                RefreshResolveAvailability();
        }

        private void ResetRemoteDataState(bool clearIdentity)
        {
            _projectLoadVersion++;
            _projectSelectionVersion++;
            _fieldLoadVersion++;
            _issueLoadVersion++;
            _issueDetailLoadVersion++;
            _resolveEpicLoadVersion++;
            _resolveStatusLoadVersion++;

            _projectsLoading = false;
            _projectsLoaded = false;
            _areFieldsLoading = false;
            _fieldsLoaded = false;
            _issuesLoading = false;
            _issuesLoaded = false;
            _resolveStatusesLoading = false;
            _resolveStatusesLoaded = false;
            _resolvePrioritiesLoading = false;
            _resolvePrioritiesLoaded = false;
            _resolveEpicsLoading = false;
            _resolveEpicsLoaded = false;
            SetDestinationLoading(false);
            _modulesAreLoading = false;
            if (_dynamicFieldsLoadingPanel != null)
                _dynamicFieldsLoadingPanel.style.display = DisplayStyle.None;
            if (_classifyLoader != null)
                _classifyLoader.style.display = DisplayStyle.None;
            UpdateLoaderAnimationState();

            _projects.Clear();
            _issueTypes.Clear();
            _epics.Clear();
            _sprints.Clear();
            _assignableUsers.Clear();
            _filteredAssignableUsers.Clear();
            _resolveIssues.Clear();
            _resolveStatuses.Clear();
            _selectedResolveStatus = null;
            _selectedResolveAssignee = null;
            _resolvePriorities.Clear();
            _resolveEpics.Clear();
            _filteredResolveEpics.Clear();
            _selectedResolveEpic = null;
            _resolveProjectKey = null;
            _resolveAssignableProjectKey = null;
            _resolveAssignableUsersLoaded = false;
            _resolveAssignableUsers.Clear();
            _resolveOwnerScope = ResolveOwnerScope.Mine;
            _resolveSprintScope = ResolveSprintScope.All;
            _resolveIssuePage = 0;
            _priorityBusyIssues.Clear();
            _selectedIssue = null;
            _resolveParentIssue = null;
            _resolveParentStack.Clear();
            _resolveSelectedChildren.Clear();
            _resolveAvailableChildTypes.Clear();
            _resolveParentTeamId = string.Empty;
            _resolveParentTeamFieldId = null;
            _activeBoardId = -1;
            _epicsLoadFailed = false;

            if (clearIdentity)
                _myself = null;

            if (_projectDropdown != null)
            {
                _projectDropdown.choices = new List<string>();
                _projectDropdown.SetValueWithoutNotify(string.Empty);
            }

            if (_typeDropdown != null)
            {
                _typeDropdown.choices = new List<string>();
                _typeDropdown.SetValueWithoutNotify(string.Empty);
            }

            if (_epicDropdown != null)
            {
                _epicDropdown.choices = new List<string>();
                _epicDropdown.SetValueWithoutNotify(string.Empty);
            }

            if (_sprintDropdown != null)
            {
                _sprintDropdown.choices = new List<string>();
                _sprintDropdown.SetValueWithoutNotify(string.Empty);
            }

            if (_classifyContent != null && _datesContent != null &&
                _additionalFieldsContent != null && _descriptionField != null)
            {
                ClearDynamicFields();
            }

            if (_fieldsStatusLabel != null)
                _fieldsStatusLabel.style.display = DisplayStyle.None;

            if (_issueListContainer != null)
                _issueListContainer.Clear();
            if (_issuePagination != null)
                _issuePagination.style.display = DisplayStyle.None;
            RefreshResolveStatusDropdown();

            if (_resolveEpicDropdown != null)
            {
                var allEpics = new List<string> { L.Tr(L.K.ResolveAllEpics) };
                _resolveEpicDropdown.choices = allEpics;
                _resolveEpicDropdown.SetValueWithoutNotify(allEpics[0]);
            }

            UpdateResolveEpicSelectedLabel();

            if (_resolveSprintScopeDropdown != null)
            {
                _resolveSprintScopeDropdown.SetValueWithoutNotify(
                    L.Tr(L.K.ResolveSprintAll));
            }

            if (_resolveOwnerScopeDropdown != null)
                RefreshResolveOwnerDropdown();

            if (_issueListStatus != null)
            {
                _issueListStatus.text = L.Tr(L.K.MsgLoadingIssues);
                _issueListStatus.style.display = DisplayStyle.Flex;
            }

            if (_resolveDetailHeader != null)
            {
                _resolveDetailHeader.text = L.Tr(L.K.SelectIssueHint);
                _resolveDetailHeader.tooltip = string.Empty;
            }

            if (_resolveDetailBody != null)
                SetDetailInteractable(false);

            UpdateParentNavigation();
            CloseStatusPopup();
        }

        private void OnDestroy()
        {
            _loaderAnimation?.Pause();
            foreach (QuickSubtaskBinding binding in _quickSubtasks)
            {
                ClearInlineAttachmentPreview(
                    binding?.AttachmentPreview);
            }
            ClearInlineAttachmentPreview(
                _resolveNewSubtaskAttachmentPreview);
            ReleaseAttachmentPreviewTexture();
        }

        private static bool HasCredentials()
        {
            return !string.IsNullOrWhiteSpace(JiraPreferences.BaseUrl)
                && !string.IsNullOrWhiteSpace(JiraPreferences.Email)
                && !string.IsNullOrWhiteSpace(JiraPreferences.Token);
        }

        private static JiraClient BuildClientOrNull()
        {
            if (!HasCredentials())
                return null;

            try
            {
                var auth = new JiraBasicTokenAuthProvider(JiraPreferences.Email, JiraPreferences.Token);
                return new JiraClient(JiraPreferences.BaseUrl, auth);
            }
            catch
            {
                return null;
            }
        }

        // --- Small UI helpers ----------------------------------------------

        private void AnimateLoaderSpinners()
        {
            _loaderFrame = (_loaderFrame + 1) % 12;
            Texture2D frame = EditorGUIUtility
                .IconContent($"WaitSpin{_loaderFrame:00}")
                ?.image as Texture2D;
            if (frame == null)
                return;

            for (int i = _loaderSpinners.Count - 1; i >= 0; i--)
            {
                VisualElement spinner = _loaderSpinners[i];
                if (spinner == null)
                {
                    _loaderSpinners.RemoveAt(i);
                    continue;
                }

                if (spinner.panel != null)
                    spinner.style.backgroundImage = new StyleBackground(frame);
            }
        }

        private void SetDestinationLoading(bool loading)
        {
            _destinationIsLoading = loading;
            if (_destinationLoader != null)
                _destinationLoader.style.display = loading
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (_destinationContent != null)
                _destinationContent.style.display = loading
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            UpdateLoaderAnimationState();
        }

        private void UpdateLoaderAnimationState()
        {
            if (_loaderAnimation == null)
                return;

            if (_destinationIsLoading || _modulesAreLoading)
                _loaderAnimation.Resume();
            else
                _loaderAnimation.Pause();
        }

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
