# Disaster Recovery & Backup

## 7.1 PostgreSQL Backup Strategy

**3-Layer Backup Approach:**

| Layer | Phương pháp | Tần suất | Retention | RPO |
|---|---|---|---|---|
| **L1 — Continuous WAL** | WAL archiving → S3/MinIO | Real-time (mỗi WAL segment) | 14 ngày | ~0 (vài giây) |
| **L2 — Base Backup** | `pgBackRest` full backup | Hàng ngày (02:00 UTC) | 30 ngày | 24 giờ |
| **L3 — Incremental** | `pgBackRest` incremental | Mỗi 6 giờ | 7 ngày | 6 giờ |

**Cấu hình WAL Archiving (postgresql.conf):**

```ini
# === WAL Configuration ===
wal_level = replica                          # required for PITR
archive_mode = on
archive_command = 'pgbackrest --stanza=blog-db archive-push %p'
archive_timeout = 60                         # force archive mỗi 60s nếu không có activity

# === Replication (cho standby) ===
max_wal_senders = 3
wal_keep_size = 1GB
```

**pgBackRest Configuration (`/etc/pgbackrest/pgbackrest.conf`):**

```ini
[blog-db]
pg1-path=/var/lib/postgresql/18/main

[global]
repo1-type=s3                                # Hoặc MinIO (S3-compatible)
repo1-s3-bucket=blog-platform-backups
repo1-s3-endpoint=minio.internal:9000
repo1-s3-region=us-east-1
repo1-s3-key=<from-secret>
repo1-s3-key-secret=<from-secret>
repo1-s3-uri-style=path
repo1-retention-full=30                      # giữ 30 full backups
repo1-retention-diff=7
repo1-cipher-type=aes-256-cbc               # encrypt backups at rest
repo1-cipher-pass=<from-secret>
compress-type=zst                            # zstandard compression
compress-level=3

[global:archive-push]
compress-type=lz4                            # fast compression cho WAL
```

**Kubernetes CronJob — Daily Full Backup:**

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pgbackrest-full-backup
  namespace: blog-platform
spec:
  schedule: "0 2 * * *"                      # 02:00 UTC daily
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: pgbackrest
            image: pgbackrest/pgbackrest:latest
            command:
            - pgbackrest
            - --stanza=blog-db
            - --type=full
            - backup
            envFrom:
            - secretRef:
                name: pgbackrest-secrets
          restartPolicy: OnFailure
  successfulJobsHistoryLimit: 7
  failedJobsHistoryLimit: 3
```

**Kubernetes CronJob — 6-Hour Incremental Backup:**

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pgbackrest-incr-backup
  namespace: blog-platform
spec:
  schedule: "0 */6 * * *"                    # every 6 hours
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: pgbackrest
            image: pgbackrest/pgbackrest:latest
            command:
            - pgbackrest
            - --stanza=blog-db
            - --type=incr
            - backup
            envFrom:
            - secretRef:
                name: pgbackrest-secrets
          restartPolicy: OnFailure
```

---

## 7.2 Redis Persistence & Backup

**Hybrid Persistence (RDB + AOF) — khuyến nghị cho production:**

```conf
# === redis.conf ===

# RDB Snapshots
save 3600 1                                  # snapshot nếu ≥1 key thay đổi trong 1 giờ
save 300 100                                 # snapshot nếu ≥100 keys thay đổi trong 5 phút
save 60 10000                                # snapshot nếu ≥10000 keys thay đổi trong 1 phút
dbfilename dump.rdb
dir /data

# AOF (Append Only File)
appendonly yes
appendfilename "appendonly.aof"
appendfsync everysec                         # fsync mỗi giây — balance giữa durability và performance
auto-aof-rewrite-percentage 100
auto-aof-rewrite-min-size 64mb

# Hybrid (Redis 8)
aof-use-rdb-preamble yes                     # AOF file bắt đầu bằng RDB snapshot → load nhanh hơn
```

**Kubernetes StatefulSet requirements:**

- PersistentVolumeClaim với `ReadWriteOnce` access mode
- Volume mount tại `/data`
- Resource limits: đảm bảo đủ memory cho dataset + `maxmemory-policy allkeys-lru`

**Backup CronJob — RDB Snapshot Export:**

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: redis-backup
  namespace: blog-platform
spec:
  schedule: "30 2 * * *"                     # 02:30 UTC daily (sau PostgreSQL backup)
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: redis-backup
            image: redis:8-alpine
            command:
            - sh
            - -c
            - |
              redis-cli -h redis-master BGSAVE
              sleep 10
              mc cp /data/dump.rdb minio/blog-platform-backups/redis/dump-$(date +%Y%m%d).rdb
            volumeMounts:
            - name: redis-data
              mountPath: /data
          restartPolicy: OnFailure
```

---

## 7.3 MinIO Object Storage Backup

MinIO tự hỗ trợ replication và versioning. Cấu hình cho production:

```bash
# Enable versioning cho media bucket
mc version enable minio/blog-media

# Site replication (nếu có 2+ MinIO clusters)
mc admin replicate add minio-primary minio-standby

# Lifecycle policy — xóa old versions sau 90 ngày
mc ilm rule add --expire-days 90 --noncurrent-expire-days 30 minio/blog-media
```

**Backup strategy:**

- **Primary:** MinIO versioning — tự động giữ old versions
- **Secondary:** `mc mirror` daily → external S3 / backup MinIO instance
- **Retention:** 90 ngày cho current versions, 30 ngày cho noncurrent

---

## 7.4 Recovery Procedures

#### Scenario 1: Point-in-Time Recovery (PITR) — PostgreSQL

```bash
# 1. Stop PostgreSQL
sudo systemctl stop postgresql

# 2. Restore to specific point in time
pgbackrest --stanza=blog-db \
    --type=time \
    --target="2026-03-12 14:30:00+07" \
    restore

# 3. Start PostgreSQL — sẽ replay WAL đến target time
sudo systemctl start postgresql

# 4. Verify
psql -c "SELECT count(*) FROM posts WHERE status = 'Published';"
```

#### Scenario 2: Full Cluster Recovery

```bash
# 1. Provision new PostgreSQL instance

# 2. Restore latest full backup
pgbackrest --stanza=blog-db \
    --type=default \
    restore

# 3. WAL replay sẽ tự động apply đến latest available WAL

# 4. Update connection strings trong K8s secrets
kubectl -n blog-platform create secret generic db-connection \
    --from-literal=ConnectionString="Host=new-pg;..." \
    --dry-run=client -o yaml | kubectl apply -f -

# 5. Rolling restart API pods
kubectl -n blog-platform rollout restart deployment/blog-api
```

#### Scenario 3: Redis Recovery

```bash
# 1. Redis tự động recovery từ AOF khi pod restart (StatefulSet + PVC)

# 2. Manual recovery từ RDB backup:
kubectl cp blog-platform-backups/redis/dump-20260312.rdb \
    redis-0:/data/dump.rdb -n blog-platform
kubectl -n blog-platform delete pod redis-0    # StatefulSet sẽ recreate với data
```

---

## 7.5 RTO / RPO Targets

| Component | RPO (max data loss) | RTO (max downtime) | Strategy |
|---|---|---|---|
| **PostgreSQL** | < 1 phút | < 30 phút | WAL archiving + pgBackRest PITR |
| **Redis (cache)** | Không áp dụng | < 5 phút | Cache rebuild từ DB (cache-aside pattern) |
| **Redis (session)** | < 1 phút | < 5 phút | AOF everysec + StatefulSet PVC |
| **MinIO (media)** | 0 (versioned) | < 15 phút | Versioning + cross-site replication |
| **Application (API)** | N/A | < 2 phút | K8s auto-restart, min 2 replicas |
| **Application (Web)** | N/A | < 2 phút | K8s auto-restart, min 2 replicas, CDN cache |

**Backup Verification — Monthly DR Drill:**

```yaml
# Kubernetes CronJob — Monthly restore test
apiVersion: batch/v1
kind: CronJob
metadata:
  name: dr-restore-test
spec:
  schedule: "0 4 1 * *"                      # 1st of each month, 04:00 UTC
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: dr-test
            image: blog-platform/dr-test:latest
            command:
            - /scripts/dr-restore-test.sh
            # Script thực hiện:
            # 1. Restore latest backup vào temporary PostgreSQL instance
            # 2. Run smoke tests (SELECT counts, verify data integrity)
            # 3. Report kết quả → Slack/email
            # 4. Cleanup temporary instance
          restartPolicy: Never
```

---
