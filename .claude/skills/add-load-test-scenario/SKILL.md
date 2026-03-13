# add-load-test-scenario

Write a k6 load test scenario with proper traffic distribution, thresholds, and pass criteria following the project's performance budget.

## Arguments

- `scenario` (required) — Test type: `smoke`, `load`, `stress`, `spike`, `soak`, `breakpoint`
- `endpoints` (optional) — Comma-separated endpoint patterns to test (defaults to full traffic distribution)

## Instructions

You are writing k6 load tests for the blog-platform API. Tests verify that the API meets performance budgets under various traffic conditions.

### File Structure

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
│   ├── auth.js        ← Login + token management
│   └── data.js        ← Test data generators
├── thresholds.json
└── k6.config.js
```

### Scenario Definitions

| Scenario | Users | Duration | Pass Criteria |
|---|---|---|---|
| Smoke | 5 | 1 min | 0 errors, P95 < 500ms |
| Load | 200 | 10 min | P95 < 200ms, Error < 0.1% |
| Stress | 500 | 10 min | P95 < 500ms, Error < 1% |
| Spike | 0→1000→0 | 5 min | Recovery < 30s, no crash |
| Soak | 200 | 2 hours | No memory > 20% growth, stable latency |
| Breakpoint | 10→∞ | Until break | Identify max capacity |

### Traffic Distribution

| Endpoint | Weight | Cached |
|---|---|---|
| `GET /posts` (list) | 35% | Yes (5 min) |
| `GET /posts/{slug}` (detail) | 30% | Yes (1 hour) |
| `GET /posts/{id}/comments` | 15% | Yes (2 min) |
| `POST /auth/login` | 5% | No |
| `POST /posts/{id}/like` | 5% | No |
| `POST /posts/{id}/comments` | 5% | No |
| `POST /posts` (create) | 3% | No |
| `PUT /posts/{id}` (update) | 2% | No |

### Load Test Example

```javascript
// tests/load/scenarios/load.js
import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { login } from '../helpers/auth.js';
import { randomSlug, randomComment } from '../helpers/data.js';

// Custom metrics
const errorRate = new Rate('errors');
const postListDuration = new Trend('post_list_duration', true);
const postDetailDuration = new Trend('post_detail_duration', true);
const commentListDuration = new Trend('comment_list_duration', true);

export const options = {
  stages: [
    { duration: '2m', target: 50 },      // ramp up
    { duration: '5m', target: 200 },     // sustained load
    { duration: '2m', target: 200 },     // hold
    { duration: '1m', target: 0 },       // ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<200', 'p(99)<500'],
    errors: ['rate<0.001'],              // < 0.1%
    post_list_duration: ['p(95)<150'],
    post_detail_duration: ['p(95)<100'],
    comment_list_duration: ['p(95)<120'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000/api/v1';

export function setup() {
  // Login to get auth token for write operations
  const token = login(BASE_URL, 'loadtest@blog.dev', 'TestPassword123!');
  return { token };
}

export default function (data) {
  const rand = Math.random();

  if (rand < 0.35) {
    // 35% — GET post list
    group('GET /posts', () => {
      const page = Math.ceil(Math.random() * 5);
      const res = http.get(`${BASE_URL}/posts?page=${page}&pageSize=10`);
      postListDuration.add(res.timings.duration);
      check(res, {
        'status 200': (r) => r.status === 200,
        'has items': (r) => JSON.parse(r.body).items.length > 0,
      });
      errorRate.add(res.status >= 400);
    });
  } else if (rand < 0.65) {
    // 30% — GET post detail
    group('GET /posts/{slug}', () => {
      const slug = randomSlug();
      const res = http.get(`${BASE_URL}/posts/${slug}`);
      postDetailDuration.add(res.timings.duration);
      check(res, {
        'status 200 or 404': (r) => r.status === 200 || r.status === 404,
      });
      errorRate.add(res.status >= 500);
    });
  } else if (rand < 0.80) {
    // 15% — GET comments
    group('GET /posts/{id}/comments', () => {
      const postId = randomSlug();
      const res = http.get(`${BASE_URL}/posts/${postId}/comments?page=1&pageSize=20`);
      commentListDuration.add(res.timings.duration);
      check(res, { 'status 200': (r) => r.status === 200 });
      errorRate.add(res.status >= 400);
    });
  } else if (rand < 0.85) {
    // 5% — POST login
    group('POST /auth/login', () => {
      const res = http.post(`${BASE_URL}/auth/login`, JSON.stringify({
        email: 'loadtest@blog.dev',
        password: 'TestPassword123!',
      }), { headers: { 'Content-Type': 'application/json' } });
      check(res, { 'login success': (r) => r.status === 200 });
      errorRate.add(res.status >= 400);
    });
  } else if (rand < 0.90) {
    // 5% — POST like
    group('POST /posts/{id}/like', () => {
      const postId = randomSlug();
      const res = http.post(`${BASE_URL}/posts/${postId}/like`, null, {
        headers: { Authorization: `Bearer ${data.token}` },
      });
      check(res, { 'status 200': (r) => r.status === 200 });
      errorRate.add(res.status >= 400);
    });
  } else {
    // 10% — Write operations (create, update, comment)
    group('Write operations', () => {
      // POST comment (5%)
      const res = http.post(`${BASE_URL}/posts/test-post/comments`,
        JSON.stringify({ content: randomComment() }),
        {
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${data.token}`,
          },
        }
      );
      check(res, { 'status 201': (r) => r.status === 201 });
      errorRate.add(res.status >= 400);
    });
  }

  sleep(Math.random() * 2 + 1);  // 1-3s think time
}
```

### Smoke Test

```javascript
// tests/load/scenarios/smoke.js
export const options = {
  vus: 5,
  duration: '1m',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};
```

### Stress Test

```javascript
// tests/load/scenarios/stress.js
export const options = {
  stages: [
    { duration: '2m', target: 100 },
    { duration: '3m', target: 300 },
    { duration: '3m', target: 500 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    errors: ['rate<0.01'],
  },
};
```

### Spike Test

```javascript
// tests/load/scenarios/spike.js
export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '30s', target: 1000 },  // Spike!
    { duration: '1m', target: 1000 },
    { duration: '30s', target: 50 },    // Recovery
    { duration: '1m30s', target: 50 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    errors: ['rate<0.05'],
  },
};
```

### Running Load Tests

```bash
# Smoke test (local)
k6 run tests/load/scenarios/smoke.js

# Load test against staging
k6 run -e BASE_URL=https://staging-api.blog-platform.dev/api/v1 tests/load/scenarios/load.js

# With Grafana Cloud k6 output
k6 run --out cloud tests/load/scenarios/load.js
```

### Performance Budget Reference

| Endpoint | Cached P95 | Uncached P95 |
|---|---|---|
| `GET /posts` | ≤ 30ms | ≤ 150ms |
| `GET /posts/{slug}` | ≤ 20ms | ≤ 100ms |
| `GET /posts/{id}/comments` | ≤ 25ms | ≤ 120ms |
| `POST /auth/login` | N/A | ≤ 300ms |
| `POST /posts` | N/A | ≤ 400ms |
| `POST /posts/{id}/like` | N/A | ≤ 100ms |
