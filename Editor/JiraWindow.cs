using System;
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

        private TextField _urlField;
        private TextField _emailField;
        private TextField _tokenField;
        private Button _connectButton;
        private Button _disconnectButton;
        private Label _statusLabel;
        private VisualElement _connectedCard;
        private Label _connectedUserLabel;
        private Label _connectedEmailLabel;
        private bool _isConnecting;

        [MenuItem("Jira/Open Jira Workspace", priority = 0)]
        public static void Open()
        {
            JiraWindow window = GetWindow<JiraWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(520, 580);
            window.Show();
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

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            JiraStyles.ApplyWindow(rootVisualElement);

            BuildHeader();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 22;
            scroll.style.paddingRight = 22;
            scroll.style.paddingTop = 20;
            scroll.style.paddingBottom = 20;
            rootVisualElement.Add(scroll);

            scroll.Add(BuildConnectionCard());
            scroll.Add(BuildConnectedCard());
            scroll.Add(BuildComingSoonCard());

            RefreshConnectionState();
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            JiraStyles.ApplyHeader(header);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var icon = new Label("J");
            icon.style.width = 34;
            icon.style.height = 34;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.fontSize = 18;
            icon.style.unityFontStyleAndWeight = FontStyle.Bold;
            icon.style.backgroundColor = new StyleColor(
                new Color32(38, 132, 255, 255)
            );
            icon.style.color = Color.white;
            icon.style.borderTopLeftRadius = 7;
            icon.style.borderTopRightRadius = 7;
            icon.style.borderBottomLeftRadius = 7;
            icon.style.borderBottomRightRadius = 7;
            icon.style.marginRight = 11;

            var textColumn = new VisualElement();
            textColumn.style.flexGrow = 1;

            var title = new Label("Jira Communication");
            JiraStyles.ApplyTitle(title);

            var subtitle = new Label("Conecte sua conta Atlassian ao Unity Editor e prepare o fluxo de criação de issues.");
            JiraStyles.ApplySubtitle(subtitle);

            textColumn.Add(title);
            textColumn.Add(subtitle);
            titleRow.Add(icon);
            titleRow.Add(textColumn);
            header.Add(titleRow);
            rootVisualElement.Add(header);
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

            _urlField = new TextField("URL do Jira")
            {
                value = JiraPreferences.BaseUrl
            };
            _urlField.tooltip = "Exemplo: https://suaempresa.atlassian.net";
            JiraStyles.ApplyField(_urlField);
            card.Add(_urlField);

            _emailField = new TextField("E-mail Atlassian")
            {
                value = JiraPreferences.Email
            };
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

            _connectButton = new Button(TestConnectionAsync)
            {
                text = "Testar e conectar"
            };
            _connectButton.style.flexGrow = 1;
            JiraStyles.ApplyPrimaryButton(_connectButton);

            var createTokenButton = new Button(OpenApiTokenPage)
            {
                text = "Criar token"
            };
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

            _disconnectButton = new Button(Disconnect)
            {
                text = "Desconectar desta sessão"
            };
            JiraStyles.ApplySecondaryButton(_disconnectButton);
            _connectedCard.Add(_disconnectButton);

            return _connectedCard;
        }

        private static VisualElement BuildComingSoonCard()
        {
            var card = new VisualElement();
            JiraStyles.ApplyCard(card);

            var sectionTitle = new Label("Próximos módulos");
            JiraStyles.ApplySectionTitle(sectionTitle);
            card.Add(sectionTitle);

            string[] modules =
            {
                "• Seleção de projeto e tipo de issue",
                "• Criação de história, tarefa, bug e subtasks",
                "• Templates reutilizáveis por equipe",
                "• Anexos, screenshot e logs do Console",
                "• Lista de issues atribuídas ao usuário"
            };

            foreach (string module in modules)
            {
                var line = new Label(module);
                JiraStyles.ApplyMuted(line);
                line.style.marginBottom = 6;
                card.Add(line);
            }

            var note = new Label("A arquitetura da API já está separada da interface para permitir a implementação desses recursos sem alterar a autenticação.");
            JiraStyles.ApplyMuted(note);
            note.style.marginTop = 8;
            card.Add(note);

            return card;
        }

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

        private void SetBusy(bool busy)
        {
            _isConnecting = busy;
            _connectButton.SetEnabled(!busy);
            _connectButton.text = busy ? "Conectando..." : "Testar e conectar";
            _urlField.SetEnabled(!busy);
            _emailField.SetEnabled(!busy);
            _tokenField.SetEnabled(!busy);
        }

        private void ShowStatus(string message, bool success)
        {
            _statusLabel.text = message;
            _statusLabel.style.display = DisplayStyle.Flex;
            JiraStyles.ApplyStatus(_statusLabel, success);
        }
    }
}
