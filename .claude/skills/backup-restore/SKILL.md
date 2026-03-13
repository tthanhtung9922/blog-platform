# backup-restore

Execute backup and restore procedures for PostgreSQL (pgBackRest PITR), Redis, and MinIO following the project's disaster recovery runbook.

## Arguments

- `component` (required) — `postgresql`, `redis`, `minio`, or `all`
- `action` (required) — `backup`, `restore`, `verify`, `status`
- `type` (optional for backup) — `full`, `incremental`, `pitr`
- `target` (optional for restore) — Target timestamp for PITR (ISO 8601) or backup ID

## Instructions

You are executing backup/restore procedures for the blog-platform. Follow the disaster recovery runbook exactly to ensure data safety.

### RTO/RPO Targets

| Component | RPO | RTO | Strategy |
|---|---|---|---|
| PostgreSQL | < 1 min | < 30 min | WAL archiving + PITR |
| Redis (cache) | N/A | < 5 min | Cache-aside (rebuild from DB) |
| Redis (session) | < 1 min | < 5 min | AOF everysec + PVC |
| MinIO | 0 (versioned) | < 15 min | Versioning + replication |

### PostgreSQL — 3-Layer Backup

| Layer | Method | Frequency | Retention |
|---|---|---|---|
| L1 — WAL | Continuous WAL archiving | Real-time | 14 days |
| L2 — Full | pgBackRest full backup | Daily 02:00 UTC | 30 days |
| L3 — Incremental | pgBackRest incremental | Every 6 hours | 7 days |

#### Manual Full Backup

```bash
# Full backup
pgbackrest --stanza=blog-db --type=full backup

# Incremental backup
pgbackrest --stanza=blog-db --type=incr backup

# Check backup status
pgbackrest --stanza=blog-db info
```

#### PITR Restore

```bash
# 1. Stop PostgreSQL
sudo systemctl stop postgresql

# 2. Restore to specific point in time
pgbackrest --stanza=blog-db \
    --type=time \
    --target="2026-03-12T14:30:00+07:00" \
    restore

# 3. Start PostgreSQL (auto-replays WAL to target time)
sudo systemctl start postgresql

# 4. Verify
psql -c "SELECT count(*) FROM posts;" blog_db
```

#### Full Cluster Recovery

```bash
# Restore latest full backup + all WAL
pgbackrest --stanza=blog-db --type=default restore
```

#### pgBackRest Configuration

```ini
# /etc/pgbackrest/pgbackrest.conf
[blog-db]
pg1-path=/var/lib/postgresql/18/main

[global]
repo1-type=s3
repo1-s3-bucket=blog-platform-backups
repo1-s3-endpoint=minio.internal:9000
repo1-s3-region=us-east-1
repo1-s3-uri-style=path
repo1-retention-full=30
repo1-retention-diff=7
repo1-cipher-type=aes-256-cbc
compress-type=zst
compress-level=3
```

### Redis Backup & Restore

#### Redis Persistence Configuration

```conf
# RDB snapshots
save 3600 1
save 300 100
save 60 10000

# AOF
appendonly yes
appendfsync everysec
aof-use-rdb-preamble yes
```

#### Manual Backup

```bash
# Trigger RDB snapshot
redis-cli -h redis-master BGSAVE

# Wait for completion
redis-cli -h redis-master LASTSAVE

# Copy to MinIO
mc cp /data/dump.rdb minio/blog-platform-backups/redis/dump-$(date +%Y%m%d).rdb
```

#### Restore

```bash
# Copy backup to Redis data directory
kubectl cp backup/dump-20260312.rdb redis-0:/data/dump.rdb -n blog-platform

# Restart Redis pod (StatefulSet recreates with data)
kubectl delete pod redis-0 -n blog-platform
```

#### Cache Rebuild (if cache-only data lost)

Since Redis is used as cache-aside, cache loss is non-critical:
```bash
# Cache rebuilds automatically on cache misses
# Just restart the pod — no backup needed for cache-only data
kubectl delete pod redis-0 -n blog-platform
```

### MinIO Backup & Restore

#### Configuration

```bash
# Enable versioning (protects against accidental deletes)
mc version enable minio/blog-media

# Set lifecycle policy
mc ilm rule add --expire-days 90 --noncurrent-expire-days 30 minio/blog-media
```

#### Backup (Mirror)

```bash
# Daily mirror to backup MinIO
mc mirror --overwrite minio-primary/blog-media minio-backup/blog-media

# Or to external S3
mc mirror minio-primary/blog-media s3/blog-platform-media-backup
```

#### Restore

```bash
# Restore from backup MinIO
mc mirror minio-backup/blog-media minio-primary/blog-media

# Restore specific file version
mc cp --version-id=<version-id> minio/blog-media/path/to/file.jpg /tmp/restored.jpg
```

### Kubernetes CronJobs

```yaml
# PostgreSQL daily backup (02:00 UTC)
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pg-backup-full
spec:
  schedule: "0 2 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: pgbackrest
              image: pgbackrest/pgbackrest:latest
              command: ['pgbackrest', '--stanza=blog-db', '--type=full', 'backup']

# PostgreSQL incremental (every 6 hours)
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pg-backup-incr
spec:
  schedule: "0 */6 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: pgbackrest
              image: pgbackrest/pgbackrest:latest
              command: ['pgbackrest', '--stanza=blog-db', '--type=incr', 'backup']

# Redis daily snapshot export (02:30 UTC)
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: redis-backup
spec:
  schedule: "30 2 * * *"
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: redis-backup
              image: redis:8
              command:
                - /bin/sh
                - -c
                - |
                  redis-cli -h redis-master BGSAVE
                  sleep 10
                  mc cp /data/dump.rdb minio/blog-platform-backups/redis/dump-$(date +%Y%m%d).rdb
```

### Verification

```bash
# Verify PostgreSQL backup integrity
pgbackrest --stanza=blog-db verify

# List all backups
pgbackrest --stanza=blog-db info

# Monthly DR drill (restore to temp instance)
pgbackrest --stanza=blog-db --type=default \
    --pg1-path=/tmp/pg-dr-test \
    restore
```

### Emergency Procedures

If the primary database is corrupted or lost:

1. **Stop all API instances** — Prevent writes to corrupted DB
2. **Assess damage** — Check WAL availability, last backup status
3. **PITR to pre-incident timestamp** — Use the most recent clean point
4. **Verify data integrity** — Run consistency checks
5. **Restart API instances** — Resume service
6. **Post-mortem** — Document timeline, root cause, prevention
