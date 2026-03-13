# incident-response

Follow the SLI/SLO incident response workflow with severity-based escalation, investigation steps, and post-mortem creation.

## Arguments

- `severity` (required) — `P1` (critical), `P2` (warning), `P3` (info)
- `alert` (optional) — Alert name that triggered the incident (e.g., `APIHighErrorRate`, `PostgreSQLDown`)
- `action` (optional) — `respond`, `investigate`, `mitigate`, `postmortem` (default: `respond`)

## Instructions

You are following the incident response workflow for the blog-platform. This skill guides you through the structured response process based on severity.

### Severity Definitions

| Level | Definition | Example | Response Time |
|---|---|---|---|
| **P1 — Critical** | Service fully down OR data loss risk | API 100% 5xx, DB corruption | < 15 min |
| **P2 — Major** | Feature significantly impacted | Publish broken for all, login fails for subset | < 1 hour |
| **P3 — Minor** | Feature/cosmetic issue | Analytics wrong, dark mode CSS broken | Next business day |

### Alert Reference

**P1 — Critical Alerts:**
| Alert | Condition | Action |
|---|---|---|
| `APIHighErrorRate` | Error rate > 1% for 5 min | Check API logs, DB connectivity |
| `PostgreSQLDown` | DB unavailable | Check StatefulSet, PVC, connections |
| `RedisDown` | Cache unavailable | Check pod, memory, AOF |
| `PodCrashLooping` | Service restart loop | Check container logs, OOM killer |
| `DiskSpaceCritical` | < 10% free space | Expand PVC, clean old data |

**P2 — Warning Alerts:**
| Alert | Condition | Action |
|---|---|---|
| `APIHighLatency` | P95 > 200ms for 10 min | Check slow queries, cache hit ratio |
| `PostgreSQLConnectionsHigh` | > 80% of max | Check connection pool, idle connections |
| `RedisMemoryHigh` | > 85% utilized | Check key eviction, TTL policies |
| `PostgreSQLReplicationLag` | > 30 sec | Check WAL sender, network |
| `RedisCacheHitRatioLow` | < 80% for 30 min | Check cache invalidation patterns |
| `HPAMaxedOut` | At max replicas for 30 min | Increase max replicas or optimize |
| `SSLCertExpiringSoon` | < 14 days to expiry | Renew certificate |

### Step 1 — Acknowledge & Communicate

```bash
# P1: Immediately create incident channel
# Post to #incidents Slack channel

## Incident Template
🔴 **INCIDENT — P{severity}**
**Alert:** {alert_name}
**Time:** {timestamp}
**Impact:** {user-facing impact description}
**On-call:** {your name}
**Status:** Investigating
```

### Step 2 — Investigate

Follow this investigation checklist based on the alert:

#### API Issues
```bash
# Check pod status
kubectl get pods -n blog-{env} -l app=blog-api

# Check recent logs
kubectl logs -n blog-{env} deployment/blog-api --tail=100 --since=5m

# Check error rate
kubectl top pods -n blog-{env}

# Check if DB is reachable from API pod
kubectl exec -n blog-{env} deployment/blog-api -- pg_isready -h postgres

# Check Redis connectivity
kubectl exec -n blog-{env} deployment/blog-api -- redis-cli -h redis ping
```

#### Database Issues
```bash
# Check PostgreSQL pod
kubectl get pods -n blog-{env} -l app=postgres

# Check PostgreSQL logs
kubectl logs -n blog-{env} statefulset/postgres --tail=100

# Check connections
psql -c "SELECT count(*) FROM pg_stat_activity WHERE state = 'active';"

# Check long-running queries
psql -c "SELECT pid, now() - pg_stat_activity.query_start AS duration, query
         FROM pg_stat_activity
         WHERE state != 'idle' AND query_start < now() - interval '30 seconds'
         ORDER BY duration DESC;"

# Check disk usage
kubectl exec -n blog-{env} postgres-0 -- df -h /var/lib/postgresql/data
```

#### Redis Issues
```bash
# Check Redis pod
kubectl get pods -n blog-{env} -l app=redis

# Check memory
kubectl exec -n blog-{env} redis-0 -- redis-cli INFO memory

# Check hit/miss ratio
kubectl exec -n blog-{env} redis-0 -- redis-cli INFO stats | grep keyspace
```

### Step 3 — Mitigate

**Common mitigations by alert:**

| Issue | Mitigation |
|---|---|
| API crash looping | Rollback to previous image: `kubectl rollout undo deployment/blog-api` |
| DB connections exhausted | Kill idle connections: `SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state = 'idle' AND query_start < now() - interval '10 min';` |
| Redis OOM | Flush non-critical caches: `redis-cli FLUSHDB` (cache-aside rebuilds automatically) |
| Disk full | Expand PVC or clean WAL: `pgbackrest --stanza=blog-db expire` |
| High latency | Scale up: `kubectl scale deployment/blog-api --replicas=6` |
| Bad deploy | Rollback: `kubectl rollout undo deployment/blog-api` |

### Step 4 — Resolve & Communicate

```bash
## Resolution Update
✅ **RESOLVED — P{severity}**
**Alert:** {alert_name}
**Duration:** {start_time} — {end_time} ({duration})
**Root Cause:** {brief description}
**Mitigation:** {what was done}
**Impact:** {user impact summary}
**Follow-up:** Post-mortem scheduled for {date}
```

### Step 5 — Post-Mortem (P1 and P2 only)

Create a post-mortem document:

```markdown
## Incident Report — INC-{YYYY}-{NNN}

**Date:** {YYYY-MM-DD}
**Duration:** {X hours Y minutes}
**Severity:** P{N}
**Impact:** {Number of users affected, features impacted}

### Timeline
- HH:MM — Alert fired: {alert name}
- HH:MM — On-call acknowledged
- HH:MM — Investigation started
- HH:MM — Root cause identified: {brief}
- HH:MM — Mitigation applied: {what}
- HH:MM — Monitoring confirmed resolution
- HH:MM — Incident resolved

### Root Cause
{Detailed technical explanation of what caused the incident}

### What Went Well
- {Good thing 1}
- {Good thing 2}

### What Went Wrong
- {Problem 1}
- {Problem 2}

### Action Items
| Action | Owner | Priority | Due |
|--------|-------|----------|-----|
| {Preventive action 1} | {Name} | High | {Date} |
| {Monitoring improvement} | {Name} | Medium | {Date} |

### Lessons Learned
- {Lesson 1}
- {Lesson 2}
```

### SLO Reference

| Service | SLO | Error Budget |
|---|---|---|
| blog-api | 99.9% availability | 43.2 min/month |
| blog-api | P95 ≤ 200ms | 5% can exceed |
| blog-api | P99 ≤ 500ms | 1% can exceed |
| blog-web | 99.95% availability | 21.6 min/month |
| PostgreSQL | 99.95% availability | 21.6 min/month |
| Redis | 99.9% availability | 43.2 min/month |

### Escalation Path

```
Alert → L1: On-call engineer (15 min for P1)
  → L2: Engineering Manager (30 min)
    → L3: CTO/Head of Eng (1 hour, P1 only)
```

### Key Rules

1. **Blameless** — Focus on systems and processes, not individuals
2. **Communicate early** — Update stakeholders even if investigating
3. **Mitigate first, root-cause later** — Restore service before deep investigation
4. **Document everything** — Timeline with timestamps for post-mortem
5. **Action items are mandatory** — Every P1/P2 incident produces action items with owners and due dates
