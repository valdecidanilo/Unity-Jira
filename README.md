# Jira Communication for Unity

Base de integração profissional entre o Unity Editor e o Jira Cloud.

## Recursos incluídos

- Menu superior `Jira` com logo oficial na janela e no ícone da aba.
- Janela `Jira > Open Jira Workspace` construída com UI Toolkit, dividida em duas abas: **Conexão** e **Criar Issue**.
- Campos para URL do Jira, e-mail Atlassian e API Token.
- Teste real de conexão usando `GET /rest/api/3/myself`.
- Aba **Criar Issue** com criação de história, tarefa, bug e subtask, em seções organizadas:
  - Seleção de projeto e tipo de issue carregados dinamicamente dos metadados (`createmeta`).
  - Título, descrição (convertida para Atlassian Document Format) e issue pai para subtasks.
  - Listagem de **épicos** ativos para vínculo (projetos team-managed), com **porcentagem de conclusão** e barra de progresso ao selecionar.
  - Listagem de **sprints ativas**; a issue é movida para a sprint escolhida após ser criada.
  - **Prioridade**, **Responsável** (com busca inline e "Atribuir a mim"), **Time/Equipe** e **datas** descobertos por projeto/tipo — só aparecem se o projeto os expõe.
  - **Anexo** de arquivo/print enviado após a criação.
  - **Presets** salvos no Editor (projeto, tipo, prioridade, responsável, time) para não reselecionar tudo a cada criação.
  - Botão para abrir a issue recém-criada direto no Jira.
- Troca automática para a aba de criação quando a conexão é validada.
- Aba **Resolver**: liste suas issues em aberto e as reabertas, **fixe** as importantes, aplique as **transições** do workflow da empresa, **comente**, **anexe** o print/arquivo do fix e **mencione pessoas** (@) — tudo de dentro do Unity.
- **Assistente de IA** na aba Criar: descreva a atividade em poucas palavras e a IA preenche título, descrição e prioridade. Suporta **Claude (Anthropic)** e **ChatGPT (OpenAI)** — cada usuário usa sua própria API Key (mantida só na sessão do Unity), com escolha de provedor e modelo nas Configurações.
- Aba **Configurações**: idioma (Português / Inglês), API Key/modelo de IA e limpeza dos dados de conexão salvos.
- Mensagens amigáveis para erros HTTP comuns.
- URL e e-mail salvos apenas nas preferências locais do Editor.
- API Token mantido somente na sessão atual do Unity por padrão.
- Separação entre autenticação, cliente REST, modelos, preferências e UI.

## Instalação

### Package Manager por arquivo local

1. Extraia este ZIP em uma pasta permanente.
2. No Unity, abra `Window > Package Manager`.
3. Clique em `+` e escolha `Add package from disk...`.
4. Selecione o arquivo `package.json`.
5. Abra `Jira > Open Jira Workspace`.

### Dentro da pasta Packages

Copie a pasta `com.oxentegames.jira-communication` diretamente para a pasta `Packages` do projeto.

## Configuração

Informe:

- URL: `https://suaempresa.atlassian.net`
- E-mail: o e-mail da conta Atlassian
- API Token: token pessoal criado na Atlassian

Depois clique em **Conectar**.

## Segurança

O token não é salvo em Assets, ProjectSettings, package files ou Git. Nesta versão ele fica em `SessionState` e é descartado quando o Unity Editor é encerrado.

Para uma distribuição corporativa definitiva, considere substituir `JiraBasicTokenAuthProvider` por OAuth 2.0 ou por armazenamento no cofre de credenciais do sistema operacional.

## Compatibilidade

- Unity 2021.3 ou superior.
- Jira Cloud REST API v3.

## Próxima etapa sugerida

Adicionar um `JiraIssueService` para consultar metadados de criação e montar dinamicamente os campos aceitos por cada projeto e tipo de issue.
