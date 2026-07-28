# Changelog

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
