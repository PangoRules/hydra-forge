#!/usr/bin/env bash
set -euo pipefail

# Portable by design: derives the repo root from this script's own location
# (deploy/postgres-backup.sh -> repo root is one level up) instead of a
# hardcoded path, so the same script works unmodified on any box this repo
# gets cloned/pulled onto — same "plug and play" convention as
# deploy/tailscale-https-setup.sh's hostname auto-detection.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker-compose.yml"
ENV_FILE="$REPO_ROOT/.env"

# Pick up the real configured POSTGRES_USER/POSTGRES_DB (not just the
# .env.example defaults) so this still works if a deployment renamed them.
if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

BACKUP_DIR="${HYDRAFORGE_BACKUP_DIR:-/etc/hydraforge/backups}"
RETENTION_DAYS="${HYDRAFORGE_BACKUP_RETENTION_DAYS:-14}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"

mkdir -p "$BACKUP_DIR"

docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_dump -U "${POSTGRES_USER:-hydraforge}" -F custom "${POSTGRES_DB:-hydraforge}" \
  > "$BACKUP_DIR/hydraforge-$TIMESTAMP.dump"

# Prune anything older than RETENTION_DAYS
find "$BACKUP_DIR" -name "hydraforge-*.dump" -mtime "+$RETENTION_DAYS" -delete

echo "Backup complete: $BACKUP_DIR/hydraforge-$TIMESTAMP.dump"
