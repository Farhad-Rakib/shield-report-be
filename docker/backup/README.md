# Backups and restore

The `backup` service (see `docker-compose.yml`) writes to the `shieldreport-backups` volume:

| File | Contents |
|---|---|
| `ShieldReportDb_<UTC timestamp>.bak` | Full SQL Server backup, verified with `CHECKSUM` |
| `files_<UTC timestamp>.tar.gz` | `uploads/` (report drafts, PDFs, templates, attachments) and `dataprotection-keys/` |

It runs once when the stack starts (`BACKUP_ON_START=true`), then daily at `BACKUP_HOUR_UTC`, and deletes files older than `BACKUP_RETENTION_DAYS`.

**Always restore the database and the files from the same timestamp.** Without the matching DataProtection keys, every saved LLM API key becomes unreadable and has to be entered again.

## Take a backup now (do this before every upgrade)

```bash
docker compose run --rm backup now
```

## Copy backups off the server

Backups on the same disk don't survive losing the disk. Copy the volume somewhere else regularly, for example from a host cron job:

```bash
docker run --rm -v shieldreport-backups:/backups:ro -v "$PWD/offsite":/out alpine \
  sh -c 'cp -u /backups/* /out/'
# then sync ./offsite to object storage with rclone / aws s3 sync / similar
```

## Restore

```bash
# 1. Stop the app (keep mssql running)
docker compose stop api fe proxy

# 2. Pick a timestamp
docker run --rm -v shieldreport-backups:/b alpine ls -lh /b

# 3. Restore the database (replaces the current one)
TS=20261115-020000
docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q \
  "ALTER DATABASE [ShieldReportDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   RESTORE DATABASE [ShieldReportDb] FROM DISK = N'/var/opt/mssql/backups/ShieldReportDb_$TS.bak' WITH REPLACE;
   ALTER DATABASE [ShieldReportDb] SET MULTI_USER;"

# 4. Restore uploaded files and DataProtection keys from the same timestamp
docker run --rm -v shieldreport-backups:/b:ro \
  -v shield-report-be_api_uploads:/data/uploads \
  -v shield-report-be_dataprotection_keys:/data/dataprotection-keys \
  alpine sh -c "rm -rf /data/uploads/* /data/dataprotection-keys/* && tar -xzf /b/files_$TS.tar.gz -C /data"

# 5. Start the app again
docker compose up -d
```

The upload and key volume names carry the compose project prefix (`shield-report-be_` by default). Check them with `docker volume ls`.

Practise a restore on a spare machine at least once. A backup you have never restored is not a backup yet.
