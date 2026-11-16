#!/usr/bin/env bash
# Tail production container logs from Dozzle (https://logs.phitv.me) on the Synology host.
# Credentials are loaded from scripts/ops.env.local (gitignored; template: ops.env.example)
# or from the DOZZLE_USER / DOZZLE_PASS environment variables.
# Usage: scripts/prod-logs.sh [list | <container-name> [seconds]]
#   scripts/prod-logs.sh list
#   scripts/prod-logs.sh snaplingo-api 10
set -euo pipefail

ENV_FILE="$(dirname "$0")/ops.env.local"
[ -f "$ENV_FILE" ] && set -a && . "$ENV_FILE" && set +a
BASE="${DOZZLE_URL:-https://logs.phitv.me}"
: "${DOZZLE_USER:?set DOZZLE_USER}" "${DOZZLE_PASS:?set DOZZLE_PASS}"
UA="curl/8.5.0" # Cloudflare rejects default python/empty UAs
JAR="$(mktemp)"; trap 'rm -f "$JAR"' EXIT

curl -fsS -A "$UA" -c "$JAR" -o /dev/null \
  --data-urlencode "username=$DOZZLE_USER" --data-urlencode "password=$DOZZLE_PASS" "$BASE/api/token"

containers() { # prints: id<TAB>host<TAB>name<TAB>state
  curl -s -m 4 -N -A "$UA" -b "$JAR" "$BASE/api/events/stream" \
    | sed -n '/^event: containers-changed/{n;s/^data: //p;q}' \
    | python -c 'import json,sys
for c in json.load(sys.stdin): print(c["id"],c["host"],c["name"],c["state"],sep="\t")'
}

cmd="${1:-list}"
if [ "$cmd" = "list" ]; then containers; exit 0; fi

read -r id host _ < <(containers | awk -F'\t' -v n="$cmd" '$3==n')
[ -n "${id:-}" ] || { echo "container '$cmd' not found; run: $0 list" >&2; exit 1; }
curl -s -m "${2:-10}" -N -A "$UA" -b "$JAR" -X POST \
  --data "container=$host/$id&stdout=true&stderr=true" "$BASE/api/containers/logs" || true
