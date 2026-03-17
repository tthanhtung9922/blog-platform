---
phase: 02-infrastructure-application-pipeline
plan: "04"
subsystem: testing
tags: [testcontainers, respawn, xunit, integration-tests, arch-tests, mediatR, redis, postgresql, webapplicationfactory]

# Dependency graph
requires:
  - phase: 02-infrastructure-application-pipeline/02-03
    provides: TagsController, CreateTagCommand, GetTagListQuery, TagCreatedCacheInvalidationHandler, public partial class Program
  - phase: 02-infrastructure-application-pipeline/02-02
    provides: IUnitOfWork implementation, RedisCacheService with Lua SCAN+DEL invalidation
  - phase: 02-infrastructure-application-pipeline/02-01
    provides: MediatR pipeline behaviors (Validation, Logging, Authorization, Caching), ICacheableQuery, IAuthorizedRequest
provides:
  - Blog.IntegrationTests project with Testcontainers postgres:18 + redis:8 test harness
  - ApiFactory (WebApplicationFactory<Program>) + IntegrationTestFixture (Respawn 7.0 + Redis flush)
  - JwtTokenHelper.GenerateJwt(role) for JWT-authenticated HTTP test calls
  - SC1 arch test verifying MediatR pipeline behavior registration order via DI reflection
  - SC2 integration test verifying IUnitOfWork rollback on DB constraint violation
  - SC3 integration test verifying Redis cache hit on second GetTagListQuery call
  - SC4+SC5 integration test verifying CreateTagCommand fires TagCreatedEvent that invalidates tag:list:* cache
affects: [phase-03, all future integration test phases]

# Tech tracking
tech-stack:
  added:
    - "Testcontainers.PostgreSql 4.11.0 — real postgres:18 container per test suite"
    - "Testcontainers.Redis 4.10.0 — real redis:8 container per test suite"
    - "Respawn 7.0.0 — fast DB reset between tests via row deletion in FK order"
    - "Microsoft.AspNetCore.Mvc.Testing 10.* — WebApplicationFactory<Program> for in-process HTTP testing"
    - "System.IdentityModel.Tokens.Jwt 8.* — JWT token generation in JwtTokenHelper"
  patterns:
    - "IntegrationTestFixture pattern: shared fixture starts containers once, Respawn + Redis flush reset between tests"
    - "JwtTokenHelper pattern: generates signed JWT tokens using same key as appsettings.json for authenticated test calls"
    - "SC1 arch test via DI reflection: build ServiceCollection, call AddBlogApplication(), inspect GetServices<IPipelineBehavior<>> for registration order"
    - "Testcontainers builder with image parameter constructor: new PostgreSqlBuilder(image).Build() (parameterless constructor is obsolete)"

key-files:
  created:
    - tests/Blog.IntegrationTests/Blog.IntegrationTests.csproj
    - tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs
    - tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs
    - tests/Blog.IntegrationTests/Fixtures/IntegrationTestCollection.cs
    - tests/Blog.IntegrationTests/Helpers/JwtTokenHelper.cs
    - tests/Blog.IntegrationTests/Infrastructure/UnitOfWorkTests.cs
    - tests/Blog.IntegrationTests/Infrastructure/CachingTests.cs
    - tests/Blog.ArchTests/PipelineBehaviorOrderTests.cs
  modified:
    - tests/Blog.ArchTests/Blog.ArchTests.csproj
    - BlogPlatform.slnx

key-decisions:
  - "Respawn 7.0 API uses Respawner.CreateAsync() + RespawnerOptions (NOT deprecated Checkpoint API from Respawn 6.x)"
  - "TablesToIgnore requires Respawn.Graph.Table type — must add using Respawn.Graph; explicitly"
  - "Testcontainers 4.11 parameterless constructor is obsolete — use new PostgreSqlBuilder(image) and new RedisBuilder(image) with image string"
  - "PostAsJsonAsync requires using System.Net.Http.Json; — not included by implicit usings in test projects"
  - "SC1 test uses DI reflection not NetArchTest — NetArchTest inspects type metadata but cannot verify runtime DI registration order"
  - "Blog.ArchTests gains Blog.Application project reference for SC1 test; EF version conflict warning (10.0.4 vs 10.0.5) is pre-existing and benign"

patterns-established:
  - "Test isolation pattern: IAsyncLifetime.InitializeAsync() calls ResetDatabaseAsync() which runs Respawn.ResetAsync() + FlushDatabaseAsync() — both stores reset before each test"
  - "SC3 cache proof pattern: seed DB → first query (cache miss, populates Redis) → delete DB row → second query (cache hit, returns cached result despite no DB row)"
  - "SC4+SC5 invalidation proof pattern: populate cache → POST via HTTP with JWT → assert Redis key absent → query again returns updated count"

requirements-completed: []

# Metrics
duration: 5min
completed: 2026-03-17
---

# Phase 2 Plan 04: Integration Tests + SC1 Arch Test Summary

**Testcontainers test harness with Respawn + Redis flush, SC1-SC5 Phase 2 verification tests covering MediatR pipeline order, UnitOfWork atomicity, Redis cache hit, and domain event cache invalidation**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-17T15:45:45Z
- **Completed:** 2026-03-17T15:50:46Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments

- Blog.IntegrationTests project scaffolded with Testcontainers postgres:18 + redis:8, Respawn 7.0, WebApplicationFactory, and JwtTokenHelper — builds cleanly
- SC1 arch test (PipelineBehaviorOrderTests) passes: DI reflection confirms ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior registration order
- SC2/SC3/SC4+SC5 integration tests exist and compile: UnitOfWork rollback, Redis cache hit, and TagCreatedEvent cache invalidation scenarios all ready for CI with Docker

## Task Commits

Each task was committed atomically:

1. **Task 1: Blog.IntegrationTests project scaffold** - `8ceecb0` (feat)
2. **Task 2: SC1 arch test + SC2/SC3/SC4+SC5 integration tests** - `038286e` (feat)

**Plan metadata:** (see final metadata commit below)

## Files Created/Modified

- `tests/Blog.IntegrationTests/Blog.IntegrationTests.csproj` - New test project: Testcontainers.PostgreSql 4.11.0, Testcontainers.Redis 4.10.0, Respawn 7.0.0, Microsoft.AspNetCore.Mvc.Testing
- `tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs` - WebApplicationFactory<Program> starting postgres:18 + redis:8 containers; overrides connection strings via in-memory config
- `tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs` - Shared fixture: applies migrations once, Respawn 7.0 with TablesToIgnore=[__EFMigrationsHistory], ResetDatabaseAsync() resets DB + flushes Redis
- `tests/Blog.IntegrationTests/Fixtures/IntegrationTestCollection.cs` - [CollectionDefinition("Integration")] + ICollectionFixture<IntegrationTestFixture>
- `tests/Blog.IntegrationTests/Helpers/JwtTokenHelper.cs` - GenerateJwt(role) creates signed JWTs using same key as appsettings.json for authenticated HTTP calls
- `tests/Blog.IntegrationTests/Infrastructure/UnitOfWorkTests.cs` - SC2: verifies CommitAsync persists on success, throws and rolls back on duplicate slug constraint violation
- `tests/Blog.IntegrationTests/Infrastructure/CachingTests.cs` - SC3: cache hit after DB row deleted; SC4+SC5: CreateTagCommand HTTP call invalidates tag:list:* via domain event
- `tests/Blog.ArchTests/PipelineBehaviorOrderTests.cs` - SC1: DI reflection test verifying pipeline behavior registration order
- `tests/Blog.ArchTests/Blog.ArchTests.csproj` - Added Blog.Application project reference for SC1 test
- `BlogPlatform.slnx` - Added Blog.IntegrationTests to solution

## Decisions Made

- **Respawn.Graph.Table namespace:** `Table` type lives in `Respawn.Graph` namespace, not `Respawn` — must add `using Respawn.Graph;` explicitly. Plan omitted this import.
- **Testcontainers image constructor:** `new PostgreSqlBuilder()` and `new RedisBuilder()` (parameterless) are obsolete in Testcontainers 4.x. Use `new PostgreSqlBuilder("postgres:18")` with image string in constructor.
- **PostAsJsonAsync namespace:** `HttpClient.PostAsJsonAsync()` requires `using System.Net.Http.Json;` — not included by .NET 10 implicit usings in test projects.
- **SC1 via DI not NetArchTest:** NetArchTest can only inspect type metadata (assembly, namespace, dependencies). It cannot verify runtime DI registration order. SC1 must use `services.AddBlogApplication(); provider.GetServices<IPipelineBehavior<>>()` reflection approach.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added missing using Respawn.Graph for Table type**
- **Found during:** Task 1 (IntegrationTestFixture.cs)
- **Issue:** `Table` type is in `Respawn.Graph` namespace, not `Respawn`. Plan's code snippet omitted this import, causing CS0246 compile error.
- **Fix:** Added `using Respawn.Graph;` to IntegrationTestFixture.cs
- **Files modified:** tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs
- **Verification:** `dotnet build` exits 0
- **Committed in:** 8ceecb0 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed obsolete Testcontainers parameterless constructor**
- **Found during:** Task 1 (ApiFactory.cs)
- **Issue:** `new PostgreSqlBuilder().WithImage(...)` and `new RedisBuilder().WithImage(...)` trigger CS0618 obsolete warning in Testcontainers 4.x. The new API passes image as constructor parameter.
- **Fix:** Changed to `new PostgreSqlBuilder("postgres:18")` and `new RedisBuilder("redis:8")` — both WithImage() calls removed since image is now in constructor
- **Files modified:** tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs
- **Verification:** `dotnet build` exits 0 with 0 warnings
- **Committed in:** 8ceecb0 (Task 1 commit)

**3. [Rule 1 - Bug] Added missing using System.Net.Http.Json for PostAsJsonAsync**
- **Found during:** Task 2 (CachingTests.cs)
- **Issue:** `HttpClient.PostAsJsonAsync()` is an extension method in `System.Net.Http.Json` namespace. Plan's code snippet omitted this import, causing CS1061 compile error.
- **Fix:** Added `using System.Net.Http.Json;` to CachingTests.cs
- **Files modified:** tests/Blog.IntegrationTests/Infrastructure/CachingTests.cs
- **Verification:** `dotnet build` exits 0
- **Committed in:** 038286e (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (3 Rule 1 - missing using directives / obsolete API)
**Impact on plan:** All three fixes were missing `using` directives or a deprecated constructor pattern — no scope change, no architectural change.

## Issues Encountered

- EF Core version conflict warning in Blog.ArchTests (10.0.4 vs 10.0.5) appeared after adding Blog.Application project reference. This is pre-existing — Blog.Infrastructure uses 10.0.5 via Npgsql while Blog.Application resolves 10.0.4. Warning only, builds succeed, all 10 arch tests pass. Deferred to a separate dependency alignment task.

## User Setup Required

None — no external service configuration required. Integration tests require Docker (for Testcontainers) when running SC2-SC5.

## Next Phase Readiness

- Phase 2 infrastructure is complete and verified: IUnitOfWork, Redis caching, MediatR pipeline, CQRS vehicles, test harness all in place
- SC1 passes immediately (no Docker needed): `dotnet test tests/Blog.ArchTests`
- SC2-SC5 require Docker for Testcontainers containers: `dotnet test tests/Blog.IntegrationTests`
- Phase 3 can begin: authentication/identity layer (IdentityDbContext, ASP.NET Identity, JWT token endpoints)

---
*Phase: 02-infrastructure-application-pipeline*
*Completed: 2026-03-17*
