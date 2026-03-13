---
description: >
  Apply these rules whenever writing, modifying, or reviewing test code. Triggers
  on: unit tests, integration tests, architecture tests, E2E tests, load tests,
  test data setup, mock/stub patterns, Testcontainers configuration.
---

# Testing Rules

## MUST

- **Integration tests use Testcontainers** for real PostgreSQL and Redis instances — never mock the database for integration tests.
  ```csharp
  // Blog.IntegrationTests — real containers
  var postgres = new PostgreSqlBuilder()
      .WithImage("postgres:18")
      .Build();
  ```
- **Architecture tests (`Blog.ArchTests`) use NetArchTest** to enforce layer boundaries at build time:
  - Domain must not reference Infrastructure or API
  - Application must not reference API
  - Infrastructure must not reference API
- **Test project structure mirrors source structure:**
  ```
  tests/
  ├── Blog.UnitTests/         # Domain + Application layer tests
  │   ├── Domain/
  │   └── Application/
  ├── Blog.IntegrationTests/  # EF Core + Redis + API tests (Testcontainers)
  ├── Blog.ArchTests/         # NetArchTest layer enforcement
  └── load/                   # k6 load test scenarios
  ```
- **CI validates migrations** by running the full cycle: apply all → rollback all → re-apply all against a real PostgreSQL 18 container.

## SHOULD

- Unit tests cover Domain aggregates (business rules, state transitions) and Application handlers (command/query logic with mocked repositories).
- E2E tests use Playwright 1.58 to test full user flows: reader browsing, author creating posts, admin managing users.
- Load tests use k6 with scenarios: smoke, load, stress, spike, soak, breakpoint.
- Test naming follows the pattern: `<MethodUnderTest>_<Scenario>_<ExpectedResult>`.
  ```csharp
  [Fact]
  public void Publish_WhenStatusIsDraft_SetsStatusToPublished() { }

  [Fact]
  public void Publish_WhenAlreadyPublished_ThrowsInvalidOperationException() { }
  ```
- Load test pass criteria for Phase 1: P95 < 200ms, error rate < 0.1%, at 200 concurrent users.

## NEVER

- Never mock the database in integration tests — use Testcontainers with real PostgreSQL/Redis.
  ```csharp
  // NEVER in integration tests
  var mockDb = new Mock<BlogDbContext>();

  // CORRECT
  var container = new PostgreSqlBuilder().Build();
  ```
- Never skip architecture tests — they are the automated guardrail for layer boundaries.
- Never commit load test results with hardcoded production URLs or credentials.
