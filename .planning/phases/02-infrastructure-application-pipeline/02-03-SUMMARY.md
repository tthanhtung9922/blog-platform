---
phase: 02-infrastructure-application-pipeline
plan: "03"
subsystem: api

tags: [mediator, cqrs, redis, jwt, aspnetcore, fluentvalidation, problemdetails]

# Dependency graph
requires:
  - phase: 02-infrastructure-application-pipeline/02-02
    provides: IUnitOfWork, IRedisCacheService, RedisCacheService, CacheKeys, repository implementations
  - phase: 02-infrastructure-application-pipeline/02-01
    provides: MediatR pipeline behaviors (ValidationBehavior, LoggingBehavior, AuthorizationBehavior, CachingBehavior), ICacheableQuery, IAllowAnonymous, IAuthorizedRequest, AddBlogApplication()
  - phase: 01-monorepo-foundation-domain-layer/01-02
    provides: Tag aggregate, TagCreatedEvent, Slug value object, ITagRepository
provides:
  - GetTagListQuery (ICacheableQuery + IAllowAnonymous) — public tag list with 1h Redis cache
  - CreateTagCommand (IAuthorizedRequest roles: Admin/Editor) — write-side pipeline test vehicle
  - CreateTagCommandValidator — FluentValidation for tag name (NotEmpty, MaxLength 100)
  - CreateTagCommandHandler — calls uow.CommitAsync() triggering TagCreatedEvent dispatch
  - TagCreatedCacheInvalidationHandler — INotificationHandler<TagCreatedEvent> invalidating tag:list:*
  - GlobalExceptionHandler — RFC 9457 ProblemDetails: ValidationException→422, NotFoundException→404, ForbiddenAccessException→403
  - TagsController at /api/v1/tags — GET (anonymous) + POST (authorized), delegates to IMediator
  - Program.cs with full middleware stack: AddBlogApplication() + AddBlogInfrastructure() + JWT Bearer + GlobalExceptionHandler
  - public partial class Program {} for WebApplicationFactory<Program> in integration tests
affects:
  - 02-infrastructure-application-pipeline/02-04 (integration tests use WebApplicationFactory<Program>, TagsController, GenerateJwt helper)
  - phase 03+ (all feature controllers follow TagsController pattern)

# Tech tracking
tech-stack:
  added:
    - Microsoft.AspNetCore.Authentication.JwtBearer 10.* (JWT Bearer middleware, added to Blog.API.csproj)
    - Scalar.AspNetCore 2.* (OpenAPI UI at /openapi in Development, added to Blog.API.csproj)
  patterns:
    - CQRS handler pattern: record Command/Query, class Handler(deps) : IRequestHandler, class Validator : AbstractValidator
    - Domain event → cache invalidation: INotificationHandler<DomainEvent> calls RemoveByPatternAsync
    - RFC 9457 ProblemDetails error contract: IExceptionHandler maps typed exceptions to HTTP status codes
    - Controller pattern: no business logic, pure IMediator delegation, [Route("api/v1/[controller]")]

key-files:
  created:
    - apps/blog-api/src/Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQuery.cs
    - apps/blog-api/src/Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQueryHandler.cs
    - apps/blog-api/src/Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommand.cs
    - apps/blog-api/src/Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommandHandler.cs
    - apps/blog-api/src/Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommandValidator.cs
    - apps/blog-api/src/Blog.Application/Features/Tags/EventHandlers/TagCreatedCacheInvalidationHandler.cs
    - apps/blog-api/src/Blog.API/Middleware/GlobalExceptionHandler.cs
    - apps/blog-api/src/Blog.API/Controllers/TagsController.cs
  modified:
    - apps/blog-api/src/Blog.API/Program.cs
    - apps/blog-api/src/Blog.API/Blog.API.csproj
    - apps/blog-api/src/Blog.API/appsettings.json

key-decisions:
  - "TagCreatedCacheInvalidationHandler uses private const string TagListPattern = 'tag:list:*' instead of referencing CacheKeys.Patterns.TagList from Blog.Infrastructure — avoids Application→Infrastructure layer violation while keeping the pattern correct"
  - "BlogDbContext registration kept in Program.cs (not moved to AddBlogInfrastructure) for EF Core migration tooling compatibility — dotnet ef migrations requires DbContext reachable from the startup project"
  - "public partial class Program {} at end of Program.cs — required for WebApplicationFactory<Program> in Blog.IntegrationTests; top-level statements produce internal Program class without this declaration"

patterns-established:
  - "Controller pattern: [ApiController], [Route('api/v1/[controller]')], constructor injection of IMediator only, no business logic"
  - "GlobalExceptionHandler: IExceptionHandler implementation registered via AddExceptionHandler<T>() + UseExceptionHandler() — maps custom Application exceptions to ProblemDetails"
  - "Domain event cache invalidation: INotificationHandler<DomainEvent> in Blog.Application/Features/<Aggregate>/EventHandlers/ calls cache.RemoveByPatternAsync(pattern)"

requirements-completed: []

# Metrics
duration: 3min
completed: 2026-03-17
---

# Phase 02 Plan 03: CQRS Pipeline Test Vehicles + Blog.API Wiring Summary

**GetTagListQuery (Redis-cached public endpoint) + CreateTagCommand (authorized domain event pipeline) + GlobalExceptionHandler (ProblemDetails) + TagsController (/api/v1/tags) + Program.cs with full JWT + MediatR middleware stack ready for integration tests**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-17T16:18:52Z
- **Completed:** 2026-03-17T16:22:14Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments

- Full CQRS pipeline vehicles created: GetTagListQuery exercises caching + anonymous access path; CreateTagCommand exercises validation + authorization + domain event dispatch + cache invalidation
- Blog.API wired with complete middleware stack: AddBlogApplication() + AddBlogInfrastructure(), JWT Bearer auth, GlobalExceptionHandler (RFC 9457 ProblemDetails), TagsController
- `public partial class Program {}` declared, enabling WebApplicationFactory<Program> for Plan 04 integration tests
- `dotnet build apps/blog-api/src/Blog.API/Blog.API.csproj` passes with 0 warnings and 0 errors

## Task Commits

Each task was committed atomically:

1. **Task 1: CQRS pipeline vehicles** - `6c82e8e` (feat)
2. **Task 2: Blog.API wiring** - `deba896` (feat)

**Plan metadata:** (pending final docs commit)

## Files Created/Modified

- `Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQuery.cs` — IRequest<IReadOnlyList<TagDto>> + ICacheableQuery (tag:list:all, 1h TTL) + IAllowAnonymous
- `Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQueryHandler.cs` — maps Tag aggregates to TagDto via ITagRepository.GetAllAsync
- `Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommand.cs` — IRequest<TagDto> + IAuthorizedRequest with RequiredRoles [Admin, Editor]
- `Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommandHandler.cs` — calls uow.CommitAsync() to trigger TagCreatedEvent dispatch post-save
- `Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommandValidator.cs` — NotEmpty + MaxLength(100) for Name
- `Blog.Application/Features/Tags/EventHandlers/TagCreatedCacheInvalidationHandler.cs` — INotificationHandler<TagCreatedEvent> calls RemoveByPatternAsync("tag:list:*")
- `Blog.API/Middleware/GlobalExceptionHandler.cs` — IExceptionHandler: ValidationException→422, NotFoundException→404, ForbiddenAccessException→403; no stack traces in response
- `Blog.API/Controllers/TagsController.cs` — [Route("api/v1/[controller]")] GET (AllowAnonymous) + POST (Authorize) delegating to IMediator
- `Blog.API/Program.cs` — rewritten with AddBlogApplication + AddBlogInfrastructure + JWT Bearer + GlobalExceptionHandler + public partial class Program {}
- `Blog.API/Blog.API.csproj` — added JwtBearer, Scalar.AspNetCore, Blog.Application project reference
- `Blog.API/appsettings.json` — added Redis connection string + Jwt config section

## Decisions Made

1. **Pattern constant in Application layer**: `TagCreatedCacheInvalidationHandler` uses `private const string TagListPattern = "tag:list:*"` rather than referencing `CacheKeys.Patterns.TagList` from Blog.Infrastructure. The plan noted this was acceptable, but the architecture rule (`Application MUST NOT reference Infrastructure`) is absolute. Using a string constant in Application keeps the layer boundary clean.

2. **BlogDbContext kept in Program.cs**: The plan considered moving DbContext registration to `AddBlogInfrastructure()` but noted it must remain in Program.cs for `dotnet ef migrations` tooling. EF Core design-time tools scan the startup project for DbContext — if it's registered only inside Infrastructure's DI extension method, migration discovery fails.

3. **`public partial class Program {}`**: Top-level statement Programs in C# generate an `internal` Program class. Without the explicit `public partial` declaration, `WebApplicationFactory<Program>` in a separate test assembly cannot access the type, causing a compile error in Plan 04 integration tests.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Avoided Application→Infrastructure layer violation in TagCreatedCacheInvalidationHandler**
- **Found during:** Task 1 (TagCreatedCacheInvalidationHandler implementation)
- **Issue:** Plan suggested using `CacheKeys.Patterns.TagList` from `Blog.Infrastructure.Caching` in an Application layer class, which violates the hard architectural rule "Application layer must never reference Infrastructure"
- **Fix:** Defined `private const string TagListPattern = "tag:list:*"` directly in the handler class instead
- **Files modified:** `Blog.Application/Features/Tags/EventHandlers/TagCreatedCacheInvalidationHandler.cs`
- **Verification:** Blog.Application.csproj has no reference to Blog.Infrastructure; `dotnet build Blog.Application.csproj` passes
- **Committed in:** `6c82e8e` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2 — architecture correctness)
**Impact on plan:** The fix strengthens architecture compliance. The string literal `"tag:list:*"` is identical in value to `CacheKeys.Patterns.TagList` — no behavioral difference, only cleaner layering.

## Issues Encountered

None — build succeeded on first attempt for both tasks.

## User Setup Required

None - no external service configuration required for compilation. Running the API requires PostgreSQL and Redis (connection strings in appsettings.json).

## Next Phase Readiness

- Blog.API builds cleanly and exposes `/api/v1/tags` (GET + POST) with full MediatR pipeline
- `public partial class Program {}` is in place — Plan 04 (integration tests) can use `WebApplicationFactory<Program>`
- JWT Bearer auth configured — Plan 04 test helper `GenerateJwt(role)` can issue valid tokens
- GlobalExceptionHandler maps all custom exceptions to ProblemDetails — integration tests can assert HTTP status codes directly

## Self-Check: PASSED

- FOUND: `apps/blog-api/src/Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQuery.cs`
- FOUND: `apps/blog-api/src/Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommand.cs`
- FOUND: `apps/blog-api/src/Blog.Application/Features/Tags/EventHandlers/TagCreatedCacheInvalidationHandler.cs`
- FOUND: `apps/blog-api/src/Blog.API/Middleware/GlobalExceptionHandler.cs`
- FOUND: `apps/blog-api/src/Blog.API/Controllers/TagsController.cs`
- FOUND: `apps/blog-api/src/Blog.API/Program.cs`
- COMMIT `6c82e8e`: feat(02-03): CQRS pipeline vehicles — verified
- COMMIT `deba896`: feat(02-03): Blog.API wiring — verified

---
*Phase: 02-infrastructure-application-pipeline*
*Completed: 2026-03-17*
