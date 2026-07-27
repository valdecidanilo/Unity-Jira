using System;
using System.Collections.Generic;
using OxenteGames.JiraCommunication.API;
using OxenteGames.JiraCommunication.Models;
using OxenteGames.JiraCommunication.Settings;
using OxenteGames.JiraCommunication.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OxenteGames.JiraCommunication
{
    internal sealed class JiraWindow : EditorWindow
    {
        private const string WindowTitle = "Jira";
        private const string NoneOption = "— Nenhum —";

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
        private VisualElement _connectionPanel;
        private VisualElement _createPanel;

        // Create tab
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

        private readonly List<JiraProject> _projects = new List<JiraProject>();
        private readonly List<JiraIssueType> _issueTypes = new List<JiraIssueType>();
        private readonly List<JiraEpic> _epics = new List<JiraEpic>();
        private readonly List<JiraSprint> _sprints = new List<JiraSprint>();
        private int _activeBoardId = -1;
        private bool _isCreating;
        private bool _projectsLoaded;
        private bool _pendingCreateTab;

        [MenuItem("Jira/Open Jira Workspace", priority = 0)]
        public static void Open()
        {
            JiraWindow window = GetWindow<JiraWindow>();
            window.titleContent = new GUIContent(WindowTitle, LoadIcon());
            window.minSize = new Vector2(540, 620);
            window.Show();
        }

        [MenuItem("Jira/Create Issue", priority = 1)]
        public static void OpenCreate()
        {
            JiraWindow window = GetWindow<JiraWindow>();
            window.titleContent = new GUIContent(WindowTitle, LoadIcon());
            window.minSize = new Vector2(540, 620);
            window.Show();
            window._pendingCreateTab = true;
            window.SelectTab(createTab: true);
        }

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

        private static Texture2D LoadIcon()
        {
            return Resources.Load<Texture2D>("jira-icon");
        }

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
            scroll.Add(_connectionPanel);
            scroll.Add(_createPanel);

            RefreshConnectionState();
            SelectTab(createTab: _pendingCreateTab);
            _pendingCreateTab = false;
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

            var subtitle = new Label("Conecte sua conta Atlassian e crie histórias, tarefas, bugs e subtasks direto do Unity.");
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

            // Fallback if the texture is not imported yet.
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

            _connectionTab = new Button(() => SelectTab(createTab: false)) { text = "Conexão" };
            _createTab = new Button(() => SelectTab(createTab: true)) { text = "Criar Issue" };

            bar.Add(_connectionTab);
            bar.Add(_createTab);
            rootVisualElement.Add(bar);
        }

        private void SelectTab(bool createTab)
        {
            if (_connectionPanel == null || _createPanel == null)
                return;

            _connectionPanel.style.display = createTab ? DisplayStyle.None : DisplayStyle.Flex;
            _createPanel.style.display = createTab ? DisplayStyle.Flex : DisplayStyle.None;

            JiraStyles.ApplyTab(_connectionTab, !createTab);
            JiraStyles.ApplyTab(_createTab, createTab);

            if (createTab)
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

            var sectionTitle = new Label("Conexão com o Jira Cloud");
            JiraStyles.ApplySectionTitle(sectionTitle);
            card.Add(sectionTitle);

            var helper = new Label("Use o endereço do Jira da empresa, seu e-mail Atlassian e um API Token pessoal. O token fica apenas na sessão atual do Unity.");
            JiraStyles.ApplyMuted(helper);
            helper.style.marginBottom = 14;
            card.Add(helper);

            _urlField = new TextField("URL do Jira") { value = JiraPreferences.BaseUrl };
            _urlField.tooltip = "Exemplo: https://suaempresa.atlassian.net";
            JiraStyles.ApplyField(_urlField);
            card.Add(_urlField);

            _emailField = new TextField("E-mail Atlassian") { value = JiraPreferences.Email };
            JiraStyles.ApplyField(_emailField);
            card.Add(_emailField);

            _tokenField = new TextField("API Token")
            {
                value = JiraPreferences.SessionToken,
                isPasswordField = true
            };
            JiraStyles.ApplyField(_tokenField);
            card.Add(_tokenField);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 4;

            _connectButton = new Button(TestConnectionAsync) { text = "Testar e conectar" };
            _connectButton.style.flexGrow = 1;
            JiraStyles.ApplyPrimaryButton(_connectButton);

            var createTokenButton = new Button(OpenApiTokenPage) { text = "Criar token" };
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

            var sectionTitle = new Label("Conta conectada");
            JiraStyles.ApplySectionTitle(sectionTitle);
            _connectedCard.Add(sectionTitle);

            _connectedUserLabel = new Label("Usuário");
            _connectedUserLabel.style.fontSize = 14;
            _connectedUserLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _connectedCard.Add(_connectedUserLabel);

            _connectedEmailLabel = new Label();
            JiraStyles.ApplyMuted(_connectedEmailLabel);
            _connectedEmailLabel.style.marginTop = 3;
            _connectedEmailLabel.style.marginBottom = 12;
            _connectedCard.Add(_connectedEmailLabel);

            var goToCreate = new Button(() => SelectTab(createTab: true)) { text = "Ir para criação de issues" };
            JiraStyles.ApplySecondaryButton(goToCreate);
            goToCreate.style.marginBottom = 8;
            _connectedCard.Add(goToCreate);

            var disconnectButton = new Button(Disconnect) { text = "Desconectar desta sessão" };
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
            var noticeTitle = new Label("Conecte-se primeiro");
            JiraStyles.ApplySectionTitle(noticeTitle);
            var noticeText = new Label("Para criar issues, valide sua conexão na aba \"Conexão\".");
            JiraStyles.ApplyMuted(noticeText);
            var noticeButton = new Button(() => SelectTab(createTab: false)) { text = "Abrir aba de conexão" };
            JiraStyles.ApplySecondaryButton(noticeButton);
            noticeButton.style.marginTop = 12;
            _createNotice.Add(noticeTitle);
            _createNotice.Add(noticeText);
            _createNotice.Add(noticeButton);
            panel.Add(_createNotice);

            _createForm = new VisualElement();

            var contextCard = new VisualElement();
            JiraStyles.ApplyCard(contextCard);

            var contextTitle = new Label("Destino");
            JiraStyles.ApplySectionTitle(contextTitle);
            contextCard.Add(contextTitle);

            _projectDropdown = new DropdownField("Projeto");
            JiraStyles.ApplyDropdown(_projectDropdown);
            _projectDropdown.RegisterValueChangedCallback(_ => OnProjectSelected());
            contextCard.Add(_projectDropdown);

            _typeDropdown = new DropdownField("Tipo de issue");
            JiraStyles.ApplyDropdown(_typeDropdown);
            _typeDropdown.RegisterValueChangedCallback(_ => OnTypeSelected());
            contextCard.Add(_typeDropdown);

            _parentContainer = new VisualElement();
            _parentField = new TextField("Issue pai (chave)");
            _parentField.tooltip = "Obrigatório para subtasks. Ex.: PROJ-123";
            JiraStyles.ApplyField(_parentField);
            var parentHint = new Label("Subtasks precisam da chave da issue pai (ex.: PROJ-123).");
            JiraStyles.ApplyFieldHint(parentHint);
            _parentContainer.Add(_parentField);
            _parentContainer.Add(parentHint);
            _parentContainer.style.display = DisplayStyle.None;
            contextCard.Add(_parentContainer);

            _epicContainer = new VisualElement();
            _epicDropdown = new DropdownField("Épico");
            _epicDropdown.tooltip = "Vincula a issue a um épico (funciona em projetos team-managed).";
            JiraStyles.ApplyDropdown(_epicDropdown);
            _epicContainer.Add(_epicDropdown);
            contextCard.Add(_epicContainer);

            _sprintDropdown = new DropdownField("Sprint ativa");
            _sprintDropdown.tooltip = "Opcional. A issue será movida para a sprint após ser criada.";
            JiraStyles.ApplyDropdown(_sprintDropdown);
            contextCard.Add(_sprintDropdown);

            var refreshButton = new Button(() => ReloadProjectsAsync()) { text = "Recarregar projetos" };
            JiraStyles.ApplySecondaryButton(refreshButton);
            contextCard.Add(refreshButton);

            _createForm.Add(contextCard);

            var detailsCard = new VisualElement();
            JiraStyles.ApplyCard(detailsCard);

            var detailsTitle = new Label("Detalhes da issue");
            JiraStyles.ApplySectionTitle(detailsTitle);
            detailsCard.Add(detailsTitle);

            _summaryField = new TextField("Título (summary)");
            JiraStyles.ApplyField(_summaryField);
            detailsCard.Add(_summaryField);

            _descriptionField = new TextField("Descrição");
            JiraStyles.ApplyMultiline(_descriptionField);
            detailsCard.Add(_descriptionField);

            _createButton = new Button(CreateIssueAsync) { text = "Criar issue" };
            JiraStyles.ApplyPrimaryButton(_createButton);
            _createButton.style.marginRight = 0;
            _createButton.style.marginTop = 4;
            detailsCard.Add(_createButton);

            _createStatus = new Label();
            _createStatus.style.display = DisplayStyle.None;
            detailsCard.Add(_createStatus);

            _openIssueButton = new Button { text = "Abrir issue no Jira" };
            JiraStyles.ApplyLinkButton(_openIssueButton);
            _openIssueButton.style.display = DisplayStyle.None;
            detailsCard.Add(_openIssueButton);

            _createForm.Add(detailsCard);
            panel.Add(_createForm);

            return panel;
        }

        private void RefreshCreateAvailability()
        {
            bool connected = HasCredentials();
            _createNotice.style.display = connected ? DisplayStyle.None : DisplayStyle.Flex;
            _createForm.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;

            if (connected && !_projectsLoaded)
                ReloadProjectsAsync();
        }

        // --- Data loading ---------------------------------------------------

        private async void ReloadProjectsAsync()
        {
            JiraClient client = BuildClientOrNull();
            if (client == null)
                return;

            SetCreateStatus("Carregando projetos...", true);
            try
            {
                List<JiraProject> projects = await client.GetProjectsAsync();
                _projects.Clear();
                _projects.AddRange(projects);
                _projectsLoaded = true;

                if (_projects.Count == 0)
                {
                    _projectDropdown.choices = new List<string>();
                    _projectDropdown.SetValueWithoutNotify("Nenhum projeto disponível");
                    SetCreateStatus("Nenhum projeto encontrado para esta conta.", false);
                    return;
                }

                var labels = new List<string>(_projects.Count);
                foreach (JiraProject project in _projects)
                    labels.Add($"{project.name} ({project.key})");

                _projectDropdown.choices = labels;
                _projectDropdown.SetValueWithoutNotify(labels[0]);
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
                foreach (JiraIssueType type in _issueTypes)
                    typeLabels.Add(type.subtask ? $"{type.name} (subtask)" : type.name);

                _typeDropdown.choices = typeLabels;
                if (typeLabels.Count > 0)
                    _typeDropdown.SetValueWithoutNotify(typeLabels[0]);
                else
                    _typeDropdown.SetValueWithoutNotify(string.Empty);

                OnTypeSelected();
            }
            catch (Exception exception)
            {
                SetCreateStatus(exception.Message, false);
            }

            await LoadBoardDataAsync(client, project.key);
        }

        private async System.Threading.Tasks.Task LoadBoardDataAsync(JiraClient client, string projectKey)
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
            catch
            {
                // Agile API not available; leave epics/sprints empty.
            }

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
            var labels = new List<string> { NoneOption };
            foreach (JiraEpic epic in _epics)
                labels.Add($"{epic.DisplayName} ({epic.key})");

            _epicDropdown.choices = labels;
            _epicDropdown.SetValueWithoutNotify(labels[0]);
        }

        private void PopulateSprintChoices()
        {
            var labels = new List<string> { NoneOption };
            foreach (JiraSprint sprint in _sprints)
                labels.Add(sprint.name);

            _sprintDropdown.choices = labels;
            _sprintDropdown.SetValueWithoutNotify(labels[0]);

            _sprintDropdown.SetEnabled(_sprints.Count > 0);
        }

        private void OnTypeSelected()
        {
            JiraIssueType type = SelectedIssueType();
            bool isSubtask = type != null && type.subtask;

            _parentContainer.style.display = isSubtask ? DisplayStyle.Flex : DisplayStyle.None;
            _epicContainer.style.display = isSubtask ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // --- Create ---------------------------------------------------------

        private async void CreateIssueAsync()
        {
            if (_isCreating)
                return;

            JiraProject project = SelectedProject();
            JiraIssueType type = SelectedIssueType();
            string summary = _summaryField.value?.Trim();

            if (project == null)
            {
                SetCreateStatus("Selecione um projeto.", false);
                return;
            }

            if (type == null)
            {
                SetCreateStatus("Selecione um tipo de issue.", false);
                return;
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                SetCreateStatus("Informe o título (summary) da issue.", false);
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
                    SetCreateStatus("Subtasks exigem a chave da issue pai (ex.: PROJ-123).", false);
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

            JiraClient client = BuildClientOrNull();
            if (client == null)
            {
                SetCreateStatus("Sessão sem credenciais. Reconecte na aba \"Conexão\".", false);
                return;
            }

            SetCreatingBusy(true);
            _openIssueButton.style.display = DisplayStyle.None;
            SetCreateStatus("Criando issue no Jira...", true);

            try
            {
                JiraCreateIssueResult result = await client.CreateIssueAsync(draft);

                if (!result.Success)
                {
                    SetCreateStatus(result.Message, false);
                    return;
                }

                string message = result.Message;

                JiraSprint sprint = SelectedSprint();
                if (sprint != null && !type.subtask)
                {
                    string sprintError = await client.MoveIssueToSprintAsync(sprint.id, result.IssueKey);
                    message += sprintError == null
                        ? $" Adicionada à sprint \"{sprint.name}\"."
                        : $" (Não foi possível mover para a sprint: {sprintError})";
                }

                SetCreateStatus(message, true);
                ShowOpenIssue(client.BaseUrl, result.IssueKey);

                _summaryField.value = string.Empty;
                _descriptionField.value = string.Empty;
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

        private void ShowOpenIssue(string baseUrl, string issueKey)
        {
            string url = $"{baseUrl}/browse/{issueKey}";
            _openIssueButton.text = $"Abrir {issueKey} no Jira";
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
            int index = _epicDropdown.index - 1; // index 0 is "— Nenhum —"
            return index >= 0 && index < _epics.Count ? _epics[index] : null;
        }

        private JiraSprint SelectedSprint()
        {
            int index = _sprintDropdown.index - 1; // index 0 is "— Nenhum —"
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
                ShowStatus("Preencha a URL do Jira, o e-mail e o API Token.", false);
                return;
            }

            JiraPreferences.BaseUrl = baseUrl;
            JiraPreferences.Email = email;
            JiraPreferences.SessionToken = token;

            SetBusy(true);
            ShowStatus("Validando credenciais com o Jira...", true);

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
                _projectsLoaded = false;
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
            string displayName = !string.IsNullOrWhiteSpace(user?.displayName)
                ? user.displayName
                : "Usuário Atlassian";

            string email = !string.IsNullOrWhiteSpace(user?.emailAddress)
                ? user.emailAddress
                : JiraPreferences.Email;

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
            ShowStatus("Token removido da sessão atual.", true);
        }

        private void RefreshConnectionState()
        {
            bool hasSessionToken = !string.IsNullOrWhiteSpace(JiraPreferences.SessionToken);
            _connectedCard.style.display = DisplayStyle.None;

            if (hasSessionToken)
            {
                ShowStatus("Há um token carregado nesta sessão. Clique em “Testar e conectar” para validar a conta.", true);
            }
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
            _connectButton.text = busy ? "Conectando..." : "Testar e conectar";
            _urlField.SetEnabled(!busy);
            _emailField.SetEnabled(!busy);
            _tokenField.SetEnabled(!busy);
        }

        private void SetCreatingBusy(bool busy)
        {
            _isCreating = busy;
            _createButton.SetEnabled(!busy);
            _createButton.text = busy ? "Criando..." : "Criar issue";
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
