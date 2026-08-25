# Jira Communication for Unity

Base de integração profissional entre o Unity Editor e o Jira Cloud.

## Recursos incluídos

- Menu superior `Jira` com logo oficial na janela e no ícone da aba.
- Janela `Jira > Jira Workspace` construída com UI Toolkit e abas para conexão, criação, atividades e configurações.
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
- **Agente local** na aba Agente: um agente de código (**Claude Code** ou **Codex CLI**) trabalha no
  repositório do projeto a partir de uma tarefa ou de uma atividade do Jira, com transcrição ao vivo,
  resultado, histórico e cancelamento. A execução roda em background e sobrevive a recompilação e ao
  fechamento do Unity. Nenhum token novo é armazenado. Ver *Agente local* abaixo.
- **Integração Git/GitHub por convenção**: no detalhe de cada atividade, gera o **nome do branch** (`feat/PROJ-123-titulo`) e a **mensagem de commit** Conventional (`feat(PROJ-123): título`), cria/faz checkout do branch localmente e copia os textos — sem enviar nada ao GitHub e sem precisar de token do GitHub.
- Aba **Configurações**: idioma (Português / Inglês), API Key/modelo de IA, integração Git/GitHub e limpeza dos dados de conexão salvos.
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
5. Abra `Jira > Jira Workspace`.

### Dentro da pasta Packages

Copie a pasta `com.oxentegames.jira-communication` diretamente para a pasta `Packages` do projeto.

## Configuração

Informe:

- URL: `https://suaempresa.atlassian.net`
- E-mail: o e-mail da conta Atlassian
- API Token: token pessoal criado na Atlassian

Depois clique em **Conectar**.

## Status sincronizados por empresa

Os filtros da aba **Atividades** não possuem nomes de status fixos no código.
Ao conectar, o plugin consulta `GET /rest/api/3/status` e monta um dropdown
compacto e pesquisável com os status disponíveis no Jira da empresa. Assim, um
workflow sem `P/Teste`, por exemplo, não exibe essa opção.

As cores também usam as categorias oficiais retornadas pela API: cinza para
"A fazer", azul para "Em andamento" e verde para "Concluído". Essas mesmas
categorias organizam os status em grupos dentro do dropdown, sem depender dos
nomes escolhidos por cada empresa. O botão **Recarregar** da aba Atividades
sincroniza novamente o catálogo remoto.

A pesquisa local por chave ou título fica junto à lista de atividades e também
encontra subtarefas carregadas.

No menu `Jira` ficam disponíveis somente:

- `Jira Workspace`
- `Documentação oficial do Jira`
- `Documentação do GitHub`

## Integração Git/GitHub

A ferramenta padroniza a **convenção** de branch e commit; a ligação entre Jira e
GitHub é mantida de forma **nativa**, sem código nem segredos guardados.

### 1. Na ferramenta (convenção)

Em `Configurações → Integração Git / GitHub`, habilite a integração e ajuste
(se quiser) a pasta do repositório, o branch base e os templates:

- Branch: `{type}/{key}-{slug}` → `feat/PROJ-123-corrigir-login`
- Commit: `{type}({key}): {title}` → `feat(PROJ-123): corrigir login`
- Placeholders: `{type}` `{key}` `{slug}` `{title}`.

Ao selecionar uma atividade na aba **Atividades**, o tipo Conventional é sugerido
a partir do tipo da issue (Bug → `fix`, demais → `feat`) e pode ser trocado. Use
**Criar / checkout branch** para começar a trabalhar já no branch certo, ou os
botões de **copiar** para colar o commit/branch onde preferir.

### 2. Linkagem automática (app oficial)

Para o Jira exibir branches/commits/PRs no painel **Development** e mover a issue
automaticamente conforme o estado do PR:

1. Instale o app **[GitHub for Jira](https://github.com/marketplace/jira-software-github)**
   (gratuito) e conecte a organização/repositório.
2. Como todo branch/commit/PR carrega a chave (ex.: `PROJ-123`), a associação
   passa a ser automática — sem configuração extra por atividade.
3. (Opcional) Crie regras em **Jira → Automation**, por exemplo:
   - *Pull request criado* → transição para **Code Review**;
   - *Pull request merjado* → transição para **Concluído**.

Assim o desenvolvedor só escolhe o estado/semântica; a plataforma cuida do resto.

## Agente local (aba Agente)

Além do **Assistente de IA** (que preenche título/descrição via HTTP), o package
dirige um **agente de código local** — `claude` (Claude Code) ou `codex` (Codex
CLI) — que trabalha no repositório do projeto sem sair do Unity.

São coisas diferentes de propósito: o assistente é uma chamada HTTP sem estado
que devolve campos; o agente é um processo local que lê o projeto, edita arquivos
e roda comandos.

### Como usar

1. Instale a CLI do provedor escolhido em `Configurações → Assistente de IA`:
   - Claude Code: `npm install -g @anthropic-ai/claude-code`
   - Codex: `npm install -g @openai/codex`
2. Abra `Jira → Jira Workspace → Agente`. O card **CLI do agente** mostra se ela
   foi encontrada; se o Unity não herdar o PATH do seu shell, informe o caminho
   manualmente.
3. Clique em **Gerar / atualizar** em *Instruções do projeto*. Isso escreve a
   convenção de branch/commit configurada e os cuidados de Unity em:
   - Claude: `.claude/skills/jira-unity/SKILL.md`
   - Codex: bloco delimitado em `AGENTS.md` (o resto do arquivo é preservado)
4. Escreva a tarefa, escolha as **permissões** e clique em **Executar em background**.

Na aba **Atividades**, o botão **Enviar para o agente** leva chave, título,
descrição e o nome do branch da convenção já preenchidos.

### Permissões

| Modo | Efeito |
| --- | --- |
| Somente leitura | Investiga e propõe; nada em disco muda. É o padrão. |
| Padrão da CLI | A CLI pergunta antes de editar — e em background ninguém responde, então ela para. |
| Editar sem perguntar | Para execuções que devem alterar o projeto. |

`bypassPermissions` não é exposto: um agente headless com todas as travas
desligadas não deve ser alcançável por um clique.

### Por que a execução não se perde

Cada execução é um diretório em `Library/JiraAgent/<runId>` com o prompt, o
script lançador, o stream de eventos, o stderr e o código de saída. A CLI escreve
direto nesses arquivos e o Editor apenas **lê** — não há pipe entre os dois.

Por isso recompilar scripts, entrar em Play Mode ou fechar o Unity não interrompe
nem perde uma execução, e a transcrição fica disponível para replay depois.
`Library/` é por projeto e já ignorado pelo Git, então nada disso é commitado.

O botão **Abrir no terminal** existe para uma sessão interativa; ela
deliberadamente **não** entra no histórico, porque não há stream para acompanhar.

### Credenciais

Este caminho **não guarda nenhum token**. A CLI usa a conta em que o
desenvolvedor já está logado, e as instruções geradas dizem ao agente para não
tentar falar com a API do Jira — transições e comentários continuam sendo feitos
nesta janela, que já tem o token.

## Segurança

O token não é salvo em Assets, ProjectSettings, package files ou Git. Ele é persistido localmente em `EditorPrefs` (registro do Windows / plist no macOS), de forma ofuscada, para que não seja necessário reinformá-lo a cada vez que o Unity Editor é aberto. A ofuscação evita que o valor fique em texto puro, mas **não** é criptografia forte.

Para uma distribuição corporativa definitiva, considere substituir `JiraBasicTokenAuthProvider` por OAuth 2.0 ou por armazenamento no cofre de credenciais do sistema operacional.

## Compatibilidade

- Unity 2021.3 ou superior.
- Jira Cloud REST API v3.

## Próxima etapa sugerida

Adicionar um `JiraIssueService` para consultar metadados de criação e montar dinamicamente os campos aceitos por cada projeto e tipo de issue.
