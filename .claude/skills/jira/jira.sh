#!/usr/bin/env bash
# Jira REST API v3 helper. Config: ~/.claude/jira.env (JIRA_URL, JIRA_EMAIL, JIRA_API_TOKEN)
set -euo pipefail

# Config, em ordem de precedencia:
#   1. $JIRA_ENV                       (caminho explicito)
#   2. <raiz-do-repo>/.claude/jira.env (por projeto; fica no .gitignore)
#   3. ~/.claude/jira.env              (global da maquina)
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
for c in "${JIRA_ENV:-}" "${REPO_ROOT:+$REPO_ROOT/.claude/jira.env}" "$HOME/.claude/jira.env"; do
  if [ -n "$c" ] && [ -f "$c" ]; then CFG="$c"; . "$c"; break; fi
done

: "${JIRA_URL:?falta JIRA_URL (defina em ~/.claude/jira.env)}"
: "${JIRA_EMAIL:?falta JIRA_EMAIL}"
: "${JIRA_API_TOKEN:?falta JIRA_API_TOKEN}"

JIRA_URL="${JIRA_URL%/}"
API="$JIRA_URL/rest/api/3"

PY="$(command -v python || command -v python3 || echo python)"

TMPD="$(mktemp -d)"
trap 'rm -rf "$TMPD"' EXIT

req() { # method path [body]
  local m="$1" p="$2" b="${3:-}"
  if [ -n "$b" ]; then
    curl -sS -u "$JIRA_EMAIL:$JIRA_API_TOKEN" -X "$m" \
      -H 'Content-Type: application/json' -H 'Accept: application/json' \
      --data-binary "$b" "$API$p"
  else
    curl -sS -u "$JIRA_EMAIL:$JIRA_API_TOKEN" -X "$m" \
      -H 'Accept: application/json' "$API$p"
  fi
}

# ADF (Atlassian Document Format) a partir de texto puro
adf() { "$PY" -c '
import json,sys
t=sys.stdin.read()
paras=[{"type":"paragraph","content":([{"type":"text","text":l}] if l.strip() else [])} for l in t.split("\n")]
print(json.dumps({"type":"doc","version":1,"content":paras}))'; }

jget() { "$PY" -c '
import json,sys
d=json.load(sys.stdin)
for k in sys.argv[1].split("."):
    d = d.get(k) if isinstance(d,dict) else None
print(d if d is not None else "")' "$1"; }

do_assign_me() {
  local aid
  aid=$(req GET /myself | jget accountId)
  req PUT "/issue/$1/assignee" "{\"accountId\":\"$aid\"}" >/dev/null
}

# transiciona por nome; retorna 1 se nao houver transicao compativel
do_move() { # KEY NOME
  local tid
  tid=$(req GET "/issue/$1/transitions" | "$PY" -c '
import json,sys
w=sys.argv[1].lower()
for t in json.load(sys.stdin)["transitions"]:
    if w in t["name"].lower() or w in t["to"]["name"].lower():
        print(t["id"]); break' "$2")
  [ -n "$tid" ] || return 1
  req POST "/issue/$1/transitions" "{\"transition\":{\"id\":\"$tid\"}}" >/dev/null
  return 0
}

# tenta varios sinonimos de status numa unica leitura de transicoes;
# imprime o nome real do status alvo
do_move_any() { # KEY termo1 termo2 ...
  local key="$1"; shift
  local out tid name
  out=$(req GET "/issue/$key/transitions" | "$PY" -c '
import json,sys
terms=[a.lower() for a in sys.argv[1:]]
trs=json.load(sys.stdin)["transitions"]
for w in terms:
    for t in trs:
        if w in t["name"].lower() or w in t["to"]["name"].lower():
            print(t["id"]); print(t["to"]["name"]); sys.exit(0)
' "$@")
  [ -n "$out" ] || return 1
  tid=$(echo "$out" | sed -n 1p)
  name=$(echo "$out" | sed -n 2p)
  req POST "/issue/$key/transitions" "{\"transition\":{\"id\":\"$tid\"}}" >/dev/null
  echo "$name"
  return 0
}

list_transitions() {
  req GET "/issue/$1/transitions" | "$PY" -c '
import json,sys
for t in json.load(sys.stdin)["transitions"]:
    sys.stderr.write("  - %s -> %s\n" % (t["name"], t["to"]["name"]))'
}

slug() {
  "$PY" -c '
import re,sys,unicodedata
s=unicodedata.normalize("NFKD",sys.argv[1]).encode("ascii","ignore").decode()
s=re.sub(r"[^a-zA-Z0-9]+","-",s).strip("-").lower()
if len(s)>48:
    s=s[:48].rsplit("-",1)[0]
print(s.strip("-"))' "$1"
}

branch_prefix() { # tipo da issue -> prefixo git
  case "$(echo "$1" | tr 'A-Z' 'a-z')" in
    bug|defeito|erro|hotfix) echo fix ;;
    *)                       echo feat ;;
  esac
}

build_fields() { # PROJ TIPO SUMMARY DESC
  "$PY" -c '
import json,sys
proj,typ,summ,desc=sys.argv[1:5]
f={"project":{"key":proj},"issuetype":{"name":typ},"summary":summ}
if desc:
    f["description"]={"type":"doc","version":1,"content":[
        {"type":"paragraph","content":([{"type":"text","text":l}] if l.strip() else [])}
        for l in desc.split("\n")]}
print(json.dumps({"fields":f}))' "$1" "$2" "$3" "$4"
}

cmd="${1:-help}"; shift || true

case "$cmd" in
  whoami)   req GET /myself ;;
  projects) req GET "/project/search?maxResults=100" ;;
  types)    req GET "/issue/createmeta/$1/issuetypes" ;;
  url)      echo "$JIRA_URL/browse/$1" ;;

  search)   # search "<JQL>" [max]
    "$PY" -c '
import json,sys
print(json.dumps({"jql":sys.argv[1],"maxResults":int(sys.argv[2]),
 "fields":["key","summary","status","assignee","issuetype","priority","updated"]}))' "$1" "${2:-25}" > "$TMPD/jql.json"
    req POST /search/jql "$(cat "$TMPD/jql.json")" ;;

  mine)
    req POST /search/jql '{"jql":"assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC","maxResults":50,"fields":["key","summary","status","issuetype","priority","updated"]}' ;;

  view)     req GET "/issue/$1?fields=summary,description,status,assignee,reporter,issuetype,priority,labels,parent,subtasks,comment" ;;

  create)   # create <PROJ> <TIPO> <TITULO> [DESC]
    build_fields "$1" "$2" "$3" "${4:-}" > "$TMPD/new.json"
    req POST /issue "$(cat "$TMPD/new.json")" ;;

  start)    # start <PROJ> <TIPO> <TITULO> [DESC] -> cria + assume + em andamento + branch sugerido
    build_fields "$1" "$2" "$3" "${4:-}" > "$TMPD/new.json"
    req POST /issue "$(cat "$TMPD/new.json")" > "$TMPD/created.json"
    key=$(jget key < "$TMPD/created.json")
    if [ -z "$key" ]; then cat "$TMPD/created.json" >&2; exit 1; fi
    do_assign_me "$key" || true
    moved=$(do_move_any "$key" "Progress" "Andamento" "Doing" "Iniciar" "Start" || echo "")
    br="$(branch_prefix "$2")/$key-$(slug "$3")"
    "$PY" -c '
import json,sys
print(json.dumps({"ok":True,"key":sys.argv[1],"url":sys.argv[2],
 "status":sys.argv[3] or "nao movido (rode transitions)","branch":sys.argv[4]}, ensure_ascii=False))' \
      "$key" "$JIRA_URL/browse/$key" "$moved" "$br" ;;

  branch)   # branch <KEY>
    req GET "/issue/$1?fields=summary,issuetype" > "$TMPD/i.json"
    s=$(jget fields.summary < "$TMPD/i.json")
    t=$(jget fields.issuetype.name < "$TMPD/i.json")
    echo "$(branch_prefix "$t")/$1-$(slug "$s")" ;;

  progress) # progress <KEY>
    do_assign_me "$1" || true
    m=$(do_move_any "$1" "Progress" "Andamento" "Doing" "Iniciar" "Start" || echo "")
    if [ -n "$m" ]; then echo "{\"ok\":\"$1 -> $m\"}"
    else echo "sem transicao de 'em andamento' para $1. Disponiveis:" >&2; list_transitions "$1"; exit 1; fi ;;

  review)   # review <KEY>
    m=$(do_move_any "$1" "Review" "Revis" "Homolog" "Teste" || echo "")
    if [ -n "$m" ]; then echo "{\"ok\":\"$1 -> $m\"}"
    else echo "sem transicao de review para $1. Disponiveis:" >&2; list_transitions "$1"; exit 1; fi ;;

  done)     # done <KEY>
    m=$(do_move_any "$1" "Conclu" "Done" "Finaliz" "Pronto" "Resolv" || echo "")
    if [ -n "$m" ]; then echo "{\"ok\":\"$1 -> $m\"}"
    else echo "sem transicao de conclusao para $1. Disponiveis:" >&2; list_transitions "$1"; exit 1; fi ;;

  comment)  # comment <KEY> <TEXTO>
    printf '%s' "$2" | adf > "$TMPD/adf.json"
    req POST "/issue/$1/comment" "{\"body\":$(cat "$TMPD/adf.json")}" > /dev/null
    echo "{\"ok\":\"comentado em $1\"}" ;;

  desc)     # desc <KEY> <TEXTO> (substitui a descricao)
    printf '%s' "$2" | adf > "$TMPD/adf.json"
    req PUT "/issue/$1" "{\"fields\":{\"description\":$(cat "$TMPD/adf.json")}}" > /dev/null
    echo "{\"ok\":\"descricao atualizada em $1\"}" ;;

  edit)     # edit <KEY> <TITULO>
    req PUT "/issue/$1" "$("$PY" -c 'import json,sys;print(json.dumps({"fields":{"summary":sys.argv[1]}}))' "$2")" > /dev/null
    echo "{\"ok\":\"updated $1\"}" ;;

  transitions) req GET "/issue/$1/transitions" ;;

  move)     # move <KEY> <STATUS>
    if do_move "$1" "$2"; then echo "{\"ok\":\"$1 -> $2\"}"
    else echo "transicao \"$2\" indisponivel para $1. Disponiveis:" >&2; list_transitions "$1"; exit 1; fi ;;

  assign)   # assign <KEY> me|<accountId>
    if [ "$2" = "me" ]; then do_assign_me "$1"
    else req PUT "/issue/$1/assignee" "{\"accountId\":\"$2\"}" > /dev/null; fi
    echo "{\"ok\":\"assigned $1\"}" ;;

  *) cat <<'H'
uso: jira.sh <comando>
  whoami                                 valida credenciais
  projects                               lista projetos
  types <PROJ>                           tipos de issue do projeto
  mine                                   minhas issues abertas
  search "<JQL>" [max]                   busca por JQL
  view <KEY>                             detalhe da issue
  create <PROJ> <TIPO> <TITULO> [DESC]   cria a issue
  start  <PROJ> <TIPO> <TITULO> [DESC]   cria + assume + em andamento + branch sugerido
  branch <KEY>                           nome de branch sugerido para o card
  progress <KEY>                         assume e move para "em andamento"
  review <KEY>                           move para revisao
  done <KEY>                             move para concluido
  edit <KEY> <TITULO>                    troca o titulo
  desc <KEY> <TEXTO>                     substitui a descricao
  comment <KEY> <TEXTO>                  comenta
  transitions <KEY>                      transicoes disponiveis
  move <KEY> <STATUS>                    transiciona por nome
  assign <KEY> me|<accountId>            atribui
  url <KEY>                              link do card
H
    ;;
esac
