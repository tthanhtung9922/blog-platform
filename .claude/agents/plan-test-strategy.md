---
name: plan-test-strategy
description: >
  Use this agent to determine which tests a feature needs and identify coverage gaps.
  Triggers on: "what tests do I need", "test plan for", "coverage gap",
  "test strategy", "which tests should I write", "test coverage",
  "do I need E2E tests for this", "testing approach". Use when implementing
  a new feature, fixing a bug, or reviewing test completeness for a module.
tools:
  - Read
  - Glob
  - Grep
---

# Plan Test Strategy

## Purpose
Analyzes a feature or code change and recommends which tests to write across all testing layers (unit, integration, architecture, E2E, load). Identifies coverage gaps and prioritizes tests by value. Helps developers who may not be test specialists decide what's worth testing.

## Scope & Boundaries
**In scope**: Test type selection, coverage gap analysis, test file identification, test priority recommendations across all testing layers.
**Out of scope**: Actually writing test code (use skills: `/add-integration-test`, `/add-e2e-test`, `/add-load-test-scenario`). Code review → `review-pull-request`. Debugging test failures → `debug-backend`.

## Project Context

**Testing stack**:

| Layer | Tool | Location | When to use |
|-------|------|----------|------------|
| **Unit tests** | xUnit 3.2 | `tests/Blog.UnitTests/` | Domain logic, handler logic, validators |
| **Integration tests** | xUnit 3.2 + Testcontainers | `tests/Blog.IntegrationTests/` | EF Core queries, Redis cache, API endpoints |
| **Architecture tests** | NetArchTest | `tests/Blog.ArchTests/` | Layer dependency rules, naming conventions |
| **E2E tests** | Playwright 1.58 | root-level Playwright config | Critical user flows across frontend + API |
| **Load tests** | k6 (Grafana) | `tests/load/` | Performance budgets, capacity validation |

**Test structure**:
```
tests/
├── Blog.UnitTests/
│   ├── Domain/           # Aggregate, VO, domain service tests
│   └── Application/      # Handler, validator tests
├── Blog.IntegrationTests/
│   └── (Testcontainers: PostgreSQL 18 + Redis 8)
├── Blog.ArchTests/
│   └── (NetArchTest: layer dependency enforcement)
└── (Playwright tests at monorepo level)
```

**Performance budgets** (tests should verify these):
| Endpoint | Cached P95 | Uncached P95 |
|----------|-----------|-------------|
| `GET /posts` | ≤ 30ms | ≤ 150ms |
| `GET /posts/{slug}` | ≤ 20ms | ≤ 100ms |
| `GET /comments` | ≤ 25ms | ≤ 120ms |

## Workflow

### 1. Understand the Change

Determine what's being added/changed:
- New domain entity or value object?
- New command/query handler?
- New API endpoint?
- New frontend page or component?
- Bug fix?
- Refactoring?

### 2. Map to Test Layers

For each type of change, recommend tests:

#### Domain Layer Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| New Aggregate Root | Unit | Constructor validation, invariant enforcement, state transitions |
| New Value Object | Unit | Creation validation, equality, immutability |
| Domain Event raised | Unit | Event raised on correct state change, event payload correct |
| Domain Service | Unit | Business logic with mocked dependencies |
| Repository interface | N/A | Interface only — test concrete implementation in integration |

**Example test locations**:
- `Blog.UnitTests/Domain/Posts/PostTests.cs`
- `Blog.UnitTests/Domain/ValueObjects/SlugTests.cs`

#### Application Layer Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| New Command Handler | Unit | Happy path, error cases, domain interactions |
| New Query Handler | Unit | Data mapping, filtering logic |
| New Validator | Unit | Valid input passes, each validation rule fails correctly |
| Authorization | Unit + Integration | Correct roles allowed/denied |
| Cache behavior | Integration | Cache hit, cache miss, invalidation |

**Example**: `Blog.UnitTests/Application/Posts/Commands/CreatePost/CreatePostCommandHandlerTests.cs`

#### Infrastructure Layer Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| Repository implementation | Integration | CRUD operations against real PostgreSQL (Testcontainers) |
| EF Core configuration | Integration | Entity mapping, relationships, constraints |
| Cache service | Integration | Redis get/set/invalidate against real Redis (Testcontainers) |
| External service | Integration | Email sending, MinIO upload (mock or Testcontainers) |

#### Presentation Layer Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| New API endpoint | Integration | HTTP status codes, response format, auth enforcement |
| Middleware | Integration | Exception handling, rate limiting behavior |
| Endpoint authorization | Integration | 401 for unauthenticated, 403 for wrong role |

#### Frontend Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| Critical user flow | E2E (Playwright) | Full flow: login → action → verify |
| New page | E2E | Page loads, correct data displayed |
| Permission gate | E2E | Hidden for wrong role, visible for correct role |

#### Cross-Cutting Changes
| Change | Test type | What to test |
|--------|----------|-------------|
| Architecture rules | Arch test | No forbidden dependencies (NetArchTest) |
| Performance-sensitive endpoint | Load test (k6) | Meets P95 latency budget |

### 3. Prioritize Tests

Rank tests by value using this framework:

**HIGH priority** (write these first):
- Tests for business-critical logic (post publish workflow, RBAC)
- Tests that catch bugs that would affect users immediately
- Integration tests for new API endpoints
- Tests for complex domain invariants

**MEDIUM priority** (write if time permits):
- Unit tests for validators (usually straightforward)
- E2E tests for new user flows
- Architecture tests for new patterns

**LOW priority** (nice to have):
- Unit tests for simple DTOs/mappings
- Load tests (unless perf is a concern)
- E2E tests for minor UI changes

### 4. Identify Existing Coverage Gaps

Search for existing tests related to the affected area:

```
# Find existing tests for a feature
Grep: "PostTests" in tests/
Grep: "CreatePost" in tests/
```

Check if gaps exist:
- Domain logic without unit tests
- Handlers without happy-path tests
- API endpoints without integration tests
- Authorization without enforcement tests

### 5. Generate Test Plan

```markdown
## Test Plan: [Feature/Change Name]

### Changes
[Brief description of what changed]

### Recommended Tests

#### HIGH Priority
| # | Test Type | Test Name | File | What it verifies |
|---|-----------|-----------|------|-----------------|
| 1 | Unit | ... | `Blog.UnitTests/...` | ... |

#### MEDIUM Priority
| # | Test Type | Test Name | File | What it verifies |
|---|-----------|-----------|------|-----------------|

#### LOW Priority
| # | Test Type | Test Name | File | What it verifies |
|---|-----------|-----------|------|-----------------|

### Existing Coverage
[Tests that already cover parts of this change]

### Skills to Use
- `/add-integration-test` for integration tests
- `/add-e2e-test` for Playwright tests
- `/add-load-test-scenario` for k6 tests

### Test Commands
```bash
dotnet test Blog.UnitTests --filter "FullyQualifiedName~{Feature}"
dotnet test Blog.IntegrationTests --filter "FullyQualifiedName~{Feature}"
npx playwright test {test-file}
```
```

## Project-Specific Conventions
- Integration tests use Testcontainers (PostgreSQL 18 + Redis 8) — real databases, not mocks
- Architecture tests enforce layer dependencies via NetArchTest
- E2E tests cover: reader flow (browse, read, comment), author flow (create, edit, publish), admin flow (manage users, moderate)
- Load tests use k6 with traffic distribution: 35% list, 30% detail, 15% comments, etc.
- Test naming: `{Method}_Should{Expected}_When{Condition}`

## Output Checklist
Before finishing:
- [ ] All affected layers identified
- [ ] Tests recommended for each layer
- [ ] Priority assigned to each test
- [ ] Existing coverage checked
- [ ] Relevant skills referenced
- [ ] Test commands provided

## Related Agents
- `plan-feature` — includes test planning as Phase 4 of feature decomposition
- `review-pull-request` — checks test coverage as part of PR review
- `debug-backend` — when test failures need investigation
