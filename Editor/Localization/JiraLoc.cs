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
            [K.TabCreate] = new[] { "Criar Atividade", "Create Activity" },
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
            [K.FieldSprint] = new[] { "Sprint", "Sprint" },
            [K.FieldSprintTooltip] = new[]
            {
                "Opcional. Selecione uma sprint ativa ou futura carregada do Jira.",
                "Optional. Select an active or future sprint loaded from Jira."
            },
            [K.BtnReloadProjects] = new[] { "Recarregar projetos", "Reload projects" },
            [K.CreateDetailsTitle] = new[] { "Detalhes da atividade", "Activity details" },
            [K.FieldSummary] = new[] { "Título (summary)", "Summary" },
            [K.FieldDescription] = new[] { "Descrição", "Description" },
            [K.BtnCreate] = new[] { "Criar issue", "Create issue" },
            [K.BtnCreating] = new[] { "Criando...", "Creating..." },
            [K.BtnOpenIssue] = new[] { "Abrir {0} no Jira", "Open {0} in Jira" },

            [K.NoneOption] = new[] { "— Nenhum —", "— None —" },
            [K.MsgLoadingProjects] = new[] { "Carregando projetos...", "Loading projects..." },
            [K.MsgLoadingCreateDestination] = new[]
            {
                "Carregando projeto, tipos e opções...",
                "Loading project, types and options..."
            },
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
            [K.MsgLoadingModule] = new[] { "Preparando este módulo...", "Preparing this module..." },
            [K.DropdownNoOptions] = new[] { "Nenhuma opção disponível.", "No options available." },
            [K.DropdownSearchLabel] = new[] { "Pesquisar opções", "Search options" },
            [K.MsgFieldsLoaded] = new[] { "{0} campos do Jira carregados para este tipo.", "{0} Jira fields loaded for this type." },
            [K.MsgFieldsLoadFailed] = new[] { "Não foi possível carregar os campos do Jira: {0}", "Could not load Jira fields: {0}" },
            [K.MsgNoFieldsReturned] = new[] { "O Jira não retornou os campos configurados para este tipo de issue.", "Jira returned no configured fields for this issue type." },
            [K.MsgFieldsNotLoaded] = new[] { "Aguarde ou recarregue os campos do Jira antes de criar.", "Wait for or reload the Jira fields before creating." },

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
            [K.SprintFieldHint] = new[]
            {
                "Selecione uma sprint ativa ou futura. Ex.: Sprint 24 - Login e Cadastro.",
                "Select an active or future sprint. Example: Sprint 24 - Login and Sign-up."
            },
            [K.AssociatedItemsSearch] = new[]
            {
                "Pesquisar item associado",
                "Search associated item"
            },
            [K.AssociatedItemsSearchHint] = new[]
            {
                "Pesquise pela chave ou por parte do título. Ex.: PROJ-123 ou corrigir login.",
                "Search by key or part of the title. Example: PROJ-123 or fix login."
            },
            [K.AssociatedItemsSearching] = new[]
            {
                "Pesquisando atividades...",
                "Searching activities..."
            },
            [K.AssociatedItemsNoResults] = new[]
            {
                "Nenhuma atividade encontrada.",
                "No activity found."
            },
            [K.TeamIdHint] = new[]
            {
                "Informe o ID do time do Jira. Este campo é carregado conforme o projeto e o tipo da issue.",
                "Enter the Jira team ID. This field is loaded according to the project and issue type."
            },
            [K.CreateAttachmentTitle] = new[] { "Anexo", "Attachment" },
            [K.FieldPriority] = new[] { "Prioridade", "Priority" },
            [K.FieldActivityWeight] = new[] { "Peso / Story Points", "Weight / Story Points" },
            [K.FieldActivityWeightHint] = new[]
            {
                "Campo numérico sincronizado com o campo de pontos configurado no Jira.",
                "Numeric field synchronized with the points field configured in Jira."
            },
            [K.FieldAssignee] = new[] { "Responsável", "Assignee" },
            [K.FieldAssigneeSearch] = new[] { "Pesquisar responsável", "Search assignee" },
            [K.BtnAssignSelf] = new[] { "Atribuir a mim", "Assign to me" },
            [K.AssigneeNone] = new[] { "— Não atribuir —", "— Unassigned —" },
            [K.FieldTeam] = new[] { "Time", "Team" },
            [K.FieldStartDate] = new[] { "Data de início", "Start date" },
            [K.FieldDueDate] = new[] { "Data limite", "Due date" },
            [K.DateHint] = new[] { "Formato: DD-MM-AAAA", "Format: YYYY-MM-DD" },
            [K.BtnSelectFile] = new[] { "Selecionar arquivo / print", "Select file / screenshot" },
            [K.BtnCaptureGameView] = new[] { "Capturar Game View", "Capture Game View" },
            [K.BtnCaptureScreenArea] = new[] { "Recortar área da tela", "Snip screen area" },
            [K.BtnPasteClipboardImage] = new[] { "Colar imagem", "Paste image" },
            [K.AttachmentPreviewTitle] = new[] { "Pré-visualização", "Preview" },
            [K.AttachmentPreviewInfo] = new[]
            {
                "{0} × {1} px • {2}",
                "{0} × {1} px • {2}"
            },
            [K.AttachmentInlineDescriptionHint] = new[]
            {
                "Quando o arquivo for uma imagem, ela também será inserida automaticamente no final da descrição.",
                "When the file is an image, it will also be inserted automatically at the end of the description."
            },
            [K.CaptureGameViewHint] = new[]
            {
                "Captura a câmera principal e usa o PNG como anexo da issue.",
                "Captures the main camera and uses the PNG as the issue attachment."
            },
            [K.CaptureScreenAreaHint] = new[]
            {
                "Abre o recorte do Windows e anexa automaticamente a área selecionada.",
                "Opens Windows screen snipping and automatically attaches the selected area."
            },
            [K.MsgScreenClipWaiting] = new[]
            {
                "Selecione a área da tela que deseja anexar...",
                "Select the screen area you want to attach..."
            },
            [K.MsgScreenClipImported] = new[]
            {
                "Recorte pronto: {0}",
                "Screen snip ready: {0}"
            },
            [K.MsgClipboardImageImporting] = new[]
            {
                "Importando imagem da área de transferência...",
                "Importing image from the clipboard..."
            },
            [K.MsgClipboardImageImported] = new[]
            {
                "Imagem colada: {0}",
                "Pasted image: {0}"
            },
            [K.MsgClipboardNoImage] = new[]
            {
                "A área de transferência não contém uma imagem.",
                "The clipboard does not contain an image."
            },
            [K.MsgScreenClipCancelled] = new[]
            {
                "Nenhum novo recorte foi encontrado.",
                "No new screen snip was found."
            },
            [K.MsgScreenClipFailed] = new[]
            {
                "Não foi possível importar o recorte da área de transferência.",
                "Could not import the screen snip from the clipboard."
            },
            [K.MsgScreenClipFailedWithReason] = new[]
            {
                "Falha ao importar o recorte: {0}",
                "Failed to import the screen snip: {0}"
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
            [K.MsgImageEmbedded] = new[]
            {
                " Imagem inserida na descrição.",
                " Image inserted into the description."
            },
            [K.MsgImageEmbedFailed] = new[]
            {
                " (Anexo enviado, mas não foi possível inserir a imagem na descrição: {0})",
                " (Attachment uploaded, but the image could not be inserted into the description: {0})"
            },
            [K.SettingsClearPresets] = new[] { "Limpar campos salvos (presets)", "Clear saved fields (presets)" },
            [K.MsgPresetsCleared] = new[] { "Presets removidos.", "Presets cleared." },
            [K.MsgRequiredField] = new[] { "O campo \"{0}\" é obrigatório.", "The \"{0}\" field is required." },
            [K.MsgInvalidNumber] = new[] { "O campo \"{0}\" precisa ser um número válido.", "The \"{0}\" field must be a valid number." },
            [K.MsgInvalidDate] = new[]
            {
                "A data do campo \"{0}\" deve usar o formato DD-MM-AAAA.",
                "The date in the \"{0}\" field must use the YYYY-MM-DD format."
            },
            [K.FieldQuickSubtask] = new[] { "Subtarefas rápidas (opcional)", "Quick subtasks (optional)" },
            [K.FieldQuickSubtaskTitle] = new[] { "Título", "Title" },
            [K.FieldQuickSubtaskDescription] = new[] { "Descrição", "Description" },
            [K.FieldQuickSubtaskPriority] = new[] { "Prioridade", "Priority" },
            [K.FieldSubtaskAttachment] = new[] { "Anexo da subtarefa", "Subtask attachment" },
            [K.BtnAddQuickSubtask] = new[] { "+ Adicionar subtarefa", "+ Add subtask" },
            [K.BtnRemoveQuickSubtask] = new[] { "Remover subtarefa", "Remove subtask" },
            [K.QuickSubtaskNumber] = new[] { "Subtarefa {0}", "Subtask {0}" },
            [K.QuickSubtaskHint] = new[]
            {
                "Use + para adicionar e − para remover. Cada subtarefa pode ter sua própria prioridade.",
                "Use + to add and − to remove. Each subtask can have its own priority."
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

            [K.TabResolve] = new[] { "Atividades", "Activities" },
            [K.ResolveNoticeText] = new[]
            {
                "Para resolver issues, valide sua conexão na aba \"Conexão\".",
                "To resolve issues, validate your connection on the \"Connection\" tab."
            },
            [K.ResolveFiltersTitle] = new[] { "Filtros das atividades", "Activity filters" },
            [K.ResolveIssueListTitle] = new[] { "Atividades", "Activities" },
            [K.ResolveStatusFilter] = new[] { "Status", "Status" },
            [K.StatusSearchLabel] = new[] { "Pesquisar status", "Search statuses" },
            [K.StatusSearchExample] = new[] { "Ex.: aberto, revisão ou teste", "Example: open, review, or test" },
            [K.StatusCategoryTodo] = new[] { "A fazer", "To do" },
            [K.StatusCategoryInProgress] = new[] { "Em andamento", "In progress" },
            [K.StatusCategoryDone] = new[] { "Concluído", "Done" },
            [K.StatusCategoryOther] = new[] { "Outros", "Other" },
            [K.ResolveEpicSearch] = new[] { "Pesquisar projeto / épico", "Search project / epic" },
            [K.ResolveEpicFilter] = new[] { "Projeto / Épico", "Project / Epic" },
            [K.ResolveAllEpics] = new[] { "Todos", "All" },
            [K.ResolveEpicNoResults] = new[] { "Nenhum projeto ou épico encontrado.", "No project or epic found." },
            [K.ResolveEpicAllSelected] = new[] { "Filtro atual: todos os épicos", "Current filter: all epics" },
            [K.ResolveEpicSelected] = new[] { "Selecionado: {0} — {1}", "Selected: {0} — {1}" },
            [K.ResolveOwnerScope] = new[] { "Responsável", "Assignee" },
            [K.ResolveOwnerMine] = new[] { "Minhas atividades", "My activities" },
            [K.ResolveOwnerEveryone] = new[] { "Todas as pessoas", "Everyone" },
            [K.ResolveOwnerSearch] = new[] { "Pesquisar responsável", "Search assignee" },
            [K.ResolveOwnerSearchHint] = new[]
            {
                "Digite ao menos 2 caracteres do nome ou e-mail.",
                "Enter at least 2 characters from the name or email."
            },
            [K.ResolveOwnerSearching] = new[] { "Pesquisando pessoas...", "Searching people..." },
            [K.ResolveOwnerNoResults] = new[] { "Nenhuma pessoa encontrada.", "No people found." },
            [K.ResolveSprintScope] = new[] { "Localização das atividades", "Activity location" },
            [K.ResolveSprintAll] = new[] { "Todas — sprint e backlog", "All — sprint and backlog" },
            [K.ResolveSprintActive] = new[] { "Somente sprint ativa", "Active sprint only" },
            [K.ResolveSprintBacklog] = new[] { "Somente backlog", "Backlog only" },
            [K.MsgResolveEpicLoadFailed] = new[]
            {
                "Não foi possível carregar os projetos/épicos: {0}",
                "Could not load projects/epics: {0}"
            },
            [K.MsgResolveStatusLoadFailed] = new[]
            {
                "Não foi possível sincronizar os status do Jira: {0}",
                "Could not sync Jira statuses: {0}"
            },
            [K.FilterAll] = new[] { "Todos", "All" },
            [K.BtnReload] = new[] { "Recarregar", "Reload" },
            [K.SearchIssuesLabel] = new[] { "Filtrar por chave ou título", "Filter by key or title" },
            [K.SearchIssuesExample] = new[]
            {
                "Ex.: PROJ-123, corrigir login ou parte do título de uma subtarefa.",
                "Example: PROJ-123, fix login, or part of a subtask title."
            },
            [K.MsgLoadingIssues] = new[] { "Carregando issues...", "Loading issues..." },
            [K.MsgNoIssues] = new[] { "Nenhuma issue encontrada para este filtro.", "No issues found for this filter." },
            [K.ResolvePageFormat] = new[] { "Página {0} de {1} • {2} atividades", "Page {0} of {1} • {2} activities" },
            [K.PreviousPageTooltip] = new[] { "Página anterior", "Previous page" },
            [K.NextPageTooltip] = new[] { "Próxima página", "Next page" },
            [K.PinTooltip] = new[] { "Fixar / desafixar", "Pin / unpin" },
            [K.PriorityNotSet] = new[] { "Prioridade não definida", "Priority not set" },
            [K.PriorityDropdownTooltip] = new[] { "Alterar a prioridade de {0}", "Change the priority of {0}" },
            [K.SelectIssueHint] = new[] { "Selecione uma atividade na lista para atualizar.", "Select an activity from the list to update." },
            [K.CloseIssueTooltip] = new[] { "Fechar atividade sem editar", "Close activity without editing" },
            [K.ResolveEditTitle] = new[] { "Editar atividade", "Edit activity" },
            [K.BtnSaveIssueChanges] = new[] { "Salvar alterações", "Save changes" },
            [K.MsgLoadingIssueEdit] = new[] { "Carregando dados da atividade...", "Loading activity data..." },
            [K.MsgSavingIssueEdit] = new[] { "Salvando alterações...", "Saving changes..." },
            [K.MsgIssueSummaryRequired] = new[] { "Informe o título da atividade.", "Enter the activity title." },
            [K.MsgIssueEditSaved] = new[] { "Alterações da atividade salvas.", "Activity changes saved." },
            [K.MsgNoIssueChanges] = new[] { "Nenhuma alteração para salvar.", "No changes to save." },
            [K.ResolveSubtasksTitle] = new[] { "Subtarefas", "Subtasks" },
            [K.ResolveNoSubtasks] = new[] { "Esta atividade não possui subtarefas.", "This activity has no subtasks." },
            [K.ResolveSubtaskCount] = new[] { "Total: {0}", "Total: {0}" },
            [K.ResolveChildActivitiesTitle] = new[] { "Atividades filhas", "Child activities" },
            [K.ResolveNoChildActivities] = new[] { "Este épico não possui atividades filhas.", "This epic has no child activities." },
            [K.BtnAddChildActivity] = new[] { "+ Adicionar atividade filha", "+ Add child activity" },
            [K.FieldChildActivityType] = new[] { "Tipo da atividade filha", "Child activity type" },
            [K.FieldChildActivityTitle] = new[] { "Título da atividade filha", "Child activity title" },
            [K.FieldChildActivityDescription] = new[] { "Descrição da atividade filha", "Child activity description" },
            [K.FieldChildActivityAttachment] = new[] { "Anexo da atividade filha", "Child activity attachment" },
            [K.BtnCreateChildActivity] = new[] { "Criar atividade filha", "Create child activity" },
            [K.MsgCreatingChildActivity] = new[] { "Criando atividade filha...", "Creating child activity..." },
            [K.MsgChildActivityCreated] = new[]
            {
                "Atividade filha {0} criada e vinculada.",
                "Child activity {0} created and linked."
            },
            [K.MsgChildTypeUnavailable] = new[]
            {
                "O projeto não possui um tipo de atividade filha disponível para este nível.",
                "The project has no child activity type available for this level."
            },
            [K.MsgIssueCannotHaveChildren] = new[]
            {
                "Este tipo de atividade não pode receber outras atividades.",
                "This activity type cannot have child activities."
            },
            [K.BtnCreateSubtask] = new[] { "Criar subtarefa", "Create subtask" },
            [K.MsgCreatingSubtask] = new[] { "Criando subtarefa...", "Creating subtask..." },
            [K.MsgSubtaskCreated] = new[]
            {
                "Subtarefa {0} criada e vinculada.",
                "Subtask {0} created and linked."
            },
            [K.MsgSubtaskTypeUnavailable] = new[]
            {
                "O projeto não possui um tipo de subtarefa disponível.",
                "The project does not have an available subtask type."
            },
            [K.MsgIssueCannotHaveSubtasks] = new[]
            {
                "Este tipo de atividade não pode receber subtarefas.",
                "This activity type cannot have subtasks."
            },
            [K.MsgResolveProjectUnknown] = new[]
            {
                "Não foi possível identificar o projeto da atividade.",
                "Could not identify the activity project."
            },
            [K.ResolveSubtaskCountCompact] = new[] { "{0} subt.", "{0} sub." },
            [K.ResolveSubtaskCountTooltip] = new[] { "Subtarefas: {0}", "Subtasks: {0}" },
            [K.OpenSubtaskTooltip] = new[] { "Abrir {0}: {1}", "Open {0}: {1}" },
            [K.BtnBackToParent] = new[] { "← Voltar para {0}", "← Back to {0}" },
            [K.BackToParentTooltip] = new[] { "Voltar para a atividade pai: {0}", "Back to parent activity: {0}" },
            [K.MsgUpdatingPriority] = new[] { "Atualizando prioridade de {0}...", "Updating priority for {0}..." },
            [K.MsgPriorityApplied] = new[] { "Prioridade alterada para \"{0}\".", "Priority changed to \"{0}\"." },
            [K.StatusDropdownTooltip] = new[] { "Alterar o status de {0}", "Change the status of {0}" },
            [K.StatusLoading] = new[] { "Carregando...", "Loading..." },
            [K.FieldTransition] = new[] { "Transição (workflow)", "Transition (workflow)" },
            [K.ResolveUpdateTitle] = new[] { "Adicionar atualização", "Add update" },
            [K.FieldComment] = new[] { "Comentário da atividade", "Activity comment" },
            [K.FieldMention] = new[] { "Mencionar pessoas", "Mention people" },
            [K.MentionSearchPlaceholder] = new[] { "Buscar pessoa para mencionar...", "Search a person to mention..." },
            [K.AttachFixHint] = new[] { "Anexe o print/arquivo do fix (opcional).", "Attach the fix screenshot/file (optional)." },
            [K.BtnComment] = new[] { "Comentar", "Comment" },
            [K.BtnApplyTransition] = new[] { "Aplicar transição", "Apply transition" },
            [K.BtnResolveMarked] = new[] { "Atualizar atividade", "Update activity" },
            [K.BtnUpdateActivity] = new[] { "Atualizar atividade", "Update activity" },
            [K.MsgCommentRequired] = new[] { "Escreva um comentário.", "Write a comment." },
            [K.MsgActivityRequired] = new[]
            {
                "Escreva um comentário, mencione alguém ou anexe um arquivo.",
                "Write a comment, mention someone, or attach a file."
            },
            [K.MsgTransitionRequired] = new[] { "Selecione uma transição.", "Select a transition." },
            [K.MsgResolving] = new[] { "Enviando para o Jira...", "Sending to Jira..." },
            [K.MsgCommented] = new[] { "Comentário adicionado.", "Comment added." },
            [K.MsgActivityUpdated] = new[] { "Atividade atualizada.", "Activity updated." },
            [K.MsgUpdatingStatus] = new[] { "Atualizando status de {0}...", "Updating status for {0}..." },
            [K.MsgTransitionApplied] = new[] { "Issue movida para \"{0}\".", "Issue moved to \"{0}\"." },
            [K.MsgAttachmentSent] = new[] { " Anexo enviado.", " Attachment uploaded." },
            [K.MsgAttachSendFailed] = new[] { " (Anexo não enviado: {0})", " (Attachment not uploaded: {0})" },
            [K.MsgResolveFailed] = new[] { "Falha: {0}", "Error: {0}" },
            [K.MsgNoTransitions] = new[] { "Nenhuma transição disponível para você nesta issue.", "No transitions available to you for this issue." },

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

            // --- Git / GitHub integration ---
            [K.GitSettingsTitle] = new[] { "Integração Git / GitHub", "Git / GitHub integration" },
            [K.GitSettingsNote] = new[]
            {
                "Padroniza o nome do branch e a mensagem de commit por atividade (ex.: feat(PROJ-123): título). Nada é enviado ao GitHub — a ligação com o Jira é feita pelo app oficial \"GitHub for Jira\".",
                "Standardizes the branch name and commit message per activity (e.g. feat(PROJ-123): title). Nothing is pushed to GitHub — the Jira link is handled by the official \"GitHub for Jira\" app."
            },
            [K.GitEnableToggle] = new[] { "Habilitar integração Git", "Enable Git integration" },
            [K.GitRepoPathLabel] = new[] { "Pasta do repositório (vazio = detectar)", "Repository folder (empty = auto-detect)" },
            [K.BtnDetectRepo] = new[] { "Detectar", "Detect" },
            [K.GitBaseBranchLabel] = new[] { "Branch base", "Base branch" },
            [K.GitBranchTemplateLabel] = new[] { "Template do branch", "Branch template" },
            [K.GitCommitTemplateLabel] = new[] { "Template do commit", "Commit template" },
            [K.GitTemplateHint] = new[]
            {
                "Placeholders: {type} {key} {slug} {title}.",
                "Placeholders: {type} {key} {slug} {title}."
            },
            [K.GitNativeLinkNote] = new[]
            {
                "Para o Jira mostrar branches/commits/PRs no painel Development e mover a issue automaticamente (ex.: PR aberto → Code Review, PR merjado → Concluído), instale o app \"GitHub for Jira\" e crie regras de Automation. Basta a chave (ex.: PROJ-123) aparecer no branch/commit/PR.",
                "For Jira to show branches/commits/PRs in the Development panel and move the issue automatically (e.g. PR opened → Code Review, PR merged → Done), install the \"GitHub for Jira\" app and create Automation rules. Just include the key (e.g. PROJ-123) in the branch/commit/PR."
            },
            [K.BtnInstallGithubJira] = new[] { "Instalar GitHub for Jira", "Install GitHub for Jira" },
            [K.GitCardTitle] = new[] { "Git — branch & commit", "Git — branch & commit" },
            [K.GitTypeLabel] = new[] { "Tipo (Conventional)", "Type (Conventional)" },
            [K.GitBranchPreview] = new[] { "Branch", "Branch" },
            [K.GitCommitPreview] = new[] { "Commit", "Commit" },
            [K.GitCurrentBranch] = new[] { "Branch atual: {0}", "Current branch: {0}" },
            [K.BtnGitCreateBranch] = new[] { "Criar / checkout branch", "Create / checkout branch" },
            [K.BtnGitCopyCommit] = new[] { "Copiar commit", "Copy commit" },
            [K.BtnGitCopyBranch] = new[] { "Copiar branch", "Copy branch" },
            [K.MsgGitRepoDetected] = new[] { "Repositório: {0}", "Repository: {0}" },
            [K.MsgGitRepoNotFound] = new[]
            {
                "Nenhum repositório Git encontrado a partir do projeto. Informe a pasta manualmente.",
                "No Git repository found from the project. Set the folder manually."
            },
            [K.MsgGitNotInstalled] = new[]
            {
                "Git não encontrado no PATH. Instale o Git para usar esta integração.",
                "Git was not found on PATH. Install Git to use this integration."
            },
            [K.MsgGitWorking] = new[] { "Executando git...", "Running git..." },
            [K.MsgGitBranchReady] = new[] { "Branch pronto: {0}", "Branch ready: {0}" },
            [K.MsgGitBranchFailed] = new[] { "Falha no git: {0}", "Git error: {0}" },
            [K.MsgGitCopiedCommit] = new[] { "Mensagem de commit copiada.", "Commit message copied." },
            [K.MsgGitCopiedBranch] = new[] { "Nome do branch copiado.", "Branch name copied." },

            // --- Local agent ---

            [K.TabAgent] = new[] { "Agente", "Agent" },
            [K.AgentSectionTitle] = new[] { "Agente local", "Local agent" },
            [K.AgentIntro] = new[]
            {
                "Descreva a tarefa e o agente trabalha no repositório deste projeto. A execução acontece "
                + "em background e o progresso aparece abaixo; você pode recompilar ou fechar o Unity sem perder a execução.",
                "Describe the task and the agent works on this project's repository. The run happens in the "
                + "background and progress shows below; you can recompile or close Unity without losing the run."
            },
            [K.AgentTaskLabel] = new[] { "Tarefa", "Task" },
            [K.AgentTaskPlaceholder] = new[]
            {
                "Ex.: investigue por que o autoplay não para no fim da rodada e proponha a correção.",
                "E.g.: investigate why autoplay does not stop at the end of the round and propose a fix."
            },
            [K.AgentIssueLabel] = new[] { "Atividade vinculada", "Linked issue" },
            [K.AgentNoIssue] = new[] { "Nenhuma (tarefa livre)", "None (free task)" },
            [K.BtnAgentRun] = new[] { "Executar em background", "Run in background" },
            [K.BtnAgentRunning] = new[] { "Executando...", "Running..." },
            [K.BtnAgentTerminal] = new[] { "Abrir no terminal", "Open in terminal" },
            [K.BtnAgentCancel] = new[] { "Cancelar execução", "Cancel run" },
            [K.BtnAgentRefresh] = new[] { "Recarregar", "Reload" },
            [K.BtnAgentDelete] = new[] { "Excluir", "Delete" },
            [K.BtnAgentCopyResult] = new[] { "Copiar resultado", "Copy result" },
            [K.BtnAgentOpenFolder] = new[] { "Abrir pasta da execução", "Open run folder" },
            [K.BtnAgentSendToAgent] = new[] { "Enviar para o agente", "Send to agent" },

            [K.AgentPermissionLabel] = new[] { "Permissões", "Permissions" },
            [K.AgentPermissionPlan] = new[] { "Somente leitura (investigar)", "Read-only (investigate)" },
            [K.AgentPermissionDefault] = new[] { "Padrão da CLI (pergunta ao editar)", "CLI default (asks before editing)" },
            [K.AgentPermissionAcceptEdits] = new[] { "Editar arquivos sem perguntar", "Edit files without asking" },
            [K.AgentPermissionNote] = new[]
            {
                "Em background ninguém responde a uma pergunta de permissão: no modo padrão o agente "
                + "para ao precisar editar. Use “Editar arquivos sem perguntar” para uma execução que deve alterar o projeto.",
                "In a background run nobody can answer a permission prompt: in the default mode the agent "
                + "stops when it needs to edit. Use “Edit files without asking” for a run that is meant to change the project."
            },

            [K.AgentCliTitle] = new[] { "CLI do agente", "Agent CLI" },
            [K.AgentCliFound] = new[] { "{0} encontrado: {1}", "{0} found: {1}" },
            [K.AgentCliMissing] = new[]
            {
                "{0} não encontrado nesta máquina. Instale a CLI para usar o agente.",
                "{0} was not found on this machine. Install the CLI to use the agent."
            },
            [K.AgentCliOverrideMissing] = new[]
            {
                "O caminho informado não existe: {0}",
                "The configured path does not exist: {0}"
            },
            [K.AgentCliChecking] = new[] { "Verificando a CLI...", "Checking the CLI..." },
            [K.AgentCliPathLabel] = new[] { "Caminho da CLI (opcional)", "CLI path (optional)" },
            [K.AgentCliPathHint] = new[]
            {
                "Deixe vazio para detecção automática. Preencha se o Unity não herdar o PATH do seu shell.",
                "Leave empty for auto-detection. Fill it in when Unity does not inherit your shell PATH."
            },
            [K.BtnAgentCheckCli] = new[] { "Verificar novamente", "Check again" },
            [K.BtnAgentInstallCli] = new[] { "Como instalar", "How to install" },
            [K.BtnAgentCopyInstall] = new[] { "Copiar comando", "Copy command" },
            [K.MsgAgentInstallCopied] = new[] { "Comando de instalação copiado.", "Install command copied." },

            [K.AgentSkillTitle] = new[] { "Instruções do projeto", "Project instructions" },
            [K.AgentSkillNote] = new[]
            {
                "Gera as instruções do projeto para o agente: convenção de branch e commit, cuidados com "
                + ".meta, prefabs e cenas, e como o resultado volta para esta janela. Nenhum token é gravado no arquivo.",
                "Generates the project instructions for the agent: branch and commit conventions, care with "
                + ".meta files, prefabs and scenes, and how the result comes back to this window. No token is written to the file."
            },
            [K.BtnAgentInstallSkill] = new[] { "Gerar / atualizar", "Generate / update" },
            [K.BtnAgentPreviewSkill] = new[] { "Ver conteúdo", "Preview" },
            [K.MsgAgentSkillInstalled] = new[] { "Instruções gravadas em {0}", "Instructions written to {0}" },
            [K.MsgAgentSkillFailed] = new[] { "Não foi possível gravar: {0}", "Could not write: {0}" },
            [K.MsgAgentSkillPresent] = new[] { "Instaladas em {0}", "Installed at {0}" },
            [K.MsgAgentSkillAbsent] = new[] { "Ainda não geradas neste repositório.", "Not generated in this repository yet." },

            [K.AgentRunsTitle] = new[] { "Execuções", "Runs" },
            [K.AgentNoRuns] = new[] { "Nenhuma execução ainda.", "No runs yet." },
            [K.AgentTranscriptTitle] = new[] { "Transcrição", "Transcript" },
            [K.AgentResultTitle] = new[] { "Resultado", "Result" },
            [K.AgentWaitingFirstEvent] = new[]
            {
                "Aguardando o primeiro evento da CLI...",
                "Waiting for the first event from the CLI..."
            },

            [K.AgentStatusRunning] = new[] { "Executando", "Running" },
            [K.AgentStatusSucceeded] = new[] { "Concluído", "Done" },
            [K.AgentStatusFailed] = new[] { "Falhou", "Failed" },
            [K.AgentStatusCanceled] = new[] { "Cancelado", "Canceled" },
            [K.AgentStatusOrphaned] = new[] { "Interrompido", "Interrupted" },

            [K.AgentEventThinking] = new[] { "pensando", "thinking" },
            [K.AgentEventTool] = new[] { "ferramenta", "tool" },
            [K.AgentEventStarted] = new[] { "sessão iniciada", "session started" },
            [K.AgentEventError] = new[] { "erro", "error" },

            [K.AgentMetaCost] = new[] { "≈ US$ {0}", "≈ US$ {0}" },
            [K.AgentMetaDuration] = new[] { "{0} s", "{0} s" },
            [K.MsgAgentNoTask] = new[] { "Descreva a tarefa antes de executar.", "Describe the task before running." },
            [K.MsgAgentStartFailed] = new[] { "Não foi possível iniciar: {0}", "Could not start: {0}" },
            [K.MsgAgentTerminalFailed] = new[] { "Não foi possível abrir o terminal: {0}", "Could not open the terminal: {0}" },
            [K.MsgAgentCopied] = new[] { "Resultado copiado.", "Result copied." },
            [K.MsgAgentWorkingDir] = new[] { "Pasta de trabalho: {0}", "Working directory: {0}" },

            [K.AgentModelLabel] = new[] { "Modelo", "Model" },
            [K.AgentModelCliDefault] = new[] { "Padrão da CLI", "CLI default" },
            [K.AgentModelNote] = new[]
            {
                "“Padrão da CLI” não envia --model e mantém o que você configurou na CLI. "
                + "Um modelo mais barato serve bem para tarefas mecânicas.",
                "“CLI default” sends no --model and keeps whatever you configured in the CLI. "
                + "A cheaper model works well for mechanical tasks."
            },

            [K.BtnAgentContinue] = new[] { "Continuar esta execução", "Continue this run" },
            [K.AgentContinueNote] = new[]
            {
                "Continuar reaproveita o contexto da sessão anterior, em vez de pagar tudo de novo. "
                + "Escreva o próximo passo no campo Tarefa e clique aqui.",
                "Continuing reuses the previous session's context instead of paying for it again. "
                + "Write the next step in the Task field and click here."
            },
            [K.AgentContinueUnavailable] = new[]
            {
                "Esta execução não pode ser continuada (sem id de sessão ou provedor sem suporte).",
                "This run cannot be continued (no session id, or the provider does not support it)."
            },
            [K.MsgAgentNoFollowUp] = new[]
            {
                "Escreva o próximo passo no campo Tarefa antes de continuar.",
                "Write the next step in the Task field before continuing."
            },
            [K.AgentResumedFrom] = new[] { "continuação de {0}", "continued from {0}" },

            [K.BtnAgentDiagnostics] = new[] { "Copiar diagnóstico", "Copy diagnostics" },
            [K.MsgAgentDiagnosticsCopied] = new[]
            {
                "Diagnóstico copiado e enviado ao Console.",
                "Diagnostics copied and written to the Console."
            },

            [K.AgentProviderLabel] = new[] { "Agente", "Agent" },
            [K.AgentNoApiKeyNote] = new[]
            {
                "Este recurso não usa API Key. A CLI entra com a conta em que você já está logado, "
                + "consumindo o seu plano — é independente do Assistente de IA das Configurações, que é cobrado por token.",
                "This feature uses no API Key. The CLI signs in with the account you are already logged into, "
                + "consuming your plan — independent of the AI Assistant in Settings, which is billed per token."
            },

            [K.AgentCliReady] = new[]
            {
                "pronto",
                "ready"
            },
            [K.AgentCliMissingShort] = new[]
            {
                "{0} não encontrado",
                "{0} not found"
            },
            [K.AgentCliMissingHint] = new[]
            {
                "Instale a CLI ou informe o caminho dela em Configurações > Agente local.",
                "Install the CLI or set its path in Settings > Local agent."
            },
            [K.BtnAgentNewChat] = new[]
            {
                "Nova conversa",
                "New chat"
            },
            [K.BtnAgentHistory] = new[]
            {
                "Histórico",
                "History"
            },
            [K.BtnAgentConfigure] = new[]
            {
                "Configurar",
                "Configure"
            },
            [K.BtnAgentSend] = new[]
            {
                "Enviar",
                "Send"
            },
            [K.BtnAgentDeleteChat] = new[]
            {
                "Excluir conversa",
                "Delete chat"
            },
            [K.BtnAgentUnlinkIssue] = new[]
            {
                "Desvincular",
                "Unlink"
            },
            [K.AgentChatEmpty] = new[]
            {
                "Escreva a primeira mensagem abaixo. O agente roda em background, na pasta do repositório, e cada resposta continua a mesma sessão da CLI.",
                "Write the first message below. The agent runs in the background, in the repository folder, and each reply continues the same CLI session."
            },
            [K.AgentComposerHint] = new[]
            {
                "Enter envia · Shift+Enter quebra linha. As permissões acima valem para a próxima mensagem.",
                "Enter sends · Shift+Enter adds a line. The permission above applies to the next message."
            },
            [K.AgentYou] = new[]
            {
                "Você",
                "You"
            },
            [K.AgentActivitySteps] = new[]
            {
                "{0} passos da CLI",
                "{0} CLI steps"
            },
            [K.AgentMetaTokens] = new[]
            {
                "{0} tokens",
                "{0} tokens"
            },
            [K.MsgAgentBusy] = new[]
            {
                "O agente ainda está trabalhando nesta conversa. Aguarde ou cancele a execução.",
                "The agent is still working on this chat. Wait for it or cancel the run."
            },
            [K.AgentUsageIdle] = new[]
            {
                "tokens: janela livre",
                "tokens: window clear"
            },
            [K.AgentUsageNoBudget] = new[]
            {
                "{0} tokens nesta janela · sem limite definido",
                "{0} tokens this window · no budget set"
            },
            [K.AgentUsageSummary] = new[]
            {
                "{0} usados · restam {1}% ({2})",
                "{0} used · {1}% left ({2})"
            },
            [K.AgentUsageReset] = new[]
            {
                "reseta {0} (em {1})",
                "resets {0} (in {1})"
            },
            [K.AgentSettingsTitle] = new[]
            {
                "Agente local",
                "Local agent"
            },
            [K.AgentSkillPathLabel] = new[]
            {
                "Arquivo de instruções ({0})",
                "Instructions file ({0})"
            },
            [K.BtnAgentOpenSkill] = new[]
            {
                "Abrir no explorador",
                "Reveal file"
            },
            [K.AgentEnvTitle] = new[]
            {
                "Credenciais do agente (jira.env)",
                "Agent credentials (jira.env)"
            },
            [K.AgentEnvNote] = new[]
            {
                "Arquivo único, em ~/.claude/jira.env — o mesmo que a skill do Jira lê. Criado automaticamente, guarda a conexão com o Jira e é exportado para a CLI antes de cada execução.",
                "A single file at ~/.claude/jira.env — the one the Jira skill reads. Created automatically, it holds the Jira connection and is exported to the CLI before every run."
            },
            [K.AgentEnvEnabledLabel] = new[]
            {
                "Exportar estas variáveis nas execuções",
                "Export these variables on every run"
            },
            [K.AgentEnvPathLabel] = new[]
            {
                "Arquivo (opcional)",
                "File (optional)"
            },
            [K.AgentEnvPathHint] = new[]
            {
                "Vazio usa ~/.claude/{0}, compartilhado por todos os projetos desta máquina. Caminho relativo parte da pasta do projeto; absoluto é usado como está.",
                "Empty uses ~/.claude/{0}, shared by every project on this machine. A relative path starts at the project folder; an absolute one is used as given."
            },
            [K.AgentEnvSecretsNote] = new[]
            {
                "Uma variável por linha, CHAVE=valor, sem interpolação. O arquivo guarda o seu token do Jira: por isso fica na sua pasta pessoal, fora do repositório, e não deve ser compartilhado.",
                "One variable per line, KEY=value, no interpolation. The file holds your Jira token: that is why it lives in your home folder, outside the repository, and must not be shared."
            },
            [K.BtnAgentEnvSave] = new[]
            {
                "Salvar .env",
                "Save .env"
            },
            [K.BtnAgentEnvReload] = new[]
            {
                "Recarregar",
                "Reload"
            },
            [K.BtnAgentEnvTemplate] = new[]
            {
                "Inserir exemplo",
                "Insert example"
            },
            [K.BtnAgentEnvReveal] = new[]
            {
                "Abrir no explorador",
                "Reveal file"
            },
            [K.MsgAgentEnvPath] = new[]
            {
                "Arquivo: {0}",
                "File: {0}"
            },
            [K.MsgAgentEnvPathAbsent] = new[]
            {
                "Ainda não existe: {0}",
                "Does not exist yet: {0}"
            },
            [K.MsgAgentEnvVars] = new[]
            {
                "{0} variáveis serão exportadas.",
                "{0} variables will be exported."
            },
            [K.MsgAgentEnvDisabled] = new[]
            {
                "Exportação desligada: nenhuma variável é enviada.",
                "Export is off: no variable is sent."
            },
            [K.MsgAgentEnvSaved] = new[]
            {
                "Gravado em {0}",
                "Written to {0}"
            },
            [K.MsgAgentEnvFailed] = new[]
            {
                "Não foi possível gravar: {0}",
                "Could not write: {0}"
            },
            [K.MsgAgentEnvMissing] = new[]
            {
                "O arquivo ainda não existe. Salve antes de abrir.",
                "The file does not exist yet. Save it before opening."
            },
            [K.MsgAgentEnvTemplate] = new[]
            {
                "Exemplo inserido. Ajuste e salve.",
                "Example inserted. Adjust it and save."
            },
            [K.AgentBudgetTitle] = new[]
            {
                "Consumo de tokens",
                "Token usage"
            },
            [K.AgentBudgetNote] = new[]
            {
                "Nenhuma CLI informa quanto resta do plano, então a aba Agente mede o consumo desta máquina contra o limite abaixo e agrupa por janela.",
                "No CLI reports the plan's remaining quota, so the Agent tab measures this machine's consumption against the budget below and groups it per window."
            },
            [K.AgentBudgetLabel] = new[]
            {
                "Limite de tokens por janela",
                "Token budget per window"
            },
            [K.AgentBudgetHint] = new[]
            {
                "Zero desliga a porcentagem e deixa só os números brutos.",
                "Zero turns the percentage off and leaves the raw counters."
            },
            [K.AgentWindowHoursLabel] = new[]
            {
                "Duração da janela (horas)",
                "Window length (hours)"
            },
            [K.AgentWindowHoursHint] = new[]
            {
                "5 horas é o ciclo dos planos Claude. A janela abre na primeira execução após um intervalo sem uso e o reset é o fim dela.",
                "5 hours is the Claude plans' cycle. A window opens with the first run after a quiet period and the reset is its end."
            },
            [K.BtnAgentClearUsage] = new[]
            {
                "Zerar histórico de consumo",
                "Clear usage history"
            },
            [K.MsgAgentUsageCleared] = new[]
            {
                "Histórico de consumo apagado.",
                "Usage history cleared."
            },
            [K.MsgAgentUsageIdle] = new[]
            {
                "Nenhuma janela ativa: o limite está inteiro.",
                "No active window: the budget is whole."
            },
            [K.MsgAgentUsageWindow] = new[]
            {
                "{0} tokens em {1} execuções · janela {2} → {3} · US$ {4}",
                "{0} tokens across {1} runs · window {2} → {3} · US$ {4}"
            },
            [K.AgentUsageEstimateNote] = new[]
            {
                "Os números vêm do que a própria CLI reporta ao fim de cada execução, contando cache. É uma estimativa local do uso deste projeto, não uma leitura da sua conta.",
                "The numbers come from what the CLI itself reports at the end of each run, cache included. It is a local estimate of this project's usage, not a reading of your account."
            },

            [K.BtnAgentEnvCreate] = new[]
            {
                "Criar arquivo jira.env",
                "Create jira.env file"
            },
            [K.BtnAgentEnvFill] = new[]
            {
                "Preencher com a conexão",
                "Fill from connection"
            },
            [K.MsgAgentEnvFilled] = new[]
            {
                "Conexão copiada para o editor. Revise e clique em Salvar .env.",
                "Connection copied into the editor. Review it and click Save .env."
            },
            [K.MsgAgentEnvNoConnection] = new[]
            {
                "Conecte-se ao Jira na aba Conexão primeiro.",
                "Connect to Jira in the Connection tab first."
            },
            [K.MsgAgentEnvJiraOk] = new[]
            {
                "JIRA_URL, JIRA_EMAIL e JIRA_API_TOKEN preenchidos: o agente consegue consultar o Jira.",
                "JIRA_URL, JIRA_EMAIL and JIRA_API_TOKEN are set: the agent can query Jira."
            },
            [K.MsgAgentEnvJiraMissing] = new[]
            {
                "Faltam JIRA_URL / JIRA_EMAIL / JIRA_API_TOKEN — sem eles o agente não consulta o Jira sozinho.",
                "JIRA_URL / JIRA_EMAIL / JIRA_API_TOKEN are missing — without them the agent cannot query Jira on its own."
            },

            [K.BtnAgentEnvTest] = new[]
            {
                "Testar conexão",
                "Test connection"
            },
            [K.MsgAgentEnvTesting] = new[]
            {
                "Chamando o Jira com estas credenciais...",
                "Calling Jira with these credentials..."
            },
            [K.MsgAgentEnvTestOk] = new[]
            {
                "Autenticado no Jira como {0}. O agente consegue usar estas credenciais.",
                "Authenticated with Jira as {0}. The agent can use these credentials."
            },
            [K.MsgAgentEnvTestFailed] = new[]
            {
                "O Jira recusou ou não respondeu: {0}",
                "Jira refused or did not answer: {0}"
            },
            [K.MsgAgentEnvApiKeyWarning] = new[]
            {
                "Atenção: ANTHROPIC_API_KEY definida aqui faz as execuções serem cobradas por token, mesmo com \"usar somente o plano\" ligado — uma chave escrita neste arquivo é tratada como intencional.",
                "Careful: an ANTHROPIC_API_KEY set here makes runs billed per token even with \"plan only\" on — a key written in this file is treated as deliberate."
            },
            [K.AgentPlanOnlyLabel] = new[]
            {
                "Usar somente o plano (sem cobrança por token)",
                "Use the plan only (no per-token billing)"
            },
            [K.AgentPlanOnlyHint] = new[]
            {
                "Remove ANTHROPIC_API_KEY, ANTHROPIC_BASE_URL e afins antes de chamar a CLI, para a execução entrar com a conta em que você já está logado. Desligue só se quiser cobrar uma conta de API de propósito.",
                "Clears ANTHROPIC_API_KEY, ANTHROPIC_BASE_URL and friends before calling the CLI, so the run signs in with the account you are already logged into. Turn it off only to bill an API account on purpose."
            },
            [K.AgentCostMeaningNote] = new[]
            {
                "O valor em US$ é o que a CLI reporta como equivalente daqueles tokens no preço da API — referência, não fatura. Logado no seu plano, ele consome a cota e não gera cobrança extra; com uma API Key no ambiente, aí sim seria cobrado, e é isso que a opção acima evita.",
                "The US$ figure is what the CLI reports as those tokens' equivalent at API prices — a reference, not an invoice. Signed into your plan it consumes quota and adds no charge; with an API key in the environment it would be billed, which is what the option above prevents."
            },

            [K.MsgAgentEnvEmailMismatch] = new[]
            {
                "O e-mail aqui ({0}) não é o da conexão da janela ({1}). O token pertence a uma conta só: com o e-mail errado o Jira responde 401, que parece token inválido e não é.",
                "The e-mail here ({0}) is not the one the window connected with ({1}). A token belongs to one account: with the wrong e-mail Jira answers 401, which looks like an invalid token and is not."
            },
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
            public const string MsgLoadingCreateDestination = "MsgLoadingCreateDestination";
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
            public const string MsgLoadingModule = "MsgLoadingModule";
            public const string DropdownNoOptions = "DropdownNoOptions";
            public const string DropdownSearchLabel = "DropdownSearchLabel";
            public const string MsgFieldsLoaded = "MsgFieldsLoaded";
            public const string MsgFieldsLoadFailed = "MsgFieldsLoadFailed";
            public const string MsgNoFieldsReturned = "MsgNoFieldsReturned";
            public const string MsgFieldsNotLoaded = "MsgFieldsNotLoaded";
            public const string CreateDatesTitle = "CreateDatesTitle";
            public const string CreateAdditionalFieldsTitle = "CreateAdditionalFieldsTitle";
            public const string AdditionalFieldsHint = "AdditionalFieldsHint";
            public const string ArrayFieldHint = "ArrayFieldHint";
            public const string SprintFieldHint = "SprintFieldHint";
            public const string AssociatedItemsSearch = "AssociatedItemsSearch";
            public const string AssociatedItemsSearchHint = "AssociatedItemsSearchHint";
            public const string AssociatedItemsSearching = "AssociatedItemsSearching";
            public const string AssociatedItemsNoResults = "AssociatedItemsNoResults";
            public const string TeamIdHint = "TeamIdHint";
            public const string CreateAttachmentTitle = "CreateAttachmentTitle";
            public const string FieldPriority = "FieldPriority";
            public const string FieldActivityWeight = "FieldActivityWeight";
            public const string FieldActivityWeightHint = "FieldActivityWeightHint";
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
            public const string BtnCaptureScreenArea = "BtnCaptureScreenArea";
            public const string BtnPasteClipboardImage = "BtnPasteClipboardImage";
            public const string AttachmentPreviewTitle = "AttachmentPreviewTitle";
            public const string AttachmentPreviewInfo = "AttachmentPreviewInfo";
            public const string AttachmentInlineDescriptionHint = "AttachmentInlineDescriptionHint";
            public const string CaptureGameViewHint = "CaptureGameViewHint";
            public const string CaptureScreenAreaHint = "CaptureScreenAreaHint";
            public const string MsgScreenClipWaiting = "MsgScreenClipWaiting";
            public const string MsgScreenClipImported = "MsgScreenClipImported";
            public const string MsgClipboardImageImporting = "MsgClipboardImageImporting";
            public const string MsgClipboardImageImported = "MsgClipboardImageImported";
            public const string MsgClipboardNoImage = "MsgClipboardNoImage";
            public const string MsgScreenClipCancelled = "MsgScreenClipCancelled";
            public const string MsgScreenClipFailed = "MsgScreenClipFailed";
            public const string MsgScreenClipFailedWithReason = "MsgScreenClipFailedWithReason";
            public const string MsgNoCameraForScreenshot = "MsgNoCameraForScreenshot";
            public const string MsgScreenshotCaptured = "MsgScreenshotCaptured";
            public const string MsgScreenshotFailed = "MsgScreenshotFailed";
            public const string BtnRemoveFile = "BtnRemoveFile";
            public const string NoFileSelected = "NoFileSelected";
            public const string StatusNote = "StatusNote";
            public const string PresetNote = "PresetNote";
            public const string MsgAttachmentAdded = "MsgAttachmentAdded";
            public const string MsgAttachmentFailed = "MsgAttachmentFailed";
            public const string MsgImageEmbedded = "MsgImageEmbedded";
            public const string MsgImageEmbedFailed = "MsgImageEmbedFailed";
            public const string SettingsClearPresets = "SettingsClearPresets";
            public const string MsgPresetsCleared = "MsgPresetsCleared";
            public const string MsgRequiredField = "MsgRequiredField";
            public const string MsgInvalidNumber = "MsgInvalidNumber";
            public const string MsgInvalidDate = "MsgInvalidDate";
            public const string FieldQuickSubtask = "FieldQuickSubtask";
            public const string FieldQuickSubtaskTitle = "FieldQuickSubtaskTitle";
            public const string FieldQuickSubtaskDescription = "FieldQuickSubtaskDescription";
            public const string FieldQuickSubtaskPriority = "FieldQuickSubtaskPriority";
            public const string FieldSubtaskAttachment = "FieldSubtaskAttachment";
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
            public const string TabResolve = "TabResolve";
            public const string ResolveNoticeText = "ResolveNoticeText";
            public const string ResolveFiltersTitle = "ResolveFiltersTitle";
            public const string ResolveIssueListTitle = "ResolveIssueListTitle";
            public const string ResolveStatusFilter = "ResolveStatusFilter";
            public const string StatusSearchLabel = "StatusSearchLabel";
            public const string StatusSearchExample = "StatusSearchExample";
            public const string StatusCategoryTodo = "StatusCategoryTodo";
            public const string StatusCategoryInProgress = "StatusCategoryInProgress";
            public const string StatusCategoryDone = "StatusCategoryDone";
            public const string StatusCategoryOther = "StatusCategoryOther";
            public const string ResolveEpicSearch = "ResolveEpicSearch";
            public const string ResolveEpicFilter = "ResolveEpicFilter";
            public const string ResolveAllEpics = "ResolveAllEpics";
            public const string ResolveEpicNoResults = "ResolveEpicNoResults";
            public const string ResolveEpicAllSelected = "ResolveEpicAllSelected";
            public const string ResolveEpicSelected = "ResolveEpicSelected";
            public const string ResolveOwnerScope = "ResolveOwnerScope";
            public const string ResolveOwnerMine = "ResolveOwnerMine";
            public const string ResolveOwnerEveryone = "ResolveOwnerEveryone";
            public const string ResolveOwnerSearch = "ResolveOwnerSearch";
            public const string ResolveOwnerSearchHint = "ResolveOwnerSearchHint";
            public const string ResolveOwnerSearching = "ResolveOwnerSearching";
            public const string ResolveOwnerNoResults = "ResolveOwnerNoResults";
            public const string ResolveSprintScope = "ResolveSprintScope";
            public const string ResolveSprintAll = "ResolveSprintAll";
            public const string ResolveSprintActive = "ResolveSprintActive";
            public const string ResolveSprintBacklog = "ResolveSprintBacklog";
            public const string MsgResolveEpicLoadFailed = "MsgResolveEpicLoadFailed";
            public const string MsgResolveStatusLoadFailed = "MsgResolveStatusLoadFailed";
            public const string FilterAll = "FilterAll";
            public const string BtnReload = "BtnReload";
            public const string SearchIssuesLabel = "SearchIssuesLabel";
            public const string SearchIssuesExample = "SearchIssuesExample";
            public const string MsgLoadingIssues = "MsgLoadingIssues";
            public const string MsgNoIssues = "MsgNoIssues";
            public const string ResolvePageFormat = "ResolvePageFormat";
            public const string PreviousPageTooltip = "PreviousPageTooltip";
            public const string NextPageTooltip = "NextPageTooltip";
            public const string PinTooltip = "PinTooltip";
            public const string PriorityNotSet = "PriorityNotSet";
            public const string PriorityDropdownTooltip = "PriorityDropdownTooltip";
            public const string SelectIssueHint = "SelectIssueHint";
            public const string CloseIssueTooltip = "CloseIssueTooltip";
            public const string ResolveEditTitle = "ResolveEditTitle";
            public const string BtnSaveIssueChanges = "BtnSaveIssueChanges";
            public const string MsgLoadingIssueEdit = "MsgLoadingIssueEdit";
            public const string MsgSavingIssueEdit = "MsgSavingIssueEdit";
            public const string MsgIssueSummaryRequired = "MsgIssueSummaryRequired";
            public const string MsgIssueEditSaved = "MsgIssueEditSaved";
            public const string MsgNoIssueChanges = "MsgNoIssueChanges";
            public const string ResolveSubtasksTitle = "ResolveSubtasksTitle";
            public const string ResolveNoSubtasks = "ResolveNoSubtasks";
            public const string ResolveSubtaskCount = "ResolveSubtaskCount";
            public const string ResolveChildActivitiesTitle = "ResolveChildActivitiesTitle";
            public const string ResolveNoChildActivities = "ResolveNoChildActivities";
            public const string BtnAddChildActivity = "BtnAddChildActivity";
            public const string FieldChildActivityType = "FieldChildActivityType";
            public const string FieldChildActivityTitle = "FieldChildActivityTitle";
            public const string FieldChildActivityDescription = "FieldChildActivityDescription";
            public const string FieldChildActivityAttachment = "FieldChildActivityAttachment";
            public const string BtnCreateChildActivity = "BtnCreateChildActivity";
            public const string MsgCreatingChildActivity = "MsgCreatingChildActivity";
            public const string MsgChildActivityCreated = "MsgChildActivityCreated";
            public const string MsgChildTypeUnavailable = "MsgChildTypeUnavailable";
            public const string MsgIssueCannotHaveChildren = "MsgIssueCannotHaveChildren";
            public const string BtnCreateSubtask = "BtnCreateSubtask";
            public const string MsgCreatingSubtask = "MsgCreatingSubtask";
            public const string MsgSubtaskCreated = "MsgSubtaskCreated";
            public const string MsgSubtaskTypeUnavailable = "MsgSubtaskTypeUnavailable";
            public const string MsgIssueCannotHaveSubtasks = "MsgIssueCannotHaveSubtasks";
            public const string MsgResolveProjectUnknown = "MsgResolveProjectUnknown";
            public const string ResolveSubtaskCountCompact = "ResolveSubtaskCountCompact";
            public const string ResolveSubtaskCountTooltip = "ResolveSubtaskCountTooltip";
            public const string OpenSubtaskTooltip = "OpenSubtaskTooltip";
            public const string BtnBackToParent = "BtnBackToParent";
            public const string BackToParentTooltip = "BackToParentTooltip";
            public const string MsgUpdatingPriority = "MsgUpdatingPriority";
            public const string MsgPriorityApplied = "MsgPriorityApplied";
            public const string StatusDropdownTooltip = "StatusDropdownTooltip";
            public const string StatusLoading = "StatusLoading";
            public const string FieldTransition = "FieldTransition";
            public const string ResolveUpdateTitle = "ResolveUpdateTitle";
            public const string FieldComment = "FieldComment";
            public const string FieldMention = "FieldMention";
            public const string MentionSearchPlaceholder = "MentionSearchPlaceholder";
            public const string AttachFixHint = "AttachFixHint";
            public const string BtnComment = "BtnComment";
            public const string BtnApplyTransition = "BtnApplyTransition";
            public const string BtnResolveMarked = "BtnResolveMarked";
            public const string BtnUpdateActivity = "BtnUpdateActivity";
            public const string MsgCommentRequired = "MsgCommentRequired";
            public const string MsgActivityRequired = "MsgActivityRequired";
            public const string MsgTransitionRequired = "MsgTransitionRequired";
            public const string MsgResolving = "MsgResolving";
            public const string MsgCommented = "MsgCommented";
            public const string MsgActivityUpdated = "MsgActivityUpdated";
            public const string MsgUpdatingStatus = "MsgUpdatingStatus";
            public const string MsgTransitionApplied = "MsgTransitionApplied";
            public const string MsgAttachmentSent = "MsgAttachmentSent";
            public const string MsgAttachSendFailed = "MsgAttachSendFailed";
            public const string MsgResolveFailed = "MsgResolveFailed";
            public const string MsgNoTransitions = "MsgNoTransitions";
            public const string SettingsTitle = "SettingsTitle";
            public const string SettingsLanguage = "SettingsLanguage";
            public const string LangPortuguese = "LangPortuguese";
            public const string LangEnglish = "LangEnglish";
            public const string SettingsDataTitle = "SettingsDataTitle";
            public const string SettingsDataNote = "SettingsDataNote";
            public const string SettingsClearData = "SettingsClearData";
            public const string MsgDataCleared = "MsgDataCleared";

            public const string GitSettingsTitle = "GitSettingsTitle";
            public const string GitSettingsNote = "GitSettingsNote";
            public const string GitEnableToggle = "GitEnableToggle";
            public const string GitRepoPathLabel = "GitRepoPathLabel";
            public const string BtnDetectRepo = "BtnDetectRepo";
            public const string GitBaseBranchLabel = "GitBaseBranchLabel";
            public const string GitBranchTemplateLabel = "GitBranchTemplateLabel";
            public const string GitCommitTemplateLabel = "GitCommitTemplateLabel";
            public const string GitTemplateHint = "GitTemplateHint";
            public const string GitNativeLinkNote = "GitNativeLinkNote";
            public const string BtnInstallGithubJira = "BtnInstallGithubJira";
            public const string GitCardTitle = "GitCardTitle";
            public const string GitTypeLabel = "GitTypeLabel";
            public const string GitBranchPreview = "GitBranchPreview";
            public const string GitCommitPreview = "GitCommitPreview";
            public const string GitCurrentBranch = "GitCurrentBranch";
            public const string BtnGitCreateBranch = "BtnGitCreateBranch";
            public const string BtnGitCopyCommit = "BtnGitCopyCommit";
            public const string BtnGitCopyBranch = "BtnGitCopyBranch";
            public const string MsgGitRepoDetected = "MsgGitRepoDetected";
            public const string MsgGitRepoNotFound = "MsgGitRepoNotFound";
            public const string MsgGitNotInstalled = "MsgGitNotInstalled";
            public const string MsgGitWorking = "MsgGitWorking";
            public const string MsgGitBranchReady = "MsgGitBranchReady";
            public const string MsgGitBranchFailed = "MsgGitBranchFailed";
            public const string MsgGitCopiedCommit = "MsgGitCopiedCommit";
            public const string MsgGitCopiedBranch = "MsgGitCopiedBranch";

            // --- Local agent ---

            public const string TabAgent = "TabAgent";
            public const string AgentSectionTitle = "AgentSectionTitle";
            public const string AgentIntro = "AgentIntro";
            public const string AgentTaskLabel = "AgentTaskLabel";
            public const string AgentTaskPlaceholder = "AgentTaskPlaceholder";
            public const string AgentIssueLabel = "AgentIssueLabel";
            public const string AgentNoIssue = "AgentNoIssue";
            public const string BtnAgentRun = "BtnAgentRun";
            public const string BtnAgentRunning = "BtnAgentRunning";
            public const string BtnAgentTerminal = "BtnAgentTerminal";
            public const string BtnAgentCancel = "BtnAgentCancel";
            public const string BtnAgentRefresh = "BtnAgentRefresh";
            public const string BtnAgentDelete = "BtnAgentDelete";
            public const string BtnAgentCopyResult = "BtnAgentCopyResult";
            public const string BtnAgentOpenFolder = "BtnAgentOpenFolder";
            public const string BtnAgentSendToAgent = "BtnAgentSendToAgent";
            public const string AgentPermissionLabel = "AgentPermissionLabel";
            public const string AgentPermissionPlan = "AgentPermissionPlan";
            public const string AgentPermissionDefault = "AgentPermissionDefault";
            public const string AgentPermissionAcceptEdits = "AgentPermissionAcceptEdits";
            public const string AgentPermissionNote = "AgentPermissionNote";
            public const string AgentCliTitle = "AgentCliTitle";
            public const string AgentCliFound = "AgentCliFound";
            public const string AgentCliMissing = "AgentCliMissing";
            public const string AgentCliOverrideMissing = "AgentCliOverrideMissing";
            public const string AgentCliChecking = "AgentCliChecking";
            public const string AgentCliPathLabel = "AgentCliPathLabel";
            public const string AgentCliPathHint = "AgentCliPathHint";
            public const string BtnAgentCheckCli = "BtnAgentCheckCli";
            public const string BtnAgentInstallCli = "BtnAgentInstallCli";
            public const string BtnAgentCopyInstall = "BtnAgentCopyInstall";
            public const string MsgAgentInstallCopied = "MsgAgentInstallCopied";
            public const string AgentSkillTitle = "AgentSkillTitle";
            public const string AgentSkillNote = "AgentSkillNote";
            public const string BtnAgentInstallSkill = "BtnAgentInstallSkill";
            public const string BtnAgentPreviewSkill = "BtnAgentPreviewSkill";
            public const string MsgAgentSkillInstalled = "MsgAgentSkillInstalled";
            public const string MsgAgentSkillFailed = "MsgAgentSkillFailed";
            public const string MsgAgentSkillPresent = "MsgAgentSkillPresent";
            public const string MsgAgentSkillAbsent = "MsgAgentSkillAbsent";
            public const string AgentRunsTitle = "AgentRunsTitle";
            public const string AgentNoRuns = "AgentNoRuns";
            public const string AgentTranscriptTitle = "AgentTranscriptTitle";
            public const string AgentResultTitle = "AgentResultTitle";
            public const string AgentWaitingFirstEvent = "AgentWaitingFirstEvent";
            public const string AgentStatusRunning = "AgentStatusRunning";
            public const string AgentStatusSucceeded = "AgentStatusSucceeded";
            public const string AgentStatusFailed = "AgentStatusFailed";
            public const string AgentStatusCanceled = "AgentStatusCanceled";
            public const string AgentStatusOrphaned = "AgentStatusOrphaned";
            public const string AgentEventThinking = "AgentEventThinking";
            public const string AgentEventTool = "AgentEventTool";
            public const string AgentEventStarted = "AgentEventStarted";
            public const string AgentEventError = "AgentEventError";
            public const string AgentMetaCost = "AgentMetaCost";
            public const string AgentMetaDuration = "AgentMetaDuration";
            public const string MsgAgentNoTask = "MsgAgentNoTask";
            public const string MsgAgentStartFailed = "MsgAgentStartFailed";
            public const string MsgAgentTerminalFailed = "MsgAgentTerminalFailed";
            public const string MsgAgentCopied = "MsgAgentCopied";
            public const string MsgAgentWorkingDir = "MsgAgentWorkingDir";
            public const string AgentModelLabel = "AgentModelLabel";
            public const string AgentModelCliDefault = "AgentModelCliDefault";
            public const string AgentModelNote = "AgentModelNote";
            public const string BtnAgentContinue = "BtnAgentContinue";
            public const string AgentContinueNote = "AgentContinueNote";
            public const string AgentContinueUnavailable = "AgentContinueUnavailable";
            public const string MsgAgentNoFollowUp = "MsgAgentNoFollowUp";
            public const string AgentResumedFrom = "AgentResumedFrom";
            public const string BtnAgentDiagnostics = "BtnAgentDiagnostics";
            public const string MsgAgentDiagnosticsCopied = "MsgAgentDiagnosticsCopied";
            public const string AgentProviderLabel = "AgentProviderLabel";
            public const string AgentNoApiKeyNote = "AgentNoApiKeyNote";

            public const string AgentCliReady = "AgentCliReady";
            public const string AgentCliMissingShort = "AgentCliMissingShort";
            public const string AgentCliMissingHint = "AgentCliMissingHint";
            public const string BtnAgentNewChat = "BtnAgentNewChat";
            public const string BtnAgentHistory = "BtnAgentHistory";
            public const string BtnAgentConfigure = "BtnAgentConfigure";
            public const string BtnAgentSend = "BtnAgentSend";
            public const string BtnAgentDeleteChat = "BtnAgentDeleteChat";
            public const string BtnAgentUnlinkIssue = "BtnAgentUnlinkIssue";
            public const string AgentChatEmpty = "AgentChatEmpty";
            public const string AgentComposerHint = "AgentComposerHint";
            public const string AgentYou = "AgentYou";
            public const string AgentActivitySteps = "AgentActivitySteps";
            public const string AgentMetaTokens = "AgentMetaTokens";
            public const string MsgAgentBusy = "MsgAgentBusy";
            public const string AgentUsageIdle = "AgentUsageIdle";
            public const string AgentUsageNoBudget = "AgentUsageNoBudget";
            public const string AgentUsageSummary = "AgentUsageSummary";
            public const string AgentUsageReset = "AgentUsageReset";
            public const string AgentSettingsTitle = "AgentSettingsTitle";
            public const string AgentSkillPathLabel = "AgentSkillPathLabel";
            public const string BtnAgentOpenSkill = "BtnAgentOpenSkill";
            public const string AgentEnvTitle = "AgentEnvTitle";
            public const string AgentEnvNote = "AgentEnvNote";
            public const string AgentEnvEnabledLabel = "AgentEnvEnabledLabel";
            public const string AgentEnvPathLabel = "AgentEnvPathLabel";
            public const string AgentEnvPathHint = "AgentEnvPathHint";
            public const string AgentEnvSecretsNote = "AgentEnvSecretsNote";
            public const string BtnAgentEnvSave = "BtnAgentEnvSave";
            public const string BtnAgentEnvReload = "BtnAgentEnvReload";
            public const string BtnAgentEnvTemplate = "BtnAgentEnvTemplate";
            public const string BtnAgentEnvReveal = "BtnAgentEnvReveal";
            public const string MsgAgentEnvPath = "MsgAgentEnvPath";
            public const string MsgAgentEnvPathAbsent = "MsgAgentEnvPathAbsent";
            public const string MsgAgentEnvVars = "MsgAgentEnvVars";
            public const string MsgAgentEnvDisabled = "MsgAgentEnvDisabled";
            public const string MsgAgentEnvSaved = "MsgAgentEnvSaved";
            public const string MsgAgentEnvFailed = "MsgAgentEnvFailed";
            public const string MsgAgentEnvMissing = "MsgAgentEnvMissing";
            public const string MsgAgentEnvTemplate = "MsgAgentEnvTemplate";
            public const string AgentBudgetTitle = "AgentBudgetTitle";
            public const string AgentBudgetNote = "AgentBudgetNote";
            public const string AgentBudgetLabel = "AgentBudgetLabel";
            public const string AgentBudgetHint = "AgentBudgetHint";
            public const string AgentWindowHoursLabel = "AgentWindowHoursLabel";
            public const string AgentWindowHoursHint = "AgentWindowHoursHint";
            public const string BtnAgentClearUsage = "BtnAgentClearUsage";
            public const string MsgAgentUsageCleared = "MsgAgentUsageCleared";
            public const string MsgAgentUsageIdle = "MsgAgentUsageIdle";
            public const string MsgAgentUsageWindow = "MsgAgentUsageWindow";
            public const string AgentUsageEstimateNote = "AgentUsageEstimateNote";
            public const string BtnAgentEnvCreate = "BtnAgentEnvCreate";
            public const string BtnAgentEnvFill = "BtnAgentEnvFill";
            public const string MsgAgentEnvFilled = "MsgAgentEnvFilled";
            public const string MsgAgentEnvNoConnection = "MsgAgentEnvNoConnection";
            public const string MsgAgentEnvJiraOk = "MsgAgentEnvJiraOk";
            public const string MsgAgentEnvJiraMissing = "MsgAgentEnvJiraMissing";
            public const string BtnAgentEnvTest = "BtnAgentEnvTest";
            public const string MsgAgentEnvTesting = "MsgAgentEnvTesting";
            public const string MsgAgentEnvTestOk = "MsgAgentEnvTestOk";
            public const string MsgAgentEnvTestFailed = "MsgAgentEnvTestFailed";
            public const string MsgAgentEnvApiKeyWarning = "MsgAgentEnvApiKeyWarning";
            public const string MsgAgentEnvEmailMismatch = "MsgAgentEnvEmailMismatch";
            public const string AgentPlanOnlyLabel = "AgentPlanOnlyLabel";
            public const string AgentPlanOnlyHint = "AgentPlanOnlyHint";
            public const string AgentCostMeaningNote = "AgentCostMeaningNote";
        }
    }
}
