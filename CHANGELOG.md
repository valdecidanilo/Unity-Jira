# Changelog

## 0.9.0 - 2026-08-25

- Nova aba **Agente**: descreva a tarefa e um agente local (**Claude Code** ou **Codex CLI**) trabalha no repositório do projeto, sem sair do Unity.
- Execução **headless em background**, com transcrição ao vivo (texto, ferramentas usadas, erros), resultado final, duração e custo reportados pela CLI.
- A execução **sobrevive a recompilação de scripts, entrar em Play Mode e fechar o Unity**: cada execução é um diretório em `Library/JiraAgent/<runId>` e o Editor apenas lê o stream; não há pipe para perder.
- **Histórico de execuções** com replay da transcrição, cancelamento (mata a árvore de processos), abrir a pasta da execução e copiar o resultado.
- **Doctor da CLI**: detecta `claude`/`codex` no PATH, nos caminhos de instalação conhecidos e **no bundle do app desktop do Claude** (`%APPDATA%\Claude\claude-code\<versão>`, versão mais nova primeiro) — quem só tem o app não precisa instalar nada. Override manual de caminho e instruções de instalação quando nada é encontrado.
- **Continuar execução**: retoma a sessão anterior (`--resume`) enviando só o próximo passo, em vez de pagar o contexto inicial de novo. Ativo quando a execução terminou e reportou um id de sessão; oculto para Codex, que não tem retomada em `codex exec`.
- **Seletor de modelo** por provedor, com **“Padrão da CLI”** como default — nenhum `--model` é enviado a menos que você escolha, então a configuração da CLI não é sobrescrita por acidente. Uma execução continuada mantém o modelo da sessão original.
- Botão **Enviar para o agente** no detalhe da atividade: leva chave, título, descrição e o nome do branch da convenção do time já preenchidos.
- **Gerador de instruções do projeto** (`.claude/skills/jira-unity/SKILL.md` para Claude; bloco delimitado em `AGENTS.md` para Codex), com a convenção de branch/commit configurada e os cuidados de `.meta`, prefabs e cenas.
- **Permissões explícitas** por execução: somente leitura (padrão), padrão da CLI, ou editar sem perguntar. `bypassPermissions` não é exposto.
- Modo **Abrir no terminal** para uma sessão interativa (não rastreada).
- **Seletor de agente próprio**, independente do provedor do Assistente de IA. Os dois recursos não compartilham nada:
  o assistente é HTTP com API Key cobrada por token, o agente é CLI local no plano do desenvolvedor. Antes o provedor do
  agente era derivado do provedor do assistente, então quem tinha ChatGPT selecionado via a aba Agente procurar a CLI do
  Codex em vez da do Claude.
- Nenhuma credencial nova é armazenada: a CLI usa a conta em que o desenvolvedor já está logado, e a aba diz isso explicitamente.

## 0.8.0 - 2026-07-28

- Nova **integração Git/GitHub por convenção** (aba Configurações → *Integração Git / GitHub*).
- No detalhe de cada atividade (aba **Atividades**), preview ao vivo do **nome do branch** (`feat/PROJ-123-titulo`) e da **mensagem de commit** Conventional (`feat(PROJ-123): título`), com tipo escolhido pelo desenvolvedor.
- Botões para **criar/checkout do branch** localmente e **copiar** o commit ou o nome do branch (nada é enviado ao GitHub; token do GitHub não é necessário).
- Detecção automática da raiz do repositório (`git rev-parse`), com override manual, branch base e templates editáveis.
- Documentado o passo a passo para linkagem nativa via app **GitHub for Jira** + regras de Automation (PR aberto → Code Review, PR merjado → Concluído).

## 0.7.0 - 2026-07-28

- Status da aba **Atividades** sincronizados dinamicamente com o Jira Cloud.
- Removida a dependência de nomes de workflow específicos no código do package.
- Catálogo e cores baseados exclusivamente nos dados retornados pela API do Jira.
- Filtro de status compacto, pesquisável e agrupado pelas categorias oficiais do Jira.
- Pesquisa por chave ou título movida para a lista de atividades, com exemplo de uso.
- Menu simplificado para Workspace e links das documentações oficiais.
- Edição de atividade com prioridade, peso/Story Points e criação de subtarefas vinculadas.
- Busca de menções usando as mesmas pessoas atribuíveis retornadas pelo projeto.
- Criação rápida de subtarefas compatível com tarefas, histórias, bugs e tipos equivalentes do Jira.

## 0.6.0 - 2026-07-27

- Nova aba **Resolver**: lista suas issues em aberto e as reabertas.
- **Fixar** issues (pin) para mantê-las no topo da lista.
- **Transições** do workflow da empresa por issue, aplicadas de dentro do Unity.
- **Comentar** e **anexar** o print/arquivo do fix junto da resolução.
- **Mencionar pessoas** (@) no comentário, com busca de usuários.
- Campo de **responsável** melhorado: resultados clicáveis aparecem inline conforme você digita (sem precisar abrir o dropdown).

## 0.5.0 - 2026-07-27

- Suporte a **ChatGPT (OpenAI)** além do Claude, com seletor de provedor nas Configurações (token e modelo por provedor).
- Ao selecionar um **épico**, mostra a **porcentagem de conclusão** (itens concluídos / total) com barra de progresso.
- Fallback de progresso do épico para projetos team-managed (via campo `parent`).
- Rodapé com a marca "OxenteGames".

## 0.4.0 - 2026-07-27

- Assistente de IA (Claude) na criação de issues: gera título, descrição e prioridade a partir de uma breve descrição.
- Cada usuário informa sua própria API Key da Anthropic (mantida apenas na sessão do Unity).
- Seleção de modelo (Sonnet 5, Haiku 4.5, Opus 5) nas Configurações.
- Integração via HTTP com a Messages API da Anthropic (sem dependências externas).

## 0.3.0 - 2026-07-27

- Formulário de criação dividido em seções (Destino, Classificação, Datas, Detalhes, Anexo) com layout de duas colunas.
- Descoberta dinâmica de campos por projeto/tipo via `createmeta` (só mostra o que o projeto realmente tem).
- Campo **Prioridade** (dropdown a partir dos valores permitidos, padrão Médio).
- Campo **Responsável** com lista de usuários atribuíveis e botão "Atribuir a mim".
- Campo **Time / Equipe** detectado automaticamente pelo nome.
- Campos **Data de início** e **Data limite** (quando o projeto os expõe).
- **Anexo**: seleção de arquivo/print enviado após a criação da issue.
- **Presets**: projeto, tipo, prioridade, responsável e time ficam salvos entre sessões.
- Botão para limpar presets nas Configurações.
- Nota explicando que o Status inicial é definido pelo workflow do projeto.

## 0.2.0 - 2026-07-27

- Logo oficial do Jira no cabeçalho e no ícone da janela.
- Nova aba **Criar Issue** com formulário profissional.
- Seleção dinâmica de projeto e tipo de issue (história, tarefa, bug, subtask) via metadados do Jira.
- Descrição enviada em Atlassian Document Format (ADF).
- Listagem de épicos ativos para vínculo (projetos team-managed).
- Listagem de sprints ativas com movimentação automática da issue após a criação.
- Campo de issue pai para subtasks.
- Troca automática para a aba **Criar Issue** ao conectar com sucesso.
- Aba **Configurações** com seleção de idioma (Português / Inglês) e limpeza dos dados de conexão.
- Fallback para o endpoint clássico de `createmeta` quando o novo não retorna tipos, com mensagem clara sobre permissão.
- Menus `Jira > Create Issue` e `Jira > Settings`.

## 0.1.0 - 2026-07-27

- Estrutura inicial do package UPM.
- Janela profissional com UI Toolkit.
- Autenticação por e-mail e API Token.
- Teste de conexão com Jira Cloud.
- Token armazenado somente durante a sessão do Unity.
