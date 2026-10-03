#!/usr/bin/env bash
# Daily PostgreSQL dump of the SnapLingo prod stack, keeping the last 7 days.
# Install on the VM:  (crontab -l; echo "0 3 * * * $HOME/snaplingo/infra/backup-db.sh") | crontab -
# Restore:            gunzip -c backup.sql.gz | docker exec -i infra-postgres-1 psql -U snaplingo snaplingo
set -euo pipefail
DIR="${BACKUP_DIR:-$HOME/backups}"
mkdir -p "$DIR"
OUT="$DIR/snaplingo-$(date +%F-%H%M).sql.gz"
sudo docker exec infra-postgres-1 pg_dump -U "${POSTGRES_USER:-snaplingo}" "${POSTGRES_DB:-snaplingo}" | gzip > "$OUT"
find "$DIR" -name 'snaplingo-*.sql.gz' -mtime +7 -delete
echo "backup written: $OUT ($(du -h "$OUT" | cut -f1))"
