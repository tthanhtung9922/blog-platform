# Load Testing Baseline

## 10.1 Capacity Targets by Phase

| Metric | Phase 1 (Launch) | Phase 2 (Growth) | Phase 3 (Scale) | Phase 4 (Enterprise) |
|---|---|---|---|---|
| **Concurrent Users** | 500 | 2,000 | 10,000 | 50,000 |
| **Requests/sec (sustained)** | 200 rps | 800 rps | 3,000 rps | 10,000 rps |
| **Requests/sec (peak)** | 500 rps | 2,000 rps | 8,000 rps | 25,000 rps |
| **API P95 Latency** | ≤ 200ms | ≤ 200ms | ≤ 150ms | ≤ 100ms |
| **API P99 Latency** | ≤ 500ms | ≤ 500ms | ≤ 300ms | ≤ 200ms |
| **Error Rate** | < 0.1% | < 0.1% | < 0.05% | < 0.01% |
| **Database Connections** | 50 | 100 | 200 (+ PgBouncer) | 500 (+ PgBouncer) |
| **Redis Memory** | 256 MB | 1 GB | 4 GB | 16 GB |
| **LCP (blog-web)** | ≤ 2.5s | ≤ 2.0s | ≤ 1.5s | ≤ 1.2s |

**Giả định traffic pattern (Phase 1):**

- **DAU:** ~2,000 users · **MAU:** ~15,000 users
- **Peak hours:** 8:00–11:00, 19:00–22:00 (UTC+7)
- **Read:Write ratio:** 95:5 (phần lớn là đọc bài, ít tạo/update)
- **Average session:** 3–5 page views · 2–3 phút/session

---

## 10.2 Load Test Scenarios

| Scenario | Mô tả | Virtual Users | Duration | Pass Criteria |
|---|---|---|---|---|
| **Smoke** | Sanity check — 1 VU per endpoint | 5 | 1 phút | 0 errors, P95 < 500ms |
| **Load** | Normal traffic simulation | 200 | 10 phút | P95 < 200ms, Error < 0.1% |
| **Stress** | Peak traffic (2x normal) | 500 | 10 phút | P95 < 500ms, Error < 1% |
| **Spike** | Sudden traffic burst (viral post) | 0 → 1000 → 0 | 5 phút | Recovery < 30s, no crash |
| **Soak** | Extended duration — memory leak detection | 200 | 2 giờ | No memory growth > 20%, stable latency |
| **Breakpoint** | Tìm giới hạn — tăng dần VU đến failure | 10 → ∞ | Until break | Xác định max capacity |

**Traffic Distribution (Read-heavy profile):**

| Endpoint | Weight | Cached |
|---|---|---|
| `GET /posts` (list) | 35% | Yes (5 min TTL) |
| `GET /posts/{slug}` (detail) | 30% | Yes (1 hour TTL) |
| `GET /posts/{id}/comments` | 15% | Yes (2 min TTL) |
| `POST /auth/login` | 5% | No |
| `POST /posts/{id}/like` | 5% | No |
| `POST /posts/{id}/comments` | 5% | No |
| `POST /posts` (create) | 3% | No |
| `PUT /posts/{id}` (update) | 2% | No |

---

## 10.3 k6 Test Configuration

**Tool:** [Grafana k6](https://k6.io/) (open source, MIT license)
**Location:** `tests/load/` (thêm vào monorepo)

```
tests/load/
├── scenarios/
│   ├── smoke.js
│   ├── load.js
│   ├── stress.js
│   ├── spike.js
│   ├── soak.js
│   └── breakpoint.js
├── helpers/
│   ├── auth.js                # Login + token helper
│   └── data.js                # Test data generators
├── thresholds.json            # Shared threshold definitions
└── k6.config.js               # Environment config
```

**Ví dụ — Load Test (`tests/load/scenarios/load.js`):**

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const postListDuration = new Trend('post_list_duration', true);
const postDetailDuration = new Trend('post_detail_duration', true);

export const options = {
  stages: [
    { duration: '2m', target: 50 },         // Ramp up
    { duration: '5m', target: 200 },         // Sustained load
    { duration: '2m', target: 200 },         // Hold
    { duration: '1m', target: 0 },           // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<200', 'p(99)<500'],  // ms
    errors: ['rate<0.001'],                          // < 0.1%
    post_list_duration: ['p(95)<150'],
    post_detail_duration: ['p(95)<100'],             // cached hit
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000/api/v1';

export default function () {
  // 35% — GET post list
  const listRes = http.get(`${BASE_URL}/posts?page=1&pageSize=10`);
  postListDuration.add(listRes.timings.duration);
  check(listRes, {
    'post list: status 200': (r) => r.status === 200,
    'post list: has items': (r) => JSON.parse(r.body).items.length > 0,
  });
  errorRate.add(listRes.status >= 400);

  sleep(1);

  // 30% — GET post detail
  const posts = JSON.parse(listRes.body).items;
  if (posts.length > 0) {
    const slug = posts[Math.floor(Math.random() * posts.length)].slug;
    const detailRes = http.get(`${BASE_URL}/posts/${slug}`);
    postDetailDuration.add(detailRes.timings.duration);
    check(detailRes, {
      'post detail: status 200': (r) => r.status === 200,
      'post detail: has bodyHtml': (r) => JSON.parse(r.body).bodyHtml !== '',
    });
    errorRate.add(detailRes.status >= 400);

    sleep(1);

    // 15% — GET comments
    const postId = posts[Math.floor(Math.random() * posts.length)].id;
    const commentsRes = http.get(`${BASE_URL}/posts/${postId}/comments`);
    check(commentsRes, {
      'comments: status 200': (r) => r.status === 200,
    });
    errorRate.add(commentsRes.status >= 400);
  }

  sleep(Math.random() * 3 + 1);             // Think time: 1–4 seconds
}
```

**CI Integration — GitHub Actions (`load-test.yml`):**

```yaml
name: Load Test (Staging)
on:
  workflow_dispatch:                          # Manual trigger
  schedule:
    - cron: '0 3 * * 1'                     # Weekly — Monday 03:00 UTC

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run k6 load test
        uses: grafana/k6-action@v0.3.1
        with:
          filename: tests/load/scenarios/load.js
        env:
          BASE_URL: https://staging-api.blog-platform.dev/api/v1

      - name: Upload results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: k6-results
          path: summary.json
```

---

## 10.4 Performance Budget

**API Endpoint Latency Budget (Phase 1 — cached/uncached):**

| Endpoint | Cached P95 | Uncached P95 | Cache TTL |
|---|---|---|---|
| `GET /posts` (list) | ≤ 30ms | ≤ 150ms | 5 min |
| `GET /posts/{slug}` (detail) | ≤ 20ms | ≤ 100ms | 1 hour |
| `GET /posts/{id}/comments` | ≤ 25ms | ≤ 120ms | 2 min |
| `POST /auth/login` | N/A | ≤ 300ms | N/A |
| `POST /posts` (create) | N/A | ≤ 400ms | N/A |
| `POST /posts/{id}/like` (toggle) | N/A | ≤ 100ms | N/A |
| `GET /users/{username}` | ≤ 20ms | ≤ 80ms | 30 min |

**Database Query Budget:**

| Query | Max Duration | Notes |
|---|---|---|
| Post list (paginated) | ≤ 50ms | Partial index on `status = 'Published'` |
| Post by slug | ≤ 10ms | Unique index lookup |
| Comments by post (paginated) | ≤ 30ms | Composite index `(post_id, created_at)` |
| Full-text search | ≤ 200ms | GIN index, `to_tsvector('vietnamese', ...)` |
| Like toggle | ≤ 20ms | Unique constraint check + insert/delete |

**Resource Limits (Kubernetes — Phase 1):**

| Service | CPU Request | CPU Limit | Memory Request | Memory Limit | Replicas |
|---|---|---|---|---|---|
| **blog-api** | 250m | 1000m | 256Mi | 512Mi | 2–4 (HPA) |
| **blog-web** | 100m | 500m | 128Mi | 256Mi | 2–3 (HPA) |
| **blog-admin** | 100m | 500m | 128Mi | 256Mi | 1–2 (HPA) |
| **PostgreSQL** | 500m | 2000m | 1Gi | 2Gi | 1 (StatefulSet) |
| **Redis** | 100m | 500m | 256Mi | 512Mi | 1 (StatefulSet) |

---
