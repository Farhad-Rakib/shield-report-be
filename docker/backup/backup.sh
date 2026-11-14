#!/bin/bash
# ShieldReport backup job (runs in the `backup` compose service).
#   - SQL Server full backup (BACKUP DATABASE ... WITH CHECKSUM) to the shared backups volume
#   - tar.gz of uploaded files (report drafts/PDFs, templates, attachments) + DataProtection keys
#     (without the keys, saved LLM API keys can't be decrypted after a restore)
#   - deletes backups older than BACKUP_RETENTION_DAYS
# Usage:  backup.sh          -> backup once at start (if BACKUP_ON_START=true), then daily at BACKUP_HOUR_UTC
#         backup.sh now      -> one backup, then exit   (docker compose run --rm backup now)
# Off-server copies are NOT made here: sync the backups volume to object storage (rclone/S3) from the host.
set -euo pipefail

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"
: "${DB_NAME:=ShieldReportDb}"
: "${BACKUP_HOUR_UTC:=2}"
: "${BACKUP_RETENTION_DAYS:=14}"
: "${BACKUP_ON_START:=true}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
BACKUP_DIR=/backups              # same volume is mounted in the mssql container at /var/opt/mssql/backups
SQL_BACKUP_DIR=/var/opt/mssql/backups

log() { echo "[backup $(date -u +%FT%TZ)] $*"; }

# SQL Server runs as uid 10001 (mssql) and must be able to write into the shared volume.
chown 10001:0 "$BACKUP_DIR"

run_backup() {
  local ts; ts=$(date -u +%Y%m%d-%H%M%S)
  log "starting backup $ts"
  "$SQLCMD" -S mssql -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q \
    "BACKUP DATABASE [$DB_NAME] TO DISK = N'$SQL_BACKUP_DIR/${DB_NAME}_${ts}.bak' WITH INIT, CHECKSUM"
  tar -czf "$BACKUP_DIR/files_${ts}.tar.gz" -C /data uploads dataprotection-keys
  find "$BACKUP_DIR" \( -name '*.bak' -o -name '*.tar.gz' \) -mtime +"$BACKUP_RETENTION_DAYS" -print -delete
  log "backup $ts done: $(du -sh "$BACKUP_DIR" | cut -f1) in $BACKUP_DIR"
}

seconds_until_next_run() {
  local now target
  now=$(date -u +%s)
  target=$(date -u -d "today ${BACKUP_HOUR_UTC}:00" +%s)
  (( target <= now )) && target=$(( target + 86400 ))
  echo $(( target - now ))
}

if [[ "${1:-}" == "now" ]]; then
  run_backup
  exit 0
fi

if [[ "$BACKUP_ON_START" == "true" ]]; then
  run_backup || log "startup backup FAILED"
fi

while true; do
  wait_s=$(seconds_until_next_run)
  log "next backup in $(( wait_s / 3600 ))h$(( wait_s % 3600 / 60 ))m"
  sleep "$wait_s"
  run_backup || log "scheduled backup FAILED"
done
