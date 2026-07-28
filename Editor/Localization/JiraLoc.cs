using System.Collections.Generic;
using OxenteGames.JiraCommunication.Settings;

namespace OxenteGames.JiraCommunication.Localization
{
    /// <summary>
    /// Minimal bilingual (Portuguese / English) string table for the editor UI.
    /// The active language is stored in <see cref="JiraPreferences.Language"/>.
    /// </summary>
    internal static class JiraLoc
    {
        public const string Pt = "pt";
        public const string En = "en";

        public static string Current
        {
            get => JiraPreferences.Language;
            set => JiraPreferences.Language = value;
        }

        private static bool IsEnglish => Current == En;

        public static string Tr(string key)
        {
            if (Table.TryGetValue(key, out string[] pair))
                return IsEnglish ? pair[1] : pair[0];
            return key;
        }

        public static string Tr(string key, params object[] args)
        {
            return string.Format(Tr(key), args);
        }

        // key => { pt, en }
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            [K.HeaderSubtitle] = new[]
            {
                "Conecte sua conta Atlassian e crie histórias, tarefas, bugs e subtasks direto do Unity.",
                "Connect your Atlassian account and create stories, tasks, bugs and subtasks from Unity."
            },
            [K.TabConnection] = new[] { "Conexão", "Connection" },
            [K.TabCreate] = new[] { "Criar Issue", "Create Issue" },
            [K.TabSettings] = new[] { "Configurações", "Settings" },

            [K.ConnSectionTitle] = new[] { "Conexão com o Jira Cloud", "Jira Cloud connection" },
            [K.ConnHelper] = new[]
            {
                "Use o endereço do Jira da empresa, seu e-mail Atlassian e um API Token pessoal. O token fica apenas na sessão atual do Unity.",
                "Use your company's Jira URL, your Atlassian email and a personal API Token. The token stays only in the current Unity session."
            },
            [K.FieldUrl] = new[] { "URL do Jira", "Jira URL" },
            [K.FieldUrlTooltip] = new[] { "Exemplo: https://suaempresa.atlassian.net", "Example: https://yourcompany.atlassian.net" },
            [K.FieldEmail] = new[] { "E-mail Atlassian", "Atlassian email" },
            [K.FieldToken] = new[] { "API Token", "API Token" },
            [K.BtnConnect] = new[] { "Testar e conectar", "Test and connect" },
            [K.BtnConnecting] = new[] { "Conectando...", "Connecting..." },
            [K.BtnCreateToken] = new[] { "Criar token", "Create token" },
            [K.MsgFillFields] = new[] { "Preencha a URL do Jira, o e-mail e o API Token.", "Fill in the Jira URL, email and API Token." },
            [K.MsgValidating] = new[] { "Validando credenciais com o Jira...", "Validating credentials with Jira..." },
            [K.MsgTokenLoaded] = new[]
            {
                "Há um token carregado nesta sessão. Clique em “Testar e conectar” para validar a conta.",
                "A token is loaded in this session. Click “Test and connect” to validate the account."
            },
            [K.MsgTokenRemoved] = new[] { "Token removido da sessão atual.", "Token removed from the current session." },

            [K.ConnectedTitle] = new[] { "Conta conectada", "Connected account" },
            [K.BtnGoToCreate] = new[] { "Ir para criação de issues", "Go to issue creation" },
            [K.BtnDisconnect] = new[] { "Desconectar desta sessão", "Disconnect from this session" },

            [K.CreateNoticeTitle] = new[] { "Conecte-se primeiro", "Connect first" },
            [K.CreateNoticeText] = new[]
            {
                "Para criar issues, valide sua conexão na aba \"Conexão\".",
                "To create issues, validate your connection on the \"Connection\" tab."
            },
            [K.BtnOpenConnTab] = new[] { "Abrir aba de conexão", "Open connection tab" },

            [K.CreateDestTitle] = new[] { "Destino", "Destination" },
            [K.FieldProject] = new[] { "Projeto", "Project" },
            [K.FieldIssueType] = new[] { "Tipo de issue", "Issue type" },
            [K.FieldParent] = new[] { "Issue pai (chave)", "Parent issue (key)" },
            [K.FieldParentTooltip] = new[] { "Obrigatório para subtasks. Ex.: PROJ-123", "Required for subtasks. E.g.: PROJ-123" },
            [K.ParentHint] = new[]
            {
                "Subtasks precisam da chave da issue pai (ex.: PROJ-123).",
                "Subtasks require the parent issue key (e.g.: PROJ-123)."
            },
            [K.FieldEpic] = new[] { "Épico", "Epic" },
            [K.FieldEpicTooltip] = new[]
            {
                "Vincula a issue a um épico (funciona em projetos team-managed).",
                "Links the issue to an epic (works on team-managed projects)."
            },
            [K.FieldSprint] = new[] { "Sprint ativa", "Active sprint" },
            [K.FieldSprintTooltip] = new[]
            {
                "Opcional. A issue será movida para a sprint após ser criada.",
                "Optional. The issue is moved to the sprint after being created."
            },
            [K.BtnReloadProjects] = new[] { "Recarregar projetos", "Reload projects" },
            [K.CreateDetailsTitle] = new[] { "Detalhes da issue", "Issue details" },
            [K.FieldSummary] = new[] { "Título (summary)", "Summary" },
            [K.FieldDescription] = new[] { "Descrição", "Description" },
            [K.BtnCreate] = new[] { "Criar issue", "Create issue" },
            [K.BtnCreating] = new[] { "Criando...", "Creating..." },
            [K.BtnOpenIssue] = new[] { "Abrir {0} no Jira", "Open {0} in Jira" },

            [K.NoneOption] = new[] { "— Nenhum —", "— None —" },
            [K.MsgLoadingProjects] = new[] { "Carregando projetos...", "Loading projects..." },
            [K.MsgNoProjectsOption] = new[] { "Nenhum projeto disponível", "No project available" },
            [K.MsgNoProjects] = new[] { "Nenhum projeto encontrado para esta conta.", "No project found for this account." },
            [K.MsgSelectProject] = new[] { "Selecione um projeto.", "Select a project." },
            [K.MsgSelectType] = new[] { "Selecione um tipo de issue.", "Select an issue type." },
            [K.MsgSummaryRequired] = new[] { "Informe o título (summary) da issue.", "Enter the issue summary." },
            [K.MsgSubtaskParentRequired] = new[]
            {
                "Subtasks exigem a chave da issue pai (ex.: PROJ-123).",
                "Subtasks require the parent issue key (e.g.: PROJ-123)."
            },
            [K.MsgNoCredentials] = new[]
            {
                "Sessão sem credenciais. Reconecte na aba \"Conexão\".",
                "No credentials in session. Reconnect on the \"Connection\" tab."
            },
            [K.MsgCreating] = new[] { "Criando issue no Jira...", "Creating issue in Jira..." },
            [K.MsgIssueCreated] = new[] { "Issue {0} criada com sucesso.", "Issue {0} created successfully." },
            [K.MsgSprintAdded] = new[] { " Adicionada à sprint \"{0}\".", " Added to sprint \"{0}\"." },
            [K.MsgSprintFailed] = new[]
            {
                " (Não foi possível mover para a sprint: {0})",
                " (Could not move to the sprint: {0})"
            },
            [K.MsgNoIssueTypes] = new[]
            {
                "Nenhum tipo de issue retornado. Verifique se sua conta tem permissão para criar issues neste projeto.",
                "No issue types returned. Check whether your account has permission to create issues in this project."
            },

            [K.CreateClassifyTitle] = new[] { "Classificação", "Classification" },
            [K.CreateDatesTitle] = new[] { "Datas (opcional)", "Dates (optional)" },
            [K.CreateAttachmentTitle] = new[] { "Anexo", "Attachment" },
            [K.FieldPriority] = new[] { "Prioridade", "Priority" },
            [K.FieldAssignee] = new[] { "Responsável", "Assignee" },
            [K.BtnAssignSelf] = new[] { "Atribuir a mim", "Assign to me" },
            [K.AssigneeNone] = new[] { "— Não atribuir —", "— Unassigned —" },
            [K.FieldTeam] = new[] { "Time", "Team" },
            [K.FieldStartDate] = new[] { "Data de início", "Start date" },
            [K.FieldDueDate] = new[] { "Data limite", "Due date" },
            [K.DateHint] = new[] { "Formato: AAAA-MM-DD", "Format: YYYY-MM-DD" },
            [K.BtnSelectFile] = new[] { "Selecionar arquivo / print", "Select file / screenshot" },
            [K.BtnRemoveFile] = new[] { "Remover", "Remove" },
            [K.NoFileSelected] = new[] { "Nenhum arquivo selecionado.", "No file selected." },
            [K.StatusNote] = new[]
            {
                "O status inicial é definido pelo fluxo (workflow) do projeto e não pode ser escolhido na criação.",
                "The initial status is defined by the project workflow and cannot be chosen at creation."
            },
            [K.PresetNote] = new[]
            {
                "Projeto, tipo, prioridade, responsável e time ficam salvos para a próxima criação.",
                "Project, type, priority, assignee and team are saved for the next creation."
            },
            [K.MsgAttachmentAdded] = new[] { " Anexo enviado.", " Attachment uploaded." },
            [K.MsgAttachmentFailed] = new[] { " (Anexo não enviado: {0})", " (Attachment not uploaded: {0})" },
            [K.SettingsClearPresets] = new[] { "Limpar campos salvos (presets)", "Clear saved fields (presets)" },
            [K.MsgPresetsCleared] = new[] { "Presets removidos.", "Presets cleared." },

            [K.SettingsTitle] = new[] { "Configurações", "Settings" },
            [K.SettingsLanguage] = new[] { "Idioma / Language", "Language / Idioma" },
            [K.LangPortuguese] = new[] { "Português", "Portuguese" },
            [K.LangEnglish] = new[] { "Inglês", "English" },
            [K.SettingsDataTitle] = new[] { "Dados salvos", "Saved data" },
            [K.SettingsDataNote] = new[]
            {
                "URL e e-mail ficam nas preferências locais do Editor. O API Token fica apenas na sessão atual.",
                "URL and email are stored in the Editor's local preferences. The API Token stays only in the current session."
            },
            [K.SettingsClearData] = new[] { "Limpar URL, e-mail e token", "Clear URL, email and token" },
            [K.MsgDataCleared] = new[] { "Dados de conexão removidos.", "Connection data removed." },
        };

        internal static class K
        {
            public const string HeaderSubtitle = "HeaderSubtitle";
            public const string TabConnection = "TabConnection";
            public const string TabCreate = "TabCreate";
            public const string TabSettings = "TabSettings";
            public const string ConnSectionTitle = "ConnSectionTitle";
            public const string ConnHelper = "ConnHelper";
            public const string FieldUrl = "FieldUrl";
            public const string FieldUrlTooltip = "FieldUrlTooltip";
            public const string FieldEmail = "FieldEmail";
            public const string FieldToken = "FieldToken";
            public const string BtnConnect = "BtnConnect";
            public const string BtnConnecting = "BtnConnecting";
            public const string BtnCreateToken = "BtnCreateToken";
            public const string MsgFillFields = "MsgFillFields";
            public const string MsgValidating = "MsgValidating";
            public const string MsgTokenLoaded = "MsgTokenLoaded";
            public const string MsgTokenRemoved = "MsgTokenRemoved";
            public const string ConnectedTitle = "ConnectedTitle";
            public const string BtnGoToCreate = "BtnGoToCreate";
            public const string BtnDisconnect = "BtnDisconnect";
            public const string CreateNoticeTitle = "CreateNoticeTitle";
            public const string CreateNoticeText = "CreateNoticeText";
            public const string BtnOpenConnTab = "BtnOpenConnTab";
            public const string CreateDestTitle = "CreateDestTitle";
            public const string FieldProject = "FieldProject";
            public const string FieldIssueType = "FieldIssueType";
            public const string FieldParent = "FieldParent";
            public const string FieldParentTooltip = "FieldParentTooltip";
            public const string ParentHint = "ParentHint";
            public const string FieldEpic = "FieldEpic";
            public const string FieldEpicTooltip = "FieldEpicTooltip";
            public const string FieldSprint = "FieldSprint";
            public const string FieldSprintTooltip = "FieldSprintTooltip";
            public const string BtnReloadProjects = "BtnReloadProjects";
            public const string CreateDetailsTitle = "CreateDetailsTitle";
            public const string FieldSummary = "FieldSummary";
            public const string FieldDescription = "FieldDescription";
            public const string BtnCreate = "BtnCreate";
            public const string BtnCreating = "BtnCreating";
            public const string BtnOpenIssue = "BtnOpenIssue";
            public const string NoneOption = "NoneOption";
            public const string MsgLoadingProjects = "MsgLoadingProjects";
            public const string MsgNoProjectsOption = "MsgNoProjectsOption";
            public const string MsgNoProjects = "MsgNoProjects";
            public const string MsgSelectProject = "MsgSelectProject";
            public const string MsgSelectType = "MsgSelectType";
            public const string MsgSummaryRequired = "MsgSummaryRequired";
            public const string MsgSubtaskParentRequired = "MsgSubtaskParentRequired";
            public const string MsgNoCredentials = "MsgNoCredentials";
            public const string MsgCreating = "MsgCreating";
            public const string MsgIssueCreated = "MsgIssueCreated";
            public const string MsgSprintAdded = "MsgSprintAdded";
            public const string MsgSprintFailed = "MsgSprintFailed";
            public const string MsgNoIssueTypes = "MsgNoIssueTypes";
            public const string CreateClassifyTitle = "CreateClassifyTitle";
            public const string CreateDatesTitle = "CreateDatesTitle";
            public const string CreateAttachmentTitle = "CreateAttachmentTitle";
            public const string FieldPriority = "FieldPriority";
            public const string FieldAssignee = "FieldAssignee";
            public const string BtnAssignSelf = "BtnAssignSelf";
            public const string AssigneeNone = "AssigneeNone";
            public const string FieldTeam = "FieldTeam";
            public const string FieldStartDate = "FieldStartDate";
            public const string FieldDueDate = "FieldDueDate";
            public const string DateHint = "DateHint";
            public const string BtnSelectFile = "BtnSelectFile";
            public const string BtnRemoveFile = "BtnRemoveFile";
            public const string NoFileSelected = "NoFileSelected";
            public const string StatusNote = "StatusNote";
            public const string PresetNote = "PresetNote";
            public const string MsgAttachmentAdded = "MsgAttachmentAdded";
            public const string MsgAttachmentFailed = "MsgAttachmentFailed";
            public const string SettingsClearPresets = "SettingsClearPresets";
            public const string MsgPresetsCleared = "MsgPresetsCleared";
            public const string SettingsTitle = "SettingsTitle";
            public const string SettingsLanguage = "SettingsLanguage";
            public const string LangPortuguese = "LangPortuguese";
            public const string LangEnglish = "LangEnglish";
            public const string SettingsDataTitle = "SettingsDataTitle";
            public const string SettingsDataNote = "SettingsDataNote";
            public const string SettingsClearData = "SettingsClearData";
            public const string MsgDataCleared = "MsgDataCleared";
        }
    }
}
