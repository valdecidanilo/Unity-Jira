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
            [K.BtnConnect] = new[] { " Conectar", "Connect" },
            [K.BtnConnecting] = new[] { "Conectando...", "Connecting..." },
            [K.BtnCreateToken] = new[] { "Criar token", "Create token" },
            [K.MsgFillFields] = new[] { "Preencha a URL do Jira, o e-mail e o API Token.", "Fill in the Jira URL, email and API Token." },
            [K.MsgValidating] = new[] { "Validando credenciais com o Jira...", "Validating credentials with Jira..." },
            [K.MsgTokenLoaded] = new[]
            {
                "Há um token carregado nesta sessão. Clique em “Conectar” para validar a conta.",
                "A token is loaded in this session. Click “Connect” to validate the account."
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
            [K.MsgNoEpicsOption] = new[] { "Nenhum épico encontrado", "No epic found" },
            [K.MsgEpicsFailedOption] = new[] { "Não foi possível carregar os épicos", "Could not load epics" },
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
            [K.MsgLoadingFields] = new[] { "Carregando campos configurados no Jira...", "Loading fields configured in Jira..." },
            [K.MsgFieldsLoaded] = new[] { "{0} campos do Jira carregados para este tipo.", "{0} Jira fields loaded for this type." },
            [K.MsgFieldsLoadFailed] = new[] { "Não foi possível carregar os campos do Jira: {0}", "Could not load Jira fields: {0}" },
            [K.MsgNoFieldsReturned] = new[] { "O Jira não retornou os campos configurados para este tipo de issue.", "Jira returned no configured fields for this issue type." },
            [K.MsgFieldsNotLoaded] = new[] { "Aguarde ou recarregue os campos do Jira antes de criar.", "Wait for or reload the Jira fields before creating." },

            [K.CreateClassifyTitle] = new[] { "Classificação", "Classification" },
            [K.CreateDatesTitle] = new[] { "Datas", "Dates" },
            [K.CreateAdditionalFieldsTitle] = new[] { "Campos adicionais do Jira", "Additional Jira fields" },
            [K.AdditionalFieldsHint] = new[]
            {
                "Campos com * são obrigatórios. Para informar issues, use as chaves separadas por vírgula: PROJ-123, PROJ-456.",
                "Fields marked with * are required. For issues, use comma-separated keys: PROJ-123, PROJ-456."
            },
            [K.ArrayFieldHint] = new[]
            {
                "Informe os valores separados por vírgula. Para issues, use: PROJ-123, PROJ-456.",
                "Enter comma-separated values. For issues, use: PROJ-123, PROJ-456."
            },
            [K.TeamIdHint] = new[]
            {
                "Informe o ID do time do Jira. Este campo é carregado conforme o projeto e o tipo da issue.",
                "Enter the Jira team ID. This field is loaded according to the project and issue type."
            },
            [K.CreateAttachmentTitle] = new[] { "Anexo", "Attachment" },
            [K.FieldPriority] = new[] { "Prioridade", "Priority" },
            [K.FieldAssignee] = new[] { "Responsável", "Assignee" },
            [K.FieldAssigneeSearch] = new[] { "Pesquisar responsável", "Search assignee" },
            [K.BtnAssignSelf] = new[] { "Atribuir a mim", "Assign to me" },
            [K.AssigneeNone] = new[] { "— Não atribuir —", "— Unassigned —" },
            [K.FieldTeam] = new[] { "Time", "Team" },
            [K.FieldStartDate] = new[] { "Data de início", "Start date" },
            [K.FieldDueDate] = new[] { "Data limite", "Due date" },
            [K.DateHint] = new[] { "Formato: AAAA-MM-DD", "Format: YYYY-MM-DD" },
            [K.BtnSelectFile] = new[] { "Selecionar arquivo / print", "Select file / screenshot" },
            [K.BtnCaptureGameView] = new[] { "Capturar Game View", "Capture Game View" },
            [K.CaptureGameViewHint] = new[]
            {
                "Captura a câmera principal e usa o PNG como anexo da issue.",
                "Captures the main camera and uses the PNG as the issue attachment."
            },
            [K.MsgNoCameraForScreenshot] = new[] { "Nenhuma câmera encontrada para capturar.", "No camera was found to capture." },
            [K.MsgScreenshotCaptured] = new[] { "Print pronto: {0}", "Screenshot ready: {0}" },
            [K.MsgScreenshotFailed] = new[] { "Falha ao capturar: {0}", "Capture failed: {0}" },
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
            [K.MsgRequiredField] = new[] { "O campo \"{0}\" é obrigatório.", "The \"{0}\" field is required." },
            [K.MsgInvalidNumber] = new[] { "O campo \"{0}\" precisa ser um número válido.", "The \"{0}\" field must be a valid number." },
            [K.FieldQuickSubtask] = new[] { "Subtarefas rápidas (opcional)", "Quick subtasks (optional)" },
            [K.FieldQuickSubtaskTitle] = new[] { "Título", "Title" },
            [K.FieldQuickSubtaskDescription] = new[] { "Descrição", "Description" },
            [K.BtnAddQuickSubtask] = new[] { "+ Adicionar subtarefa", "+ Add subtask" },
            [K.BtnRemoveQuickSubtask] = new[] { "Remover subtarefa", "Remove subtask" },
            [K.QuickSubtaskNumber] = new[] { "Subtarefa {0}", "Subtask {0}" },
            [K.QuickSubtaskHint] = new[]
            {
                "Use + para adicionar e − para remover. Cada item preenchido será criado junto com a História.",
                "Use + to add and − to remove. Each filled item is created with the Story."
            },
            [K.MsgQuickSubtaskCreated] = new[] { " Subtarefa {0} criada.", " Subtask {0} created." },
            [K.MsgQuickSubtaskFailed] = new[] { " (Subtarefa \"{0}\" não criada: {1})", " (Subtask \"{0}\" was not created: {1})" },
            [K.MsgQuickSubtaskTitleRequired] = new[] { "Preencha o título da subtarefa que possui descrição.", "Enter a title for the subtask that has a description." },

            [K.AiProviderLabel] = new[] { "Provedor de IA", "AI provider" },
            [K.ProviderClaude] = new[] { "Claude (Anthropic)", "Claude (Anthropic)" },
            [K.ProviderOpenAi] = new[] { "ChatGPT (OpenAI)", "ChatGPT (OpenAI)" },
            [K.EpicProgressLoading] = new[] { "Calculando conclusão do épico...", "Calculating epic completion..." },
            [K.EpicProgressFormat] = new[] { "Conclusão do épico: {0}/{1} itens ({2}%)", "Epic completion: {0}/{1} items ({2}%)" },
            [K.EpicProgressEmpty] = new[] { "Este épico ainda não tem itens.", "This epic has no items yet." },
            [K.EpicProgressFailed] = new[] { "Não foi possível calcular a conclusão do épico.", "Could not calculate epic completion." },
            [K.AiSettingsTitle] = new[] { "Assistente de IA", "AI Assistant" },
            [K.AiSettingsNote] = new[]
            {
                "Escolha o provedor e informe sua própria API Key. A chave fica apenas na sessão atual do Unity.",
                "Choose the provider and enter your own API Key. The key stays only in the current Unity session."
            },
            [K.AiTokenLabel] = new[] { "API Key", "API Key" },
            [K.AiModelLabel] = new[] { "Modelo", "Model" },
            [K.BtnGetAiKey] = new[] { "Obter API Key", "Get API Key" },
            [K.AiSectionTitle] = new[] { "Assistente de IA", "AI Assistant" },
            [K.AiPromptLabel] = new[] { "Descreva a atividade (a IA gera título, descrição e prioridade)", "Describe the task (AI drafts title, description and priority)" },
            [K.BtnConfigureAi] = new[] { "Configurar Assistente de IA", "Configure AI Assistant" },
            [K.BtnAiGenerate] = new[] { "Gerar com IA", "Draft with AI" },
            [K.BtnAiGenerating] = new[] { "Gerando...", "Drafting..." },
            [K.MsgAiNoToken] = new[]
            {
                "Configure sua API Key de IA na aba Configurações.",
                "Set your AI API Key on the Settings tab."
            },
            [K.MsgAiNoInput] = new[] { "Descreva a atividade para a IA gerar.", "Describe the task for the AI to draft." },
            [K.MsgAiGenerating] = new[] { "Gerando com IA...", "Drafting with AI..." },
            [K.MsgAiDone] = new[] { "Campos preenchidos pela IA. Revise antes de criar.", "Fields drafted by AI. Review before creating." },
            [K.MsgAiFailed] = new[] { "Falha na IA: {0}", "AI error: {0}" },

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
            public const string MsgNoEpicsOption = "MsgNoEpicsOption";
            public const string MsgEpicsFailedOption = "MsgEpicsFailedOption";
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
            public const string MsgLoadingFields = "MsgLoadingFields";
            public const string MsgFieldsLoaded = "MsgFieldsLoaded";
            public const string MsgFieldsLoadFailed = "MsgFieldsLoadFailed";
            public const string MsgNoFieldsReturned = "MsgNoFieldsReturned";
            public const string MsgFieldsNotLoaded = "MsgFieldsNotLoaded";
            public const string CreateClassifyTitle = "CreateClassifyTitle";
            public const string CreateDatesTitle = "CreateDatesTitle";
            public const string CreateAdditionalFieldsTitle = "CreateAdditionalFieldsTitle";
            public const string AdditionalFieldsHint = "AdditionalFieldsHint";
            public const string ArrayFieldHint = "ArrayFieldHint";
            public const string TeamIdHint = "TeamIdHint";
            public const string CreateAttachmentTitle = "CreateAttachmentTitle";
            public const string FieldPriority = "FieldPriority";
            public const string FieldAssignee = "FieldAssignee";
            public const string FieldAssigneeSearch = "FieldAssigneeSearch";
            public const string BtnAssignSelf = "BtnAssignSelf";
            public const string AssigneeNone = "AssigneeNone";
            public const string FieldTeam = "FieldTeam";
            public const string FieldStartDate = "FieldStartDate";
            public const string FieldDueDate = "FieldDueDate";
            public const string DateHint = "DateHint";
            public const string BtnSelectFile = "BtnSelectFile";
            public const string BtnCaptureGameView = "BtnCaptureGameView";
            public const string CaptureGameViewHint = "CaptureGameViewHint";
            public const string MsgNoCameraForScreenshot = "MsgNoCameraForScreenshot";
            public const string MsgScreenshotCaptured = "MsgScreenshotCaptured";
            public const string MsgScreenshotFailed = "MsgScreenshotFailed";
            public const string BtnRemoveFile = "BtnRemoveFile";
            public const string NoFileSelected = "NoFileSelected";
            public const string StatusNote = "StatusNote";
            public const string PresetNote = "PresetNote";
            public const string MsgAttachmentAdded = "MsgAttachmentAdded";
            public const string MsgAttachmentFailed = "MsgAttachmentFailed";
            public const string SettingsClearPresets = "SettingsClearPresets";
            public const string MsgPresetsCleared = "MsgPresetsCleared";
            public const string MsgRequiredField = "MsgRequiredField";
            public const string MsgInvalidNumber = "MsgInvalidNumber";
            public const string FieldQuickSubtask = "FieldQuickSubtask";
            public const string FieldQuickSubtaskTitle = "FieldQuickSubtaskTitle";
            public const string FieldQuickSubtaskDescription = "FieldQuickSubtaskDescription";
            public const string BtnAddQuickSubtask = "BtnAddQuickSubtask";
            public const string BtnRemoveQuickSubtask = "BtnRemoveQuickSubtask";
            public const string QuickSubtaskNumber = "QuickSubtaskNumber";
            public const string QuickSubtaskHint = "QuickSubtaskHint";
            public const string MsgQuickSubtaskCreated = "MsgQuickSubtaskCreated";
            public const string MsgQuickSubtaskFailed = "MsgQuickSubtaskFailed";
            public const string MsgQuickSubtaskTitleRequired = "MsgQuickSubtaskTitleRequired";
            public const string AiProviderLabel = "AiProviderLabel";
            public const string ProviderClaude = "ProviderClaude";
            public const string ProviderOpenAi = "ProviderOpenAi";
            public const string EpicProgressLoading = "EpicProgressLoading";
            public const string EpicProgressFormat = "EpicProgressFormat";
            public const string EpicProgressEmpty = "EpicProgressEmpty";
            public const string EpicProgressFailed = "EpicProgressFailed";
            public const string AiSettingsTitle = "AiSettingsTitle";
            public const string AiSettingsNote = "AiSettingsNote";
            public const string AiTokenLabel = "AiTokenLabel";
            public const string AiModelLabel = "AiModelLabel";
            public const string BtnGetAiKey = "BtnGetAiKey";
            public const string AiSectionTitle = "AiSectionTitle";
            public const string AiPromptLabel = "AiPromptLabel";
            public const string BtnConfigureAi = "BtnConfigureAi";
            public const string BtnAiGenerate = "BtnAiGenerate";
            public const string BtnAiGenerating = "BtnAiGenerating";
            public const string MsgAiNoToken = "MsgAiNoToken";
            public const string MsgAiNoInput = "MsgAiNoInput";
            public const string MsgAiGenerating = "MsgAiGenerating";
            public const string MsgAiDone = "MsgAiDone";
            public const string MsgAiFailed = "MsgAiFailed";
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
