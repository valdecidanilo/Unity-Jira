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

### Permissões

O `.claude/settings.json` versionado já libera a skill e os comandos `git` de leitura,
para o Claude Code não pedir aprovação a cada chamada. Na primeira vez que você abrir
o repo, o Claude Code pergunta se confia nas configurações do projeto — aceite.

Preferências pessoais que não devem ir pro repo: use `.claude/settings.local.json`
(já está no `.gitignore`). É lá que entra o que é caminho de máquina — por exemplo
liberar a instalação do ai-jira, que mora fora do repositório:

```json
{
  "permissions": {
    "additionalDirectories": [
      "C:\\Users\\voce\\.ai-jira",
      "C:\\Users\\voce\\.claude\\skills"
    ]
  }
}
```

Sem isso o agente lê esses caminhos e recebe de volta que estão **fora dos
diretórios permitidos**. As execuções disparadas pela janela do Jira dentro do
Unity já recebem esse acesso sozinhas (`--add-dir`); este arquivo é para quando
você mesmo abre o `claude` no terminal.

### Precedência de config

`$JIRA_ENV` → `<raiz-do-repo>/.claude/jira.env` → `~/.claude/jira.env`

Ou seja: dá pra ter um token global na máquina e sobrescrever por repositório quando precisar.

### Requisitos

Apenas `bash`, `curl` e `awk` — os tres ja vem no Git Bash (Windows) e em qualquer Mac/Linux.
Nao precisa de Python nem de jq.
