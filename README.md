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

1. Em `Configurações → Agente local`, escolha o agente no dropdown **Agente**
   (Claude Code ou Codex). Essa escolha é **independente** do Assistente de IA:
   aquele usa API Key e é cobrado por token, este usa a CLI com a conta em que
   você já está logado e consome o seu plano.

   **Se você usa o app desktop do Claude, provavelmente não precisa instalar nada.**
   O app já traz um `claude` completo em
   `%APPDATA%\Claude\claude-code\<versão>\claude.exe` (macOS:
   `~/Library/Application Support/Claude/claude-code/<versão>/claude`) — só não o
   coloca no PATH. O package procura ali e usa a versão mais nova.

   Se preferir a CLI no PATH, ou usar Codex:
   - Claude Code: `npm install -g @anthropic-ai/claude-code`
   - Codex: `npm install -g @openai/codex`

   A ordem de busca é: caminho informado manualmente → PATH → instalações
   conhecidas (npm etc.) → bundle do app desktop. Uma CLI que **você** instalou
   sempre ganha da cópia gerenciada pelo app, cuja pasta muda a cada atualização.
2. Ainda em Configurações, o status logo abaixo do dropdown mostra qual binário
   foi encontrado e a versão; se nada aparecer, informe o caminho manualmente.
3. Clique em **Gerar / atualizar** em *Instruções do projeto*. Isso escreve a
   convenção de branch/commit configurada e os cuidados de Unity em:
   - Claude: `.claude/skills/jira-unity/SKILL.md`
   - Codex: bloco delimitado em `AGENTS.md` (o resto do arquivo é preservado)

   O card mostra o caminho completo do arquivo e tem **Abrir no explorador**.
4. Abra `Jira → Jira Workspace → Agente`, escreva a mensagem, escolha as
   **permissões** e envie (**Enter** envia, **Shift+Enter** quebra linha).

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

### A aba é um chat

A aba Agente é uma conversa: você escreve, o agente responde, e a **próxima
mensagem continua a mesma sessão da CLI** — não há botão de "continuar". Isso é o
que segura o custo: uma sessão retomada já tem o enquadramento do projeto e tudo
que o agente leu, então o prompt enviado é só a instrução seguinte. Começar de
novo pagaria esse contexto inteiro outra vez.

Cada turno ainda é um processo próprio — é isso que sobrevive a uma recompilação —
e os turnos aparecem como uma conversa só porque compartilham o mesmo `threadId`.
Quando a sessão **não** pode ser retomada (o turno anterior falhou antes de
reportar um id, ou a CLI não tem retomada — `codex exec` não tem), a mensagem
inicia um turno novo dentro da mesma conversa, em vez de virar um beco sem saída.

**Nova conversa** começa um assunto do zero. **Histórico** lista as conversas, com
recarregar, copiar resultado, abrir a pasta da execução e excluir a conversa.

Os passos da CLI ficam recolhidos em uma linha por turno (`▸ 14 passos da CLI ·
Read Player.cs`), que abre no clique e continua aberta. São detalhe de
diagnóstico: expandidos, empurravam a resposta para fora da tela.

O dropdown **Modelo**, em Configurações, permite fixar um modelo mais barato para
tarefas mecânicas. O padrão é **“Padrão da CLI”**, que não envia `--model` — o
package não sobrescreve a sua configuração sem você pedir. Uma conversa retomada
mantém o modelo com que começou; trocar no meio descartaria o contexto que se
quis reaproveitar.

### Tokens: consumo, porcentagem e reset

A barra da aba Agente mostra os tokens consumidos na janela atual, a porcentagem
que resta e o horário do reset, com a contagem para ele.

Os números vêm do que a **própria CLI reporta** ao fim de cada execução (entrada,
saída e cache), gravados em `Library/JiraAgent/usage.jsonl` — então sobrevivem ao
fechamento do Unity. A janela funciona como nos planos Claude: abre na primeira
execução depois de um intervalo sem uso e dura N horas (5 por padrão).

**Nenhuma CLI informa a cota real da sua conta.** Por isso a porcentagem é medida
contra um **limite de tokens que você define** em `Configurações → Consumo de
tokens`, junto com a duração da janela. Com limite zero a aba mostra só os números
brutos, em vez de inventar um denominador. É uma estimativa do uso desta máquina
neste projeto, não uma leitura da sua conta.

### Variáveis do agente (`.env`)

Ao importar o package, um arquivo **`.env` é criado na raiz do projeto** com as
chaves já escritas e vazias:

```
JIRA_URL=
JIRA_EMAIL=
JIRA_API_TOKEN=
```

São elas que permitem ao agente **consultar o Jira sozinho durante o chat** (ler a
issue, comentários, issues ligadas) em vez de depender do que veio no prompt. Em
`Configurações → Variáveis do agente` você lê, edita e salva o arquivo; o botão
**Preencher com a conexão** copia URL, e-mail e token da aba Conexão, para você não
digitar o token duas vezes. Nada é gravado sem você clicar em **Salvar .env**.

O arquivo também aceita opções da CLI (`ANTHROPIC_MODEL`, `MAX_THINKING_TOKENS`,
`BASH_DEFAULT_TIMEOUT_MS`, …). Uma variável por linha, `CHAVE=valor`, sem
interpolação — o valor vai literal. Variável vazia não é exportada, para o agente
não achar que tem conexão quando não tem.

A exportação acontece no próprio script lançador, não no lado do Editor: a
execução é um processo destacado, e um environment montado aqui não chegaria até
ele.

> **Este arquivo guarda o seu token do Jira.** O package o adiciona ao
> `.gitignore` do projeto ao criá-lo e repete o aviso no cabeçalho do arquivo, mas
> a responsabilidade de não commitar nem compartilhar continua sendo sua. O caminho
> é configurável se você preferir guardá-lo fora do repositório.

As instruções geradas para o agente descrevem esse acesso: **ler** a API do Jira é
livre; **escrever** (transição, comentário, edição) só quando a tarefa pedir, já
que a janela do Unity é o caminho normal para isso; e o token nunca deve aparecer
em resposta, arquivo ou commit.

### O agente chamando o Jira, na prática

Uma execução headless **não tem quem responda a um pedido de permissão** — o que
não estiver liberado de antemão é negado no meio da tarefa. Por isso, quando o
`.env` tem as três chaves preenchidas, o run recebe `--allowedTools "Bash(curl *)"`:
uma permissão só, e apenas nesse caso. Dá para desligar em Configurações.

As **instruções do projeto** são regeradas sozinhas quando estão desatualizadas.
Versões antigas diziam ao agente que ele não tinha credencial do Jira e não devia
tentar a API; com o `.env` preenchido isso está errado, e um arquivo velho fazia o
agente recusar a consulta mesmo com tudo configurado.

Se ainda assim falhar, o botão **Testar conexão** no card do `.env` chama
`/rest/api/3/myself` com aquelas credenciais, **pelo mesmo shell que a execução
usa**, e mostra o nome autenticado ou a resposta literal do Jira — 401 de token
errado, HTML de URL errada, ou o erro do próprio `curl`.

### Cobrança: o agente usa o seu plano

O número em `≈ US$ …` que aparece ao fim de cada turno é o que a CLI reporta como
**equivalente daqueles tokens no preço da API**. É referência, não fatura: logado
no seu plano, a execução consome a cota e não gera cobrança extra.

O que geraria é uma credencial de API no ambiente — `ANTHROPIC_API_KEY`,
`ANTHROPIC_BASE_URL`, Bedrock/Vertex. Uma delas exportada no perfil da máquina
meses atrás é suficiente para todo run virar chamada cobrada, sem nenhum sinal na
janela. Por isso a opção **Usar somente o plano** vem ligada: o script lançador
limpa essas variáveis antes de chamar a CLI. Desligue só se quiser cobrar uma
conta de API de propósito.

A exceção é declarar `ANTHROPIC_API_KEY` dentro do próprio `.env` — aí é
intencional, a chave é mantida, e o card avisa na tela.

### Dois caminhos de cobrança, um por recurso

O package tem **duas integrações de IA que não se misturam**, e cada uma tem o seu
próprio seletor de provedor:

| Recurso | Onde escolhe | Credencial | Cobrança |
| --- | --- | --- | --- |
| Assistente de IA (preenche título/descrição) | `Configurações → Assistente de IA` | API Key sua | por token |
| Agente local (trabalha no repositório) | `Configurações → Agente local` | nenhuma; a CLI usa o seu login | o seu plano |

Escolher ChatGPT no Assistente **não** faz o agente procurar o Codex — são
configurações separadas, de propósito.

### Credenciais

A CLI do agente **não usa credencial nossa**: ela entra com a conta em que o
desenvolvedor já está logado. O token do Jira só existe em dois lugares, os dois
sob controle do desenvolvedor: o `EditorPrefs` desta máquina (aba Conexão) e o
`.env` do projeto, se você optar por preenchê-lo para que o agente consulte o Jira.
Transições e comentários continuam sendo feitos nesta janela por padrão.

## Segurança

O token não é salvo em Assets, ProjectSettings, package files ou Git. Ele é persistido localmente em `EditorPrefs` (registro do Windows / plist no macOS), de forma ofuscada, para que não seja necessário reinformá-lo a cada vez que o Unity Editor é aberto. A ofuscação evita que o valor fique em texto puro, mas **não** é criptografia forte.

Para uma distribuição corporativa definitiva, considere substituir `JiraBasicTokenAuthProvider` por OAuth 2.0 ou por armazenamento no cofre de credenciais do sistema operacional.

## Compatibilidade

- Unity 2021.3 ou superior.
- Jira Cloud REST API v3.

## Próxima etapa sugerida

Adicionar um `JiraIssueService` para consultar metadados de criação e montar dinamicamente os campos aceitos por cada projeto e tipo de issue.
