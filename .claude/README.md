# Claude Code neste repositório

## Skill do Jira

Quem clonar o repo já recebe a skill pronta em `.claude/skills/jira/`. Só falta o token.

### Setup (uma vez por máquina)

```bash
cp .claude/jira.env.example .claude/jira.env
```

Edite `.claude/jira.env` com:

- `JIRA_URL` — ex. `https://opagames.atlassian.net`
- `JIRA_EMAIL` — o e-mail da sua conta Atlassian
- `JIRA_API_TOKEN` — gere em https://id.atlassian.com/manage-profile/security/api-tokens
- `JIRA_DEFAULT_PROJECT` — a key do projeto (ex. `OPA`)

Valide:

```bash
bash .claude/skills/jira/jira.sh whoami
```

`.claude/jira.env` está no `.gitignore`. **Nunca** commite ele.

### O que a skill faz sozinha

Depois disso, é só falar normalmente com o Claude Code:

| você diz | o que acontece |
|---|---|
| "cria uma branch pra ajustar o autoplay" | cria a issue no Jira, atribui a você, move pra **Em progresso**, cria o branch `feat/OPA-123-...` e devolve o link |
| "o que já foi feito?" / "atualiza o Jira" | lê os commits do branch, resume pra você no chat, comenta o progresso no card e ajusta o status |
| "manda pra review" / "conclui o card" | transiciona o card (conclusão pede confirmação) |
| "quais minhas tarefas?" | lista suas issues abertas |

Comandos manuais do helper: `bash .claude/skills/jira/jira.sh` sem argumentos lista todos.

### Precedência de config

`$JIRA_ENV` → `<raiz-do-repo>/.claude/jira.env` → `~/.claude/jira.env`

Ou seja: dá pra ter um token global na máquina e sobrescrever por repositório quando precisar.

### Requisitos

`bash`, `curl` e `python` no PATH. No Windows, o Git Bash que vem com o Git já resolve.
