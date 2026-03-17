# Phase 2: Infrastructure + Application Pipeline - Context

**Gathered:** 2026-03-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Blog.Infrastructure implements all Domain repository interfaces, Blog.Application has the complete MediatR 4-behavior pipeline registered in the correct order, Redis cache-aside with pattern invalidation is operational via Lua scripts, IUnitOfWork wraps both DbContexts in a shared transaction for cross-context atomicity, and the Testcontainers integration test scaffold is ready for feature tests.

This phase also includes GetTagListQuery and CreateTagCommand as the first real Application layer commands — they serve as pipeline test vehicles and are not throw-away probes.

Phase 2 is an enabling infrastructure phase: no standalone requirement IDs map to it, but it unblocks all feature phases (3+).

</domain>

<decisions>
## Implementation Decisions

### Integration test harness

- **WebApplicationFactory<Program>** is the test bootstrap strategy — spins up the full ASP.NET Core pipeline so tests hit real HTTP endpoints, verifying middleware, auth, and routing alongside the MediatR pipeline.
- **Shared containers per test run** — one PostgreSQL 18 + Redis 8 Testcontainers instance starts at test run start and is shared across all test classes via a single xUnit `[CollectionDefinition]`.
- **Respawn** library resets the database between tests (deletes all rows in FK order, ~20ms). No schema rebuild, no transaction rollback.
- **Shared collection fixture** (`[CollectionDefinition]`) across all integration tests. Tests within the same collection run sequentially and share warm containers.
- **BaseIntegrationTest abstract class** exposes `HttpClient`, a scoped `IServiceProvider`, and a `ResetDatabaseAsync()` helper. All integration test classes inherit from it — no setup boilerplate repeated per class.
- **appsettings.Testing.json** in Blog.API overrides connection strings when `ASPNETCORE_ENVIRONMENT=Testing`. ApiFactory injects Testcontainers ports into this config at startup.
- **Fake JWT via test helper** — a `GenerateJwt(role)` helper in BaseIntegrationTest creates valid signed tokens with any role, bypassing real OAuth flows. Phase 2 has no auth endpoints yet; real tokens are Phase 3.
- **Blog.ArchTests and Blog.IntegrationTests remain separate projects** — ArchTests is pure NetArchTest (no containers, no HTTP); IntegrationTests uses Testcontainers + WebApplicationFactory. Separate CI steps, separate failure domains.

### Domain Event dispatch mechanism

- **IUnitOfWork.CommitAsync()** is the dispatch point — not a SaveChangesInterceptor. Command handlers call `_uow.CommitAsync(ct)` which saves and dispatches events as one explicit operation.
- **Eagerly scan tracked aggregates** — CommitAsync() scans all EF Core change-tracked entities for domain events before calling SaveChangesAsync(). Events are cleared from aggregates before save, then dispatched after save completes.
  ```
  1. Collect events from all tracked AggregateRoot<TId> entities
  2. Clear DomainEvents on each aggregate
  3. SaveChangesAsync(ct)
  4. For each event: await _mediator.Publish(evt, ct)
  ```
- **Shared DbConnection** — IUnitOfWork holds one shared `NpgsqlConnection`. Both BlogDbContext and IdentityDbContext share this connection and the same transaction (ADR-007 Option A). Satisfies cross-context atomicity for Register/Ban operations.
- **Domain Event handler failures are logged and swallowed** — event handlers are fire-and-forget side effects (cache invalidation, email triggers). If a handler throws after CommitAsync() has committed, log the error but don't bubble up — the primary write already succeeded.
- **IUnitOfWork defined in Blog.Application/Abstractions/** — handlers depend on the interface, not the EF Core implementation. Implementation goes in Blog.Infrastructure.
- **IAllowAnonymous marker interface** — AuthorizationBehavior skips auth check for commands/queries that implement `IAllowAnonymous`. GetTagListQuery uses this marker. Auth behavior only throws for requests that lack both IAllowAnonymous and a valid current user.
- **CachingBehavior is a silent no-op** for requests that don't implement ICacheableQuery — calls `next()` immediately with zero overhead and no logging.

### Application abstraction stubs

- **NoOp stubs registered in Phase 2** for services that can't be fully implemented yet: `NoOpEmailService`, `NoOpStorageService` live in `Blog.Infrastructure/Services/NoOp/`. They implement the interface, log a debug message, and return success/empty result. Phase 3/4 replaces them with real implementations.
- **ICurrentUserService is fully implemented in Phase 2** — reads JWT claims from IHttpContextAccessor and returns current user's Id and Role. Required for AuthorizationBehavior to work. JWT auth is standard ASP.NET — no dependency on Phase 3 OAuth flows.
- **IIdentityService interface defined in Phase 2** in `Blog.Application/Abstractions/` (needed for IUnitOfWork cross-context transaction contract design), but the concrete IdentityService implementation is deferred to Phase 3.
- **IDateTimeService abstraction** in `Blog.Application/Abstractions/` with `DateTimeService` in Infrastructure returning `DateTimeOffset.UtcNow`. Makes time deterministic in tests.
- **IRedisCacheService defined in Blog.Application/Abstractions/** — CachingBehavior and Domain Event handlers (both Application layer) depend on this interface. Implementation in `Blog.Infrastructure/Caching/`.
- **AddBlogApplication() and AddBlogInfrastructure() extension methods** created in Phase 2. Program.cs calls both. All DI registrations go through these methods — Phase 3+ adds to them, not to Program.cs directly.
- **Result<T> introduced in Phase 2** in `Blog.Application/Common/`. Handlers return `Result<T>` (Ok/Failure with error message/code) instead of throwing exceptions for expected failures. Controllers map Result to HTTP responses. Typed exceptions (NotFoundException, ValidationException) map to ProblemDetails via GlobalExceptionHandler middleware.

### Pipeline test vehicles

- **GetTagListQuery** as the primary pipeline test vehicle — implements `ICacheableQuery` (tests caching behavior + all 4 behaviors). Implements `IAllowAnonymous` (tag list is public). Returns flat `IReadOnlyList<TagDto>` — no pagination (tags are a small fixed set; paginated list queries belong to Phase 4+).
- **CreateTagCommand** as the write-side test vehicle — requires Editor/Admin role, exercises ValidationBehavior + AuthorizationBehavior. Also fires TagCreatedEvent, exercising cache invalidation via Domain Events.
- **All 4 integration test scenarios implemented** to satisfy Phase 2 success criteria:
  1. **SC1** — NetArchTest (Blog.ArchTests) verifies pipeline behaviors are registered in order: Validation → Logging → Authorization → Caching
  2. **SC2** — Integration test: IUnitOfWork begins transaction across both DbContexts, simulates mid-transaction failure, verifies both contexts rolled back atomically
  3. **SC3** — Integration test: GetTagListQuery called twice; assert DB not queried on second call (served from Redis)
  4. **SC4+SC5** — Integration test: CreateTagCommand fires TagCreatedEvent; verify relevant Redis key pattern is cleared by Lua script

### Claude's Discretion

- Exact Respawn configuration (tables to ignore, FK handling)
- Lua script implementation for wildcard cache invalidation (SCAN + DEL pattern)
- Redis connection resilience policy (retry count, backoff)
- Exact `AggregateRoot<TId>.DomainEvents` collection type and `ClearDomainEvents()` implementation
- Result<T> exact shape (whether to include error codes, metadata)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Transactions
- `docs/blog-platform/03-architecture-decisions.md` §ADR-007 — Transaction Strategy: shared DbConnection between IdentityDbContext and BlogDbContext (Option A); IUnitOfWork abstraction requirement; cross-context atomicity for Register/Ban
- `docs/blog-platform/03-architecture-decisions.md` §ADR-008 — Cache opt-in mechanism: ICacheableQuery interface, CachingBehavior bypass for non-cacheable queries, TTL declaration pattern

### Caching
- `.claude/rules/caching.md` — CacheKeys.cs naming convention, cache invalidation via Domain Events only (never in command handlers), Lua script requirement for wildcard invalidation (SCAN+DEL, never KEYS), cache TTL table

### Backend Architecture
- `.claude/rules/backend-architecture.md` — MediatR pipeline behavior order (fixed and immutable), CQRS folder structure, Application layer abstraction locations, repository interface vs. implementation separation

### Testing
- `.claude/rules/testing.md` — Testcontainers requirement (real PostgreSQL/Redis, never mock DB in integration tests), NetArchTest layer enforcement rules, test naming convention

### Database
- `.claude/rules/database.md` — snake_case, UUID PKs, TIMESTAMPTZ — relevant for repository implementations and any new EF Core configurations added in Phase 2

### Security
- `.claude/rules/security-auth.md` — RBAC 3-layer enforcement, ICurrentUserService usage in handlers (never access IHttpContextAccessor from Application layer directly)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- `Blog.Domain/Common/AggregateRoot.cs` — base class all aggregates extend; CommitAsync() scans its `DomainEvents` collection
- `Blog.Domain/Repositories/IPostRepository.cs`, `ICommentRepository.cs`, `IUserRepository.cs`, `ITagRepository.cs` — all 4 repository interfaces already defined; Phase 2 implements all 4 in `Blog.Infrastructure/Persistence/Repositories/`
- `Blog.Domain/DomainEvents/TagEvents.cs`, `PostEvents.cs`, `CommentEvents.cs` — all domain events already defined as C# records; Phase 2 wires INotificationHandler for each
- `Blog.Infrastructure/Persistence/BlogDbContext.cs` — existing Phase 1 DbContext; Phase 2 adds IUnitOfWork wrapper around it + shared connection setup
- `Blog.Infrastructure/Persistence/Configurations/` — all EF Core entity configs already exist; Phase 2 adds no new configurations

### Established Patterns

- Layer dependency: Domain → Application → Infrastructure → Presentation (enforced by Blog.ArchTests from Phase 1)
- snake_case column names via EF Core conventions (Phase 1)
- UUID primary keys, TIMESTAMPTZ timestamps (Phase 1 schema)
- Nx monorepo with `@nx/dotnet` plugin (Phase 1)

### Integration Points

- `Blog.API/Program.cs` — Phase 2 adds `AddBlogApplication()` + `AddBlogInfrastructure()` extension method calls, JWT auth middleware registration, GlobalExceptionHandler middleware
- `Blog.Infrastructure` → `Blog.API` — DI registration only; no business logic in API
- `Blog.Application` → `Blog.Domain` — handlers depend on repository interfaces and domain aggregates
- `docker-compose.yml` (repo root) — already has PostgreSQL 18 + Redis 8; Testcontainers replaces this in CI (no compose needed in CI pipeline)
- `Blog.ArchTests` — already enforces layer boundaries from Phase 1; Phase 2 adds pipeline behavior order test as a new arch test rule

</code_context>

<specifics>
## Specific Ideas

- CommitAsync() dispatch pattern explicitly chosen over SaveChangesInterceptor — handlers call `_uow.CommitAsync(ct)` as the single explicit save+dispatch operation.
- IAllowAnonymous marker is the mechanism for public queries (not controller [AllowAnonymous] alone) — ensures AuthorizationBehavior and API layer are in sync for anonymous access.
- GetTagListQuery returns flat `IReadOnlyList<TagDto>` (not paginated) — tags are small, fixed sets; pagination complexity belongs in post/comment queries in Phase 4.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within Phase 2 scope.

</deferred>

---

*Phase: 02-infrastructure-application-pipeline*
*Context gathered: 2026-03-16*
