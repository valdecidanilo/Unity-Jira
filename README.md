# Jira Communication for Unity

Base de integração profissional entre o Unity Editor e o Jira Cloud.

## Recursos incluídos

- Menu superior `Jira`.
- Janela `Jira > Open Jira Workspace` construída com UI Toolkit.
- Campos para URL do Jira, e-mail Atlassian e API Token.
- Teste real de conexão usando `GET /rest/api/3/myself`.
- Mensagens amigáveis para erros HTTP comuns.
- URL e e-mail salvos apenas nas preferências locais do Editor.
- API Token mantido somente na sessão atual do Unity por padrão.
- Separação entre autenticação, cliente REST, modelos, preferências e UI.
- Estrutura preparada para histórias, tarefas, bugs, subtasks e templates.

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

Depois clique em **Testar e conectar**.

## Segurança

O token não é salvo em Assets, ProjectSettings, package files ou Git. Nesta versão ele fica em `SessionState` e é descartado quando o Unity Editor é encerrado.

Para uma distribuição corporativa definitiva, considere substituir `JiraBasicTokenAuthProvider` por OAuth 2.0 ou por armazenamento no cofre de credenciais do sistema operacional.

## Compatibilidade

- Unity 2021.3 ou superior.
- Jira Cloud REST API v3.

## Próxima etapa sugerida

Adicionar um `JiraIssueService` para consultar metadados de criação e montar dinamicamente os campos aceitos por cada projeto e tipo de issue.
