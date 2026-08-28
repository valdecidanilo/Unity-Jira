#!/usr/bin/env bash
# Jira REST API v3 helper.
# Dependencias: bash, curl, awk (POSIX). Sem python, sem jq.
# Config, em ordem de precedencia:
#   1. $JIRA_ENV                       (caminho explicito)
#   2. <raiz-do-repo>/.claude/jira.env (por projeto; fica no .gitignore)
#   3. ~/.claude/jira.env              (global da maquina)
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
for c in "${JIRA_ENV:-}" "${REPO_ROOT:+$REPO_ROOT/.claude/jira.env}" "$HOME/.claude/jira.env"; do
  if [ -n "$c" ] && [ -f "$c" ]; then CFG="$c"; . "$c"; break; fi
done

: "${JIRA_URL:?falta JIRA_URL (copie .claude/jira.env.example para .claude/jira.env)}"
: "${JIRA_EMAIL:?falta JIRA_EMAIL}"
: "${JIRA_API_TOKEN:?falta JIRA_API_TOKEN}"

JIRA_URL="${JIRA_URL%/}"
API="$JIRA_URL/rest/api/3"

TMPD="$(mktemp -d)"
trap 'rm -rf "$TMPD"' EXIT

# ---------------------------------------------------------------- HTTP

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

# ---------------------------------------------------------------- JSON (awk)

# Achata JSON do stdin em linhas "caminho<TAB>valor".
# Ex.: transitions.0.to.name<TAB>Em progresso
jflat() {
  awk '
  function ws(   c){
    while (i <= n) {
      c = substr(buf, i, 1)
      if (c == " " || c == "\t" || c == "\n" || c == "\r") i++; else break
    }
  }
  function pstr(   out, c, e){
    i++                                   # abre aspas
    out = ""
    while (i <= n) {
      c = substr(buf, i, 1)
      if (c == "\\") {
        e = substr(buf, i + 1, 1); i += 2
        if (e == "n")      out = out "\n"
        else if (e == "t") out = out "\t"
        else if (e == "r") out = out ""
        else if (e == "b") out = out ""
        else if (e == "f") out = out ""
        else if (e == "u") { out = out "?"; i += 4 }
        else               out = out e
      } else if (c == "\"") { i++; return out }
      else { out = out c; i++ }
    }
    return out
  }
  function pv(path,   c, k, idx, v){
    ws()
    c = substr(buf, i, 1)
    if (c == "{") {
      i++; ws()
      if (substr(buf, i, 1) == "}") { i++; return }
      while (1) {
        ws(); k = pstr(); ws(); i++       # pula os dois-pontos
        pv(path == "" ? k : path "." k)
        ws(); c = substr(buf, i, 1); i++
        if (c == "}") return
        if (c != ",") return              # json malformado: aborta o ramo
      }
    } else if (c == "[") {
      i++; ws(); idx = 0
      if (substr(buf, i, 1) == "]") { i++; return }
      while (1) {
        pv(path == "" ? idx : path "." idx); idx++
        ws(); c = substr(buf, i, 1); i++
        if (c == "]") return
        if (c != ",") return
      }
    } else if (c == "\"") {
      v = pstr()
      gsub(/\n/, "\\n", v)              # mantem uma linha por caminho
      print path "\t" v
    } else {
      v = ""
      while (i <= n) {
        c = substr(buf, i, 1)
        if (c == "," || c == "}" || c == "]" || c == " " || c == "\n" || c == "\t" || c == "\r") break
        v = v c; i++
      }
      print path "\t" v
    }
  }
  { buf = buf $0 "\n" }
  END { i = 1; n = length(buf); pv("") }
  '
}

# valor de um caminho exato, lendo JSON do stdin
jval() { jflat | awk -F'\t' -v p="$1" '$1 == p { print $2; exit }'; }

# string JSON escapada e entre aspas, a partir de $1 (multilinha vira \n)
jstr() {
  printf '%s' "${1:-}" | awk '
  function esc(x){
    gsub(/\\/, "\\\\", x)
    gsub(/"/,  "\\\"", x)
    gsub(/\t/, "\\t",  x)
    gsub(/\r/, "",     x)
    return x
  }
  { out = out (NR > 1 ? "\\n" : "") esc($0) }
  END { printf "\"%s\"", out }
  '
}

# ADF (Atlassian Document Format) a partir de texto puro no stdin
adf() {
  awk '
  function esc(x){
    gsub(/\\/, "\\\\", x)
    gsub(/"/,  "\\\"", x)
    gsub(/\t/, "\\t",  x)
    gsub(/\r/, "",     x)
    return x
  }
  BEGIN { printf "{\"type\":\"doc\",\"version\":1,\"content\":["; first = 1 }
  {
    if (!first) printf ","
    first = 0
    line = esc($0)
    if (line ~ /^[ \t]*$/) printf "{\"type\":\"paragraph\",\"content\":[]}"
    else printf "{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"%s\"}]}", line
  }
  END {
    if (first) printf "{\"type\":\"paragraph\",\"content\":[]}"
    printf "]}"
  }
  '
}

# ---------------------------------------------------------------- Jira

do_assign_me() {
  local aid
  aid=$(req GET /myself | jval accountId)
  [ -n "$aid" ] || return 1
  req PUT "/issue/$1/assignee" "{\"accountId\":$(jstr "$aid")}" >/dev/null
}

# procura a 1a transicao cujo nome (ou status destino) contenha um dos termos,
# na ordem dos termos; imprime "id<TAB>nome-do-status-destino"
find_transition() { # KEY termo1 termo2 ...
  local key="$1"; shift
  req GET "/issue/$key/transitions" | jflat | awk -F'\t' -v terms="$(printf '%s\n' "$@")" '
  $1 ~ /^transitions\.[0-9]+\.id$/       { split($1, a, "."); id[a[2]] = $2; if (a[2]+0 > max) max = a[2]+0 }
  $1 ~ /^transitions\.[0-9]+\.name$/     { split($1, a, "."); nm[a[2]] = $2 }
  $1 ~ /^transitions\.[0-9]+\.to\.name$/ { split($1, a, "."); tn[a[2]] = $2 }
  END {
    t = split(terms, T, "\n")
    for (k = 1; k <= t; k++) {
      w = tolower(T[k])
      if (w == "") continue
      for (j = 0; j <= max; j++) {
        if (!(j in id)) continue
        if (index(tolower(nm[j]), w) > 0 || index(tolower(tn[j]), w) > 0) {
          print id[j] "\t" tn[j]; exit
        }
      }
    }
  }'
}

# transiciona tentando varios sinonimos; imprime o nome real do status destino
do_move_any() { # KEY termo1 termo2 ...
  local key="$1"; shift
  local hit tid name
  hit=$(find_transition "$key" "$@")
  [ -n "$hit" ] || return 1
  tid=${hit%%	*}
  name=${hit#*	}
  req POST "/issue/$key/transitions" "{\"transition\":{\"id\":$(jstr "$tid")}}" >/dev/null
  echo "$name"
}

list_transitions() { # imprime as transicoes reais no stderr
  req GET "/issue/$1/transitions" | jflat | awk -F'\t' '
  $1 ~ /^transitions\.[0-9]+\.name$/     { split($1, a, "."); nm[a[2]] = $2; if (a[2]+0 > max) max = a[2]+0 }
  $1 ~ /^transitions\.[0-9]+\.to\.name$/ { split($1, a, "."); tn[a[2]] = $2 }
  END { for (j = 0; j <= max; j++) if (j in nm) printf "  - %s -> %s\n", nm[j], tn[j] }
  ' >&2
}

# slug ascii para nome de branch
slug() {
  printf '%s' "$1" | awk '
  {
    s = $0
    # alternacao, nao classe: em locale byte uma classe [áã] casaria bytes soltos
    gsub(/á|à|â|ã|ä|Á|À|Â|Ã|Ä/, "a", s)
    gsub(/é|è|ê|ë|É|È|Ê|Ë/,     "e", s)
    gsub(/í|ì|î|ï|Í|Ì|Î|Ï/,     "i", s)
    gsub(/ó|ò|ô|õ|ö|Ó|Ò|Ô|Õ|Ö/, "o", s)
    gsub(/ú|ù|û|ü|Ú|Ù|Û|Ü/,     "u", s)
    gsub(/ç|Ç/, "c", s); gsub(/ñ|Ñ/, "n", s)
    s = tolower(s)
    gsub(/[^a-z0-9]+/, "-", s)
    gsub(/^-+/, "", s); gsub(/-+$/, "", s)
    if (length(s) > 48) { s = substr(s, 1, 48); sub(/-[^-]*$/, "", s) }
    gsub(/^-+/, "", s); gsub(/-+$/, "", s)
    print s
  }'
}

branch_prefix() { # tipo da issue -> prefixo git
  case "$(printf '%s' "$1" | tr 'A-Z' 'a-z')" in
    bug|defeito|erro|hotfix) echo fix ;;
    *)                       echo feat ;;
  esac
}

build_fields() { # PROJ TIPO SUMMARY [DESC]
  local body
  body="{\"project\":{\"key\":$(jstr "$1")},\"issuetype\":{\"name\":$(jstr "$2")},\"summary\":$(jstr "$3")"
  if [ -n "${4:-}" ]; then
    body="$body,\"description\":$(printf '%s' "$4" | adf)"
  fi
  printf '{"fields":%s}}' "$body"
}

# ---------------------------------------------------------------- comandos

cmd="${1:-help}"; shift || true

case "$cmd" in
  whoami)   req GET /myself ;;
  projects) req GET "/project/search?maxResults=100" ;;
  types)    req GET "/issue/createmeta/$1/issuetypes" ;;
  url)      echo "$JIRA_URL/browse/$1" ;;

  search)   # search "<JQL>" [max]
    max="${2:-25}"
    case "$max" in ''|*[!0-9]*) echo "max precisa ser numero" >&2; exit 1 ;; esac
    req POST /search/jql "{\"jql\":$(jstr "$1"),\"maxResults\":$max,\"fields\":[\"key\",\"summary\",\"status\",\"assignee\",\"issuetype\",\"priority\",\"updated\"]}" ;;

  mine)
    req POST /search/jql '{"jql":"assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC","maxResults":50,"fields":["key","summary","status","issuetype","priority","updated"]}' ;;

  view)     req GET "/issue/$1?fields=summary,description,status,assignee,reporter,issuetype,priority,labels,parent,subtasks,comment" ;;

  create)   # create <PROJ> <TIPO> <TITULO> [DESC]
    req POST /issue "$(build_fields "$1" "$2" "$3" "${4:-}")" ;;

  start)    # start <PROJ> <TIPO> <TITULO> [DESC]
    req POST /issue "$(build_fields "$1" "$2" "$3" "${4:-}")" > "$TMPD/created.json"
    key=$(jval key < "$TMPD/created.json")
    if [ -z "$key" ]; then cat "$TMPD/created.json" >&2; exit 1; fi
    do_assign_me "$key" || true
    moved=$(do_move_any "$key" "Progress" "Andamento" "Doing" "Iniciar" "Start" || echo "")
    br="$(branch_prefix "$2")/$key-$(slug "$3")"
    printf '{"ok":true,"key":%s,"url":%s,"status":%s,"branch":%s}\n' \
      "$(jstr "$key")" "$(jstr "$JIRA_URL/browse/$key")" \
      "$(jstr "${moved:-nao movido (rode transitions)}")" "$(jstr "$br")" ;;

  branch)   # branch <KEY>
    req GET "/issue/$1?fields=summary,issuetype" | jflat > "$TMPD/i.flat"
    s=$(awk -F'\t' '$1 == "fields.summary" { print $2; exit }' "$TMPD/i.flat")
    t=$(awk -F'\t' '$1 == "fields.issuetype.name" { print $2; exit }' "$TMPD/i.flat")
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
    req POST "/issue/$1/comment" "{\"body\":$(printf '%s' "$2" | adf)}" >/dev/null
    echo "{\"ok\":\"comentado em $1\"}" ;;

  desc)     # desc <KEY> <TEXTO>
    req PUT "/issue/$1" "{\"fields\":{\"description\":$(printf '%s' "$2" | adf)}}" >/dev/null
    echo "{\"ok\":\"descricao atualizada em $1\"}" ;;

  edit)     # edit <KEY> <TITULO>
    req PUT "/issue/$1" "{\"fields\":{\"summary\":$(jstr "$2")}}" >/dev/null
    echo "{\"ok\":\"updated $1\"}" ;;

  transitions) req GET "/issue/$1/transitions" ;;

  move)     # move <KEY> <STATUS>
    m=$(do_move_any "$1" "$2" || echo "")
    if [ -n "$m" ]; then echo "{\"ok\":\"$1 -> $m\"}"
    else echo "transicao \"$2\" indisponivel para $1. Disponiveis:" >&2; list_transitions "$1"; exit 1; fi ;;

  assign)   # assign <KEY> me|<accountId>
    if [ "$2" = "me" ]; then do_assign_me "$1"
    else req PUT "/issue/$1/assignee" "{\"accountId\":$(jstr "$2")}" >/dev/null; fi
    echo "{\"ok\":\"assigned $1\"}" ;;

  flat)     # flat <KEY> - issue achatada em caminho<TAB>valor (debug/scripts)
    req GET "/issue/$1?fields=summary,description,status,assignee,issuetype,priority,labels" | jflat ;;

  *) cat <<'H'
uso: jira.sh <comando>          (requer apenas bash, curl e awk)
  whoami                                 valida credenciais
  projects                               lista projetos
  types <PROJ>                           tipos de issue do projeto
  mine                                   minhas issues abertas
  search "<JQL>" [max]                   busca por JQL
  view <KEY>                             detalhe da issue
  flat <KEY>                             issue achatada em caminho<TAB>valor
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
