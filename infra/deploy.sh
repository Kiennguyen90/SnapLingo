#!/usr/bin/env bash
# Deploy entry point on the VM, run by GitHub Actions over SSH (.github/workflows/docker.yml).
#
# Installed once at ~/snaplingo-deploy.sh and bound to the deploy key with a forced command in
# ~/.ssh/authorized_keys, so that key can do nothing else:
#   command="/home/snaplingo/snaplingo-deploy.sh",restrict ssh-ed25519 AAAA... github-deploy
# The workflow runs:  tar czf - infra | ssh deploy@vm "ghcr.io/<owner>/snaplingo-api:sha-1234567"
# (the image reference arrives as SSH_ORIGINAL_COMMAND, the new infra/ files on stdin).
#
# Steps: refresh infra/, point .env.prod at the new image, pull, restart, wait for the API to be
# healthy, and put the previous image back if it never becomes healthy.
# Migrations run on start (Database__MigrateOnStartup) and are NOT undone by a rollback.
#
# Local testing on the VM:  DEPLOY_NO_STDIN=1 DEPLOY_SKIP_PULL=1 ~/snaplingo-deploy.sh <image:tag>
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/snaplingo}"
ALLOWED_PREFIX="${ALLOWED_PREFIX:-ghcr.io/kiennguyen90/}"
HEALTH_ATTEMPTS="${HEALTH_ATTEMPTS:-40}"   # x 3 s
IMAGE_REF="${1:-${SSH_ORIGINAL_COMMAND:-}}"

# The only input from outside: an image reference under our own registry namespace.
if [[ ! "$IMAGE_REF" =~ ^[a-z0-9./_-]+:[A-Za-z0-9_.-]{1,128}$ ]] || [[ "$IMAGE_REF" != "$ALLOWED_PREFIX"* && "${DEPLOY_ANY_IMAGE:-0}" != 1 ]]; then
  echo "refusing image reference: '${IMAGE_REF}' (expected ${ALLOWED_PREFIX}<name>:<tag>)" >&2
  exit 2
fi
NEW_IMAGE="${IMAGE_REF%:*}"
NEW_TAG="${IMAGE_REF##*:}"

cd "$APP_DIR"

DOCKER=(docker)
docker info >/dev/null 2>&1 || DOCKER=(sudo docker)

if [ "${DEPLOY_NO_STDIN:-0}" != 1 ]; then
  echo "==> Updating infra/ from the workflow"
  tar xzf - -C "$APP_DIR" --no-same-owner infra
fi

set_env() { # KEY VALUE -> .env.prod
  if grep -q "^$1=" .env.prod; then sed -i "s|^$1=.*|$1=$2|" .env.prod; else echo "$1=$2" >> .env.prod; fi
}
get_env() { grep "^$1=" .env.prod | head -n1 | cut -d= -f2-; }

PREV_IMAGE="$(get_env API_IMAGE)"
PREV_TAG="$(get_env API_TAG)"

compose() {
  "${DOCKER[@]}" compose -f infra/docker-compose.prod.yml -f infra/docker-compose.https.yml --env-file .env.prod "$@"
}

wait_healthy() {
  for _ in $(seq 1 "$HEALTH_ATTEMPTS"); do
    status="$("${DOCKER[@]}" inspect -f '{{.State.Health.Status}}' infra-api-1 2>/dev/null || echo missing)"
    [ "$status" = healthy ] && return 0
    sleep 3
  done
  return 1
}

echo "==> Deploying ${NEW_IMAGE}:${NEW_TAG} (was ${PREV_IMAGE}:${PREV_TAG})"
set_env API_IMAGE "$NEW_IMAGE"
set_env API_TAG "$NEW_TAG"

if [ "${DEPLOY_SKIP_PULL:-0}" != 1 ] && ! compose pull api; then
  echo "!! could not pull the image; nothing was restarted" >&2
  set_env API_IMAGE "$PREV_IMAGE"; set_env API_TAG "$PREV_TAG"
  exit 1
fi

compose up -d --remove-orphans

if wait_healthy; then
  echo "==> Healthy: ${NEW_IMAGE}:${NEW_TAG}"
  "${DOCKER[@]}" image prune -f >/dev/null
  exit 0
fi

echo "!! API did not become healthy; last log lines:" >&2
"${DOCKER[@]}" logs --tail 25 infra-api-1 >&2 || true
echo "==> Rolling back to ${PREV_IMAGE}:${PREV_TAG}" >&2
set_env API_IMAGE "$PREV_IMAGE"; set_env API_TAG "$PREV_TAG"
compose up -d --remove-orphans
wait_healthy && echo "==> Rolled back and healthy" >&2 || echo "!! rollback is not healthy either" >&2
exit 1
