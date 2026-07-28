# Changelog

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
