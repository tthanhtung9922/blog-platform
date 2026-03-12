# Observability & Incident Response

## 8.1 SLI / SLO / SLA Definitions

### Service Level Indicators (SLIs)

| SLI | Mô tả | Phương pháp đo | Source |
|---|---|---|---|
| **Availability** | % requests trả về non-5xx response | `1 - (count(status >= 500) / count(total))` | Nginx Ingress logs |
| **Latency (P95)** | 95th percentile response time | `histogram_quantile(0.95, http_request_duration_seconds)` | Prometheus |
| **Latency (P99)** | 99th percentile response time | `histogram_quantile(0.99, http_request_duration_seconds)` | Prometheus |
| **Error Rate** | % requests trả về 5xx status | `rate(http_requests_total{status=~"5.."}[5m])` | Prometheus |
| **Throughput** | Requests per second | `rate(http_requests_total[5m])` | Prometheus |
| **Saturation** | CPU / Memory utilization | `container_cpu_usage_seconds_total` | cAdvisor |

### Service Level Objectives (SLOs)

| Service | SLO | Window | Error Budget |
|---|---|---|---|
| **blog-api** (REST API) | Availability ≥ 99.9% | 30 ngày | 43.2 phút/tháng |
| **blog-api** (REST API) | P95 Latency ≤ 200ms | 30 ngày | 5% requests có thể > 200ms |
| **blog-api** (REST API) | P99 Latency ≤ 500ms | 30 ngày | 1% requests có thể > 500ms |
| **blog-web** (SSG/ISR) | Availability ≥ 99.95% | 30 ngày | 21.6 phút/tháng |
| **blog-web** (SSG/ISR) | LCP ≤ 2.5s | 30 ngày (Chrome UX Report) | 25% pages có thể > 2.5s |
| **blog-admin** (CMS) | Availability ≥ 99.5% | 30 ngày | 3.6 giờ/tháng |
| **blog-admin** (CMS) | P95 Latency ≤ 500ms | 30 ngày | 5% requests có thể > 500ms |
| **PostgreSQL** | Availability ≥ 99.95% | 30 ngày | 21.6 phút/tháng |
| **Redis** | Availability ≥ 99.9% | 30 ngày | 43.2 phút/tháng |

### SLA (Customer-Facing — Phase 4 Enterprise)

| Tier | Uptime Guarantee | Support Response | Penalty |
|---|---|---|---|
| **Free** | Best effort | Community (Discord) | Không |
| **Pro** | 99.5% | 24h email | Credit 10% |
| **Enterprise** | 99.9% | 4h (P1), 8h (P2) | Credit 25% |

---

## 8.2 Monitoring Stack

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  blog-api    │    │  blog-web    │    │  blog-admin  │    │  PostgreSQL  │
│  (/metrics)  │    │  (OTEL SDK)  │    │  (OTEL SDK)  │    │  (pg_stat)   │
└──────┬───────┘    └──────┬───────┘    └──────┬───────┘    └──────┬───────┘
       │                   │                   │                   │
       ▼                   ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     Prometheus (Metrics)                                │
│  - Scrape interval: 15s                                                │
│  - Retention: 15 ngày local, remote-write → Thanos/Mimir (long-term) │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
     ┌────────────────┐ ┌────────────┐ ┌──────────────────┐
     │  Alertmanager  │ │  Grafana   │ │  Loki (Logs)     │
     │  → Slack       │ │  Dashboards│ │  ← Promtail      │
     │  → PagerDuty   │ │            │ │  Retention: 30d  │
     │  → Email       │ │            │ └──────────────────┘
     └────────────────┘ │            │
                        │            │ ┌──────────────────┐
                        │            │ │  Tempo (Traces)  │
                        │            │ │  ← OTLP          │
                        │            │ │  Retention: 7d   │
                        └────────────┘ └──────────────────┘
```

**Exporters & Data Sources:**

| Component | Exporter / Source | Port | Metrics |
|---|---|---|---|
| **blog-api** | Built-in `/metrics` (ASP.NET) | 8080 | HTTP requests, latency histogram, active connections |
| **PostgreSQL** | `postgres_exporter` | 9187 | Connections, locks, replication lag, table stats, query duration |
| **Redis** | `redis_exporter` | 9121 | Memory usage, hit/miss ratio, connected clients, keyspace |
| **Nginx Ingress** | Built-in Prometheus metrics | 10254 | Request rate, latency by backend, 4xx/5xx rate |
| **Node** | `node_exporter` | 9100 | CPU, memory, disk, network |
| **Kubernetes** | `kube-state-metrics` | 8080 | Pod status, deployment replicas, HPA metrics |
| **MinIO** | Built-in `/minio/v2/metrics` | 9000 | Bucket size, request rate, errors |

---

## 8.3 Alerting Rules

**Severity Levels:**

| Level | Meaning | Response Time | Channel |
|---|---|---|---|
| **P1 — Critical** | Service down / data loss risk | < 15 phút | PagerDuty + Slack #incidents + Phone call |
| **P2 — Warning** | Degraded performance / approaching limits | < 1 giờ | Slack #alerts |
| **P3 — Info** | Non-urgent, awareness only | Next business day | Slack #monitoring |

**Prometheus Alert Rules (`alerting-rules.yaml`):**

```yaml
groups:
  # ============================================================
  # P1 — CRITICAL: Requires immediate action
  # ============================================================
  - name: critical
    rules:
      # API availability dropped below SLO
      - alert: APIHighErrorRate
        expr: |
          (
            sum(rate(http_requests_total{job="blog-api", status=~"5.."}[5m]))
            /
            sum(rate(http_requests_total{job="blog-api"}[5m]))
          ) > 0.01
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "API error rate > 1% for 5 minutes"
          description: "Current error rate: {{ $value | humanizePercentage }}"
          runbook: "https://wiki.internal/runbook/api-high-error-rate"

      # PostgreSQL down
      - alert: PostgreSQLDown
        expr: pg_up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL instance is down"
          runbook: "https://wiki.internal/runbook/postgresql-down"

      # Redis down
      - alert: RedisDown
        expr: redis_up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Redis instance is down"
          runbook: "https://wiki.internal/runbook/redis-down"

      # Pod crash loop
      - alert: PodCrashLooping
        expr: rate(kube_pod_container_status_restarts_total{namespace="blog-platform"}[15m]) > 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Pod {{ $labels.pod }} is crash-looping"

      # Disk almost full (PostgreSQL data)
      - alert: DiskSpaceCritical
        expr: |
          (node_filesystem_avail_bytes{mountpoint="/var/lib/postgresql"}
           / node_filesystem_size_bytes{mountpoint="/var/lib/postgresql"}) < 0.1
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL disk < 10% free"

  # ============================================================
  # P2 — WARNING: Needs attention soon
  # ============================================================
  - name: warning
    rules:
      # API latency above SLO
      - alert: APIHighLatency
        expr: |
          histogram_quantile(0.95,
            sum(rate(http_request_duration_seconds_bucket{job="blog-api"}[5m])) by (le)
          ) > 0.2
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "API P95 latency > 200ms for 10 minutes"
          description: "Current P95: {{ $value | humanizeDuration }}"

      # PostgreSQL connection pool near limit
      - alert: PostgreSQLConnectionsHigh
        expr: |
          sum(pg_stat_activity_count{datname="blog_db"})
          / pg_settings_max_connections > 0.8
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "PostgreSQL connections at {{ $value | humanizePercentage }} of max"

      # Redis memory high
      - alert: RedisMemoryHigh
        expr: redis_memory_used_bytes / redis_memory_max_bytes > 0.85
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Redis memory usage > 85%"

      # PostgreSQL replication lag
      - alert: PostgreSQLReplicationLag
        expr: pg_replication_lag > 30
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "PostgreSQL replication lag > 30 seconds"

      # Redis cache hit ratio low
      - alert: RedisCacheHitRatioLow
        expr: |
          redis_keyspace_hits_total
          / (redis_keyspace_hits_total + redis_keyspace_misses_total) < 0.8
        for: 30m
        labels:
          severity: warning
        annotations:
          summary: "Redis cache hit ratio < 80% — review cache strategy"

      # HPA at max replicas
      - alert: HPAMaxedOut
        expr: |
          kube_horizontalpodautoscaler_status_current_replicas
          == kube_horizontalpodautoscaler_spec_max_replicas
        for: 30m
        labels:
          severity: warning
        annotations:
          summary: "HPA {{ $labels.horizontalpodautoscaler }} at max replicas"

      # Disk filling up
      - alert: DiskSpaceWarning
        expr: |
          (node_filesystem_avail_bytes{mountpoint="/var/lib/postgresql"}
           / node_filesystem_size_bytes{mountpoint="/var/lib/postgresql"}) < 0.2
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "PostgreSQL disk < 20% free"

      # SSL certificate expiring
      - alert: SSLCertExpiringSoon
        expr: (probe_ssl_earliest_cert_expiry - time()) / 86400 < 14
        for: 1h
        labels:
          severity: warning
        annotations:
          summary: "SSL certificate expires in {{ $value }} days"

  # ============================================================
  # P3 — INFO: Awareness only
  # ============================================================
  - name: info
    rules:
      # Backup failed
      - alert: BackupJobFailed
        expr: kube_job_status_failed{namespace="blog-platform", job_name=~"pgbackrest.*|redis-backup.*"} > 0
        for: 1m
        labels:
          severity: warning              # backup failure escalate to P2
        annotations:
          summary: "Backup job {{ $labels.job_name }} failed"
          runbook: "https://wiki.internal/runbook/backup-failure"

      # Error budget burn rate
      - alert: ErrorBudgetBurnRate
        expr: |
          (
            1 - (
              sum(rate(http_requests_total{job="blog-api", status!~"5.."}[1h]))
              / sum(rate(http_requests_total{job="blog-api"}[1h]))
            )
          ) > (1 - 0.999) * 14.4
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Error budget burn rate is 14.4x — will exhaust monthly budget in 5 days"
```

---

## 8.4 Dashboards

**Grafana Dashboards (provisioned via ConfigMap):**

| Dashboard | Audience | Panels chính |
|---|---|---|
| **Platform Overview** | SRE / Engineering Manager | Availability SLO status, Error budget remaining, Request rate, Active users |
| **API Performance** | Backend Engineers | Latency heatmap (P50/P95/P99), Error rate by endpoint, Top slow queries, Request throughput |
| **PostgreSQL** | SRE / Backend | Connections active/idle, Query duration histogram, Table/Index bloat, WAL generation rate, Replication lag |
| **Redis** | SRE / Backend | Memory usage, Hit/Miss ratio, Connected clients, Key eviction rate, Command latency |
| **Kubernetes** | Platform Engineers | Pod status (Running/Pending/Failed), HPA current vs desired, Resource utilization (CPU/Memory), Node health |
| **Frontend Performance** | Frontend Engineers | Core Web Vitals (LCP, FID, CLS), TTFB by page, JS bundle size trends, Cache hit ratio (CDN) |
| **Business Metrics** | Product Manager | DAU/MAU, Posts published/day, Comments/day, Top posts by views, Author engagement |

**Dashboard as Code — Grafana provisioning path:**

```
deploy/k8s/base/
├── grafana-dashboards-configmap.yaml       # Dashboard JSON definitions
├── grafana-datasources-configmap.yaml      # Prometheus, Loki, Tempo sources
└── grafana-alerting-configmap.yaml         # Alert notification channels
```

---

## 8.5 On-Call & Incident Response

#### On-Call Rotation

| Phase | Schedule | Team |
|---|---|---|
| **Phase 1** (Launch) | Shared rotation — 1 week shifts | All Backend + Platform Engineers (5 people) |
| **Phase 2** (Growth) | Primary + Secondary — 1 week shifts | Dedicated SRE + Backend rotation |
| **Phase 3+** (Scale) | Follow-the-sun (nếu multi-timezone) | SRE team + Escalation to domain experts |

**Escalation Path:**

```
Alert fires
  → L1: On-call engineer (15 min response for P1)
    → L2: Engineering Manager + relevant domain lead (30 min)
      → L3: CTO / Head of Engineering (1 hour, P1 only)
```

#### Incident Response Process

```
┌─────────┐     ┌─────────────┐     ┌────────────┐     ┌──────────┐     ┌────────────┐
│ Detect  │────►│  Triage     │────►│  Mitigate  │────►│  Resolve │────►│ Post-mortem│
│         │     │             │     │            │     │          │     │            │
│ Alert   │     │ Assign P1-3 │     │ Rollback / │     │ Root     │     │ Blameless  │
│ fires   │     │ Notify team │     │ Scale /    │     │ cause    │     │ 48h after  │
│         │     │ Slack #inc  │     │ Feature    │     │ fix      │     │ incident   │
│         │     │             │     │ flag off   │     │          │     │            │
└─────────┘     └─────────────┘     └────────────┘     └──────────┘     └────────────┘
```

**Incident Severity Definitions:**

| Severity | Definition | Example |
|---|---|---|
| **P1 — Critical** | Service fully down hoặc data loss | API trả 100% 5xx, DB corruption |
| **P2 — Major** | Feature chính bị ảnh hưởng | Không thể publish bài, Login fail cho subset users |
| **P3 — Minor** | Feature phụ / cosmetic | Analytics dashboard hiển thị sai, Dark mode lỗi CSS |

**Post-mortem Template (Blameless):**

```markdown
## Incident Report — [INC-YYYY-NNN]

**Date:** YYYY-MM-DD
**Duration:** HH:MM
**Severity:** P1/P2/P3
**Impact:** [Mô tả impact đến users]
**On-call:** [Tên]

### Timeline
- HH:MM — Alert fired: [alert name]
- HH:MM — On-call acknowledged
- HH:MM — Root cause identified
- HH:MM — Mitigation applied
- HH:MM — Full resolution

### Root Cause
[Mô tả nguyên nhân gốc]

### What Went Well
- [...]

### What Went Wrong
- [...]

### Action Items
| Action | Owner | Priority | Due Date |
|--------|-------|----------|----------|
| [...] | [...] | P1/P2/P3 | YYYY-MM-DD |

### Lessons Learned
[...]
```

**Tooling:**

| Purpose | Tool | Ghi chú |
|---|---|---|
| Alerting | Alertmanager | Route to Slack, PagerDuty, Email |
| On-call schedule | PagerDuty / Grafana OnCall (OSS) | **Grafana OnCall** là open source alternative cho PagerDuty |
| Incident tracking | GitHub Issues (label: `incident`) | Hoặc Rootly / FireHydrant nếu cần advanced |
| Status page | Upptime (OSS) / Cachet (OSS) | Public status page cho users |
| Communication | Slack #incidents channel | Dedicated channel, auto-created per incident |

---
