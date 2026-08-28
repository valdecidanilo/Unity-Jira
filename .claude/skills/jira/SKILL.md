---
name: jira
description: Automação completa do Jira (criar, mover, comentar, atribuir, buscar) integrada ao git. Use SEMPRE que o usuário falar em nova atividade/task/tarefa/bug/card, pedir para criar branch, começar/iniciar/pegar uma atividade, mover card de status (em andamento, review, concluído), comentar numa issue, listar suas issues, perguntar "o que foi feito", pedir resumo do trabalho/commits, ou pedir para atualizar/sincronizar o Jira com o que já foi commitado. Também para /jira (start, sync, create, view, move, mine, done).
allowed-tools: Bash, Read, Grep, Glob
---

# Jira

Automação do Jira via REST API v3 + git.

**Helper:** o `jira.sh` fica ao lado deste `SKILL.md`. Se a skill veio no repo, chame `bash .claude/skills/jira/jira.sh <cmd>` (a partir da raiz do repo); se for a skill global da máquina, `bash ~/.claude/skills/jira/jira.sh <cmd>`. Nos exemplos abaixo, `JIRA` = esse caminho.

## Postura: aja, não pergunte

O usuário já autorizou esta skill a operar no Jira dele. **Execute as escritas direto** (create, comment, move, assign, edit, desc) e reporte o resultado — não peça confirmação a cada passo. Peça confirmação apenas em três casos:

- não dá para descobrir a **project key** (veja abaixo);
- o pedido é ambíguo entre **dois cards existentes** diferentes;
- mover para **Concluído/Done** sem evidência de que o trabalho terminou (código commitado, testes passando).

Nunca peça o token no chat e nunca ecoe o conteúdo de nenhum `jira.env`.

## Setup

O helper procura a config nesta ordem: `$JIRA_ENV` → `<raiz-do-repo>/.claude/jira.env` → `~/.claude/jira.env`.

Se nenhuma existir, pare e oriente o usuário (sem pedir o token no chat):

```
cp .claude/jira.env.example .claude/jira.env
```

e preencher:

```
JIRA_URL=https://SEU-SITE.atlassian.net
JIRA_EMAIL=email@dominio.com
JIRA_API_TOKEN=token-gerado-em-id.atlassian.com/manage-profile/security/api-tokens
JIRA_DEFAULT_PROJECT=OPA
```

`.claude/jira.env` está no `.gitignore` — **nunca** commite esse arquivo nem ecoe o conteúdo dele.
Valide com `bash .claude/skills/jira/jira.sh whoami`.

## Descobrindo a project key (nesta ordem, sem perguntar)

1. Branch atual: `git rev-parse --abbrev-ref HEAD` → padrão `[A-Z][A-Z0-9]+-\d+`.
2. Histórico: `git log --oneline -80 | grep -oE '\b[A-Z][A-Z0-9]{1,9}-[0-9]+\b'` → prefixo mais frequente.
3. `grep JIRA_DEFAULT_PROJECT .claude/jira.env ~/.claude/jira.env 2>/dev/null`.
4. Só então pergunte ao usuário — e, ao receber, **grave** no `jira.env` em uso como `JIRA_DEFAULT_PROJECT` para não perguntar de novo.

## Gatilho 1 — nova atividade / criar branch

Dispare quando o usuário disser algo como: "cria uma branch pra X", "nova task", "vou fazer X", "começa a atividade Y", "abre um bug de Z", "/jira start".

Faça tudo de uma vez, sem perguntar:

1. Resolva a project key (acima). Se não souber o tipo, rode `types <PROJ>` — os nomes variam ("Task"/"Tarefa"/"Bug"/"História").
2. Monte um título curto e imperativo em PT-BR a partir do pedido. Descrição: contexto + critério de aceite, se der para inferir do pedido/código.
3. `$JIRA start <PROJ> <TIPO> "<TITULO>" "<DESC>"`
   → cria a issue, **atribui ao usuário**, move para **Em andamento** e devolve `{key, url, branch}`.
4. Crie o branch a partir da base correta:
   ```
   git fetch origin
   git checkout -b <branch> origin/dev     # ou origin/main se não existir dev
   ```
   Use o `branch` devolvido pelo `start`. Se o usuário sugeriu um nome, respeite o dele mas garanta a KEY no nome.
5. Reporte em poucas linhas: `KEY – título`, status, branch criado, link `/browse/KEY`.

Se o card **já existe** e o usuário só quer trabalhar nele: `progress <KEY>` + `branch <KEY>` + `git checkout -b`.

## Gatilho 2 — "o que foi feito" / atualizar o Jira / sync

Dispare em: "o que já foi feito", "atualiza o Jira", "sincroniza", "resume os commits", "manda pro card o que subiu", "/jira sync".

1. Descubra a base remota: `git symbolic-ref refs/remotes/origin/HEAD`, senão tente `origin/dev`, senão `origin/main`.
2. Levante o trabalho:
   ```
   git log <base>..HEAD --oneline
   git diff <base>...HEAD --stat
   git status --short
   ```
3. Descubra a(s) KEY(s): nome do branch, senão as keys citadas nos commits. Havendo várias, agrupe os commits por key e trate cada card separadamente.
4. `view <KEY>` para ver o status atual e os comentários já postados (não repita um comentário igual ao último).
5. **Escreva o resumo para o dev no chat** (commits, arquivos tocados, o que ainda falta) **e** poste como comentário no card:
   ```
   $JIRA comment <KEY> "Progresso (branch <branch>):
   - <commit 1>
   - <commit 2>
   Arquivos: <n> alterados (<principais>).
   Pendente: <o que falta ou 'nada'>."
   ```
6. Ajuste o status pelo estado real, direto:
   - há commits e o card está em To Do → `progress <KEY>`
   - branch já pushado / PR aberto → `review <KEY>`
   - trabalho concluído e verificado → `done <KEY>` (**este confirme antes**)
7. Termine com o link `/browse/KEY`.

Se houver commits locais **não pushados**, diga isso explicitamente no resumo do chat — não afirme no card que algo subiu se não subiu.

## Gatilho 3 — status / consulta

"quais minhas tarefas", "o que tá em aberto" → `mine`. Pergunta sobre um card → `view <KEY>`. Busca ampla → `search "<JQL>"`.

Sempre resuma em tabela/texto legível. **Nunca despeje o JSON cru** no chat.

## Comandos do helper

```
whoami | projects | types <PROJ> | mine | url <KEY>
search "<JQL>" [max] | view <KEY> | transitions <KEY>
create <PROJ> <TIPO> <TITULO> [DESC]
start  <PROJ> <TIPO> <TITULO> [DESC]   # cria + assume + em andamento + branch sugerido
branch <KEY>                           # nome de branch a partir do card
progress <KEY> | review <KEY> | done <KEY> | move <KEY> <STATUS>
comment <KEY> <TEXTO> | edit <KEY> <TITULO> | desc <KEY> <TEXTO> | assign <KEY> me
```

## Detalhes

- `progress`/`review`/`done` já tentam vários nomes de status (PT e EN). Se falharem, o erro lista as transições reais — escolha a certa e use `move`.
- Comentários e descrições: texto puro, o helper converte para ADF. Quebras de linha viram parágrafos.
- Aspas: passe títulos e textos entre aspas duplas, escapando as internas.
- Sempre inclua a KEY no nome do branch e na mensagem de commit (`KEY-123: mensagem`) — é o que faz o sync funcionar depois.
