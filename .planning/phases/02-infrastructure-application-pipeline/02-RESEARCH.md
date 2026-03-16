# Phase 2: Infrastructure + Application Pipeline - Research

**Researched:** 2026-03-16
**Domain:** MediatR 14 pipeline behaviors, ASP.NET Core DI, Redis cache-aside, Lua invalidation, Testcontainers integration test harness, Respawn database reset, WebApplicationFactory minimal API
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Integration test harness:**
- WebApplicationFactory<Program> is the test bootstrap strategy — spins up the full ASP.NET Core pipeline so tests hit real HTTP endpoints, verifying middleware, auth, and routing alongside the MediatR pipeline.
- Shared containers per test run — one PostgreSQL 18 + Redis 8 Testcontainers instance starts at test run start and is shared across all test classes via a single xUnit `[CollectionDefinition]`.
- Respawn library resets the database between tests (deletes all rows in FK order, ~20ms). No schema rebuild, no transaction rollback.
- Shared collection fixture (`[CollectionDefinition]`) across all integration tests. Tests within the same collection run sequentially and share warm containers.
- BaseIntegrationTest abstract class exposes `HttpClient`, a scoped `IServiceProvider`, and a `ResetDatabaseAsync()` helper. All integration test classes inherit from it — no setup boilerplate repeated per class.
- appsettings.Testing.json in Blog.API overrides connection strings when `ASPNETCORE_ENVIRONMENT=Testing`. ApiFactory injects Testcontainers ports into this config at startup.
- Fake JWT via test helper — a `GenerateJwt(role)` helper in BaseIntegrationTest creates valid signed tokens with any role, bypassing real OAuth flows. Phase 2 has no auth endpoints yet; real tokens are Phase 3.
- Blog.ArchTests and Blog.IntegrationTests remain separate projects — ArchTests is pure NetArchTest (no containers, no HTTP); IntegrationTests uses Testcontainers + WebApplicationFactory. Separate CI steps, separate failure domains.

**Domain Event dispatch mechanism:**
- IUnitOfWork.CommitAsync() is the dispatch point — not a SaveChangesInterceptor. Command handlers call `_uow.CommitAsync(ct)` which saves and dispatches events as one explicit operation.
- Eagerly scan tracked aggregates — CommitAsync() scans all EF Core change-tracked entities for domain events before calling SaveChangesAsync(). Events are cleared from aggregates before save, then dispatched after save completes. Sequence: (1) Collect events from all tracked AggregateRoot<TId> entities, (2) Clear DomainEvents on each aggregate, (3) SaveChangesAsync(ct), (4) For each event: await _mediator.Publish(evt, ct).
- Shared DbConnection — IUnitOfWork holds one shared NpgsqlConnection. Both BlogDbContext and IdentityDbContext share this connection and the same transaction (ADR-007 Option A). Satisfies cross-context atomicity for Register/Ban operations.
- Domain Event handler failures are logged and swallowed — event handlers are fire-and-forget side effects (cache invalidation, email triggers). If a handler throws after CommitAsync() has committed, log the error but don't bubble up — the primary write already succeeded.
- IUnitOfWork defined in Blog.Application/Abstractions/ — handlers depend on the interface, not the EF Core implementation. Implementation goes in Blog.Infrastructure.
- IAllowAnonymous marker interface — AuthorizationBehavior skips auth check for commands/queries that implement `IAllowAnonymous`. GetTagListQuery uses this marker. Auth behavior only throws for requests that lack both IAllowAnonymous and a valid current user.
- CachingBehavior is a silent no-op for requests that don't implement ICacheableQuery — calls `next()` immediately with zero overhead and no logging.

**Application abstraction stubs:**
- NoOp stubs registered in Phase 2 for services that can't be fully implemented yet: `NoOpEmailService`, `NoOpStorageService` live in `Blog.Infrastructure/Services/NoOp/`. They implement the interface, log a debug message, and return success/empty result. Phase 3/4 replaces them with real implementations.
- ICurrentUserService is fully implemented in Phase 2 — reads JWT claims from IHttpContextAccessor and returns current user's Id and Role. Required for AuthorizationBehavior to work. JWT auth is standard ASP.NET — no dependency on Phase 3 OAuth flows.
- IIdentityService interface defined in Phase 2 in `Blog.Application/Abstractions/` (needed for IUnitOfWork cross-context transaction contract design), but the concrete IdentityService implementation is deferred to Phase 3.
- IDateTimeService abstraction in `Blog.Application/Abstractions/` with `DateTimeService` in Infrastructure returning `DateTimeOffset.UtcNow`. Makes time deterministic in tests.
- IRedisCacheService defined in Blog.Application/Abstractions/ — CachingBehavior and Domain Event handlers (both Application layer) depend on this interface. Implementation in `Blog.Infrastructure/Caching/`.
- AddBlogApplication() and AddBlogInfrastructure() extension methods created in Phase 2. Program.cs calls both. All DI registrations go through these methods — Phase 3+ adds to them, not to Program.cs directly.
- Result<T> introduced in Phase 2 in `Blog.Application/Common/`. Handlers return `Result<T>` (Ok/Failure with error message/code) instead of throwing exceptions for expected failures. Controllers map Result to HTTP responses. Typed exceptions (NotFoundException, ValidationException) map to ProblemDetails via GlobalExceptionHandler middleware.

**Pipeline test vehicles:**
- GetTagListQuery as the primary pipeline test vehicle — implements `ICacheableQuery` (tests caching behavior + all 4 behaviors). Implements `IAllowAnonymous` (tag list is public). Returns flat `IReadOnlyList<TagDto>` — no pagination.
- CreateTagCommand as the write-side test vehicle — requires Editor/Admin role, exercises ValidationBehavior + AuthorizationBehavior. Also fires TagCreatedEvent, exercising cache invalidation via Domain Events.
- All 4 integration test scenarios implemented: SC1 (arch test: pipeline order), SC2 (IUnitOfWork rollback), SC3 (GetTagListQuery Redis cache hit), SC4+SC5 (TagCreatedEvent cache invalidation via Lua).

### Claude's Discretion

- Exact Respawn configuration (tables to ignore, FK handling)
- Lua script implementation for wildcard cache invalidation (SCAN + DEL pattern)
- Redis connection resilience policy (retry count, backoff)
- Exact `AggregateRoot<TId>.DomainEvents` collection type and `ClearDomainEvents()` implementation
- Result<T> exact shape (whether to include error codes, metadata)

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within Phase 2 scope.
</user_constraints>

---

## Summary

Phase 2 builds the full Application and Infrastructure layers on top of the Phase 1 Domain skeleton. The work divides into five areas: (1) the Blog.Application project with MediatR 14 four-behavior pipeline, CQRS abstractions, and Result<T>; (2) Blog.Infrastructure completing all repository implementations, the UnitOfWork cross-context transaction wrapper, Redis cache service with Lua invalidation, and NoOp stubs; (3) wiring Program.cs with extension methods and JWT auth middleware; (4) GetTagListQuery + CreateTagCommand as live pipeline test vehicles; and (5) a new Blog.IntegrationTests project backed by Testcontainers + WebApplicationFactory + Respawn.

The most critical technical facts: MediatR is now at version 14.1.0 (last updated March 2026, targeting .NET 10), and pipeline behavior registration order is enforced entirely by DI registration sequence — behaviors registered first execute first. The verification arch test for SC1 cannot use NetArchTest's type-inspection rules for pipeline order; it must use reflection against the DI container's `ServiceDescriptor` list. Respawn 7.0.0 is the current version; its `RespawnerOptions` requires `DbAdapter = DbAdapter.Postgres` for PostgreSQL and works with Npgsql connections. WebApplicationFactory with minimal API (top-level statements) requires `public partial class Program {}` at the bottom of Program.cs to make the class visible across assemblies.

**Primary recommendation:** Create Blog.Application first (no external dependencies beyond MediatR + FluentValidation), then Blog.Infrastructure (adds EF Core, StackExchange.Redis, Npgsql), then Blog.IntegrationTests (adds Testcontainers, Respawn, WebApplicationFactory). Each layer builds cleanly on the previous.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MediatR | 14.1.0 | CQRS dispatcher + IPipelineBehavior pipeline | Current version targeting .NET 10; Blog.Domain already references `12.*` — upgrade needed |
| FluentValidation | 12.1.1 | Input validation in ValidationBehavior | Last updated Dec 2025; major version for .NET 10 |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | `AddValidatorsFromAssembly` DI helper | Same version as core package |
| StackExchange.Redis | 2.12.1 | Redis client for cache-aside + Lua scripts | Last updated March 2026; standard .NET Redis client |
| Testcontainers.PostgreSql | 4.11.0 | Real PostgreSQL 18 container in tests | Latest version; official Testcontainers .NET module |
| Testcontainers.Redis | 4.10.0 | Real Redis 8 container in tests | Latest version; official Testcontainers .NET module |
| Respawn | 7.0.0 | Database reset between integration tests | Last updated Nov 2025; supports `DbAdapter.Postgres` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.* | JWT Bearer auth middleware | Required for ICurrentUserService to read claims from HTTP context |
| System.IdentityModel.Tokens.Jwt | 8.* | JWT token creation in test helper | Only used in Blog.IntegrationTests for GenerateJwt() helper |

### Existing (Phase 1 — no changes)
| Library | Version | Notes |
|---------|---------|-------|
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | Already in Blog.Infrastructure |
| EFCore.NamingConventions | 10.0.1 | Already in Blog.Infrastructure |
| NetArchTest.Rules | 1.3.2 | Already in Blog.ArchTests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Respawn | TRUNCATE in test teardown | Respawn orders deletions by FK graph (no CASCADE needed), ~20ms; manual TRUNCATE is faster but risks FK constraint errors on complex schemas |
| StackExchange.Redis directly | Microsoft.Extensions.Caching.StackExchangeRedis | IDistributedCache doesn't expose `ScriptEvaluateAsync` needed for Lua; direct StackExchange.Redis required for pattern invalidation |
| WebApplicationFactory | TestServer directly | WebApplicationFactory handles the full middleware + DI pipeline; TestServer requires more manual wiring |

**Installation (Blog.Application):**
```bash
dotnet add package MediatR --version 14.1.0
dotnet add package FluentValidation --version 12.1.1
dotnet add package FluentValidation.DependencyInjectionExtensions --version 12.1.1
```

**Installation (Blog.Infrastructure — additions):**
```bash
dotnet add package StackExchange.Redis --version 2.12.1
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.*
```

**Installation (Blog.IntegrationTests — new project):**
```bash
dotnet add package Testcontainers.PostgreSql --version 4.11.0
dotnet add package Testcontainers.Redis --version 4.10.0
dotnet add package Respawn --version 7.0.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package xunit --version 2.9.3
dotnet add package xunit.runner.visualstudio --version 3.1.5
dotnet add package FluentAssertions --version 6.*
```

---

## Architecture Patterns

### Recommended Project Structure

```
apps/blog-api/src/
├── Blog.Domain/              (Phase 1 — unchanged)
├── Blog.Application/         (NEW in Phase 2)
│   ├── Abstractions/         ← Interfaces: ICurrentUserService, IUnitOfWork,
│   │                           IRedisCacheService, IDateTimeService,
│   │                           IEmailService, IStorageService, IIdentityService
│   ├── Behaviors/            ← 4 IPipelineBehavior<,> implementations
│   ├── Common/               ← Result<T>, PaginatedList<T>, custom exceptions
│   ├── DTOs/                 ← TagDto, shared response shapes
│   └── Features/
│       └── Tags/
│           ├── Commands/CreateTag/    ← CreateTagCommand, Handler, Validator
│           ├── Queries/GetTagList/    ← GetTagListQuery, Handler
│           └── EventHandlers/        ← TagCreatedEventHandler (cache invalidation)
├── Blog.Infrastructure/      (EXTENDED in Phase 2)
│   ├── Caching/              ← RedisCacheService, CacheKeys
│   ├── Persistence/
│   │   ├── Repositories/     ← TagRepository, PostRepository, CommentRepository, UserRepository
│   │   └── UnitOfWork.cs     ← IUnitOfWork implementation (shared NpgsqlConnection)
│   ├── Services/
│   │   ├── CurrentUserService.cs
│   │   ├── DateTimeService.cs
│   │   └── NoOp/             ← NoOpEmailService, NoOpStorageService
│   └── DependencyInjection.cs  ← AddBlogInfrastructure() extension method
└── Blog.API/
    └── Program.cs            ← calls AddBlogApplication() + AddBlogInfrastructure()

tests/
├── Blog.ArchTests/           (EXTENDED — add pipeline order test)
├── Blog.UnitTests/           (unchanged — no Phase 2 additions)
└── Blog.IntegrationTests/    (NEW in Phase 2)
    ├── Fixtures/
    │   ├── ApiFactory.cs                ← WebApplicationFactory<Program> subclass
    │   ├── IntegrationTestFixture.cs    ← Testcontainers + Respawn setup
    │   └── IntegrationTestCollection.cs ← [CollectionDefinition("Integration")]
    ├── Helpers/
    │   └── JwtTokenHelper.cs            ← GenerateJwt(role)
    ├── Infrastructure/
    │   ├── PipelineOrderTests.cs        ← SC1: verify behavior order via DI reflection
    │   ├── UnitOfWorkTests.cs           ← SC2: cross-context rollback
    │   └── CachingTests.cs             ← SC3 + SC4+SC5: Redis cache hit + Lua invalidation
    └── Blog.IntegrationTests.csproj
```

### Pattern 1: MediatR 14 Pipeline Behavior Registration

**What:** IPipelineBehavior<TRequest, TResponse> open generics registered with `AddOpenBehavior()` inside `AddMediatR()`. Registration order = execution order.

**When to use:** In `AddBlogApplication()` extension method inside Blog.Application.

```csharp
// apps/blog-api/src/Blog.Application/DependencyInjection.cs
public static IServiceCollection AddBlogApplication(this IServiceCollection services)
{
    // MediatR 14.1 — behaviors run in registration order (first registered = outermost = runs first)
    services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

        // ORDER IS IMMUTABLE: Validation → Logging → Authorization → Caching
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    });

    // Register all FluentValidation validators in this assembly
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

    return services;
}
```

**CRITICAL:** In MediatR 14, behaviors registered via `AddOpenBehavior` within `AddMediatR(cfg => ...)` are appended to the `ServiceCollection` in registration order. Microsoft.Extensions.DependencyInjection returns `IEnumerable<IPipelineBehavior<,>>` in LIFO order when resolved via `GetServices()`, but MediatR internally reverses this so the first-registered behavior wraps all others (executes first). The arch test (SC1) must account for this reversal.

### Pattern 2: ValidationBehavior

```csharp
// apps/blog-api/src/Blog.Application/Behaviors/ValidationBehavior.cs
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new Application.Common.Exceptions.ValidationException(failures);

        return await next();
    }
}
```

### Pattern 3: AuthorizationBehavior with IAllowAnonymous

```csharp
// Blog.Application/Abstractions/IAuthorizedRequest.cs
public interface IAuthorizedRequest
{
    string[] RequiredRoles { get; }
}

// Blog.Application/Abstractions/IAllowAnonymous.cs
public interface IAllowAnonymous { }

// Blog.Application/Behaviors/AuthorizationBehavior.cs
public class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Public requests skip auth entirely
        if (request is IAllowAnonymous) return await next();

        // Authorized requests: check role
        if (request is IAuthorizedRequest authorized)
        {
            var userRole = currentUser.Role
                ?? throw new ForbiddenAccessException();

            if (!authorized.RequiredRoles.Contains(userRole))
                throw new ForbiddenAccessException();
        }

        return await next();
    }
}
```

### Pattern 4: CachingBehavior (silent no-op for non-cacheable)

```csharp
// Blog.Application/Behaviors/CachingBehavior.cs
public class CachingBehavior<TRequest, TResponse>(
    IRedisCacheService cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Non-cacheable: zero overhead, no logging
        if (request is not ICacheableQuery cacheable)
            return await next();

        var cached = await cache.GetAsync<TResponse>(cacheable.CacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT: {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        logger.LogDebug("Cache MISS: {CacheKey}", cacheable.CacheKey);
        var result = await next();
        var ttl = cacheable.CacheDuration ?? TimeSpan.FromMinutes(5);
        await cache.SetAsync(cacheable.CacheKey, result, ttl, ct);
        return result;
    }
}
```

### Pattern 5: IUnitOfWork — CommitAsync with Domain Event Dispatch

```csharp
// Blog.Application/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}

// Blog.Infrastructure/Persistence/UnitOfWork.cs
public class UnitOfWork(BlogDbContext blogContext, IPublisher publisher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
    {
        // 1. Collect events from all change-tracked aggregates
        var aggregates = blogContext.ChangeTracker
            .Entries<AggregateRoot<Guid>>()       // covers Guid-keyed aggregates
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var events = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // 2. Clear domain events before save (prevents double-dispatch on retry)
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        // 3. Persist
        await blogContext.SaveChangesAsync(ct);

        // 4. Dispatch events after successful persist
        //    Failures are swallowed after this point (fire-and-forget side effects)
        foreach (var evt in events)
        {
            try
            {
                await publisher.Publish(evt, ct);
            }
            catch (Exception ex)
            {
                // Log and swallow — primary write already committed
                // In Phase 9+, replace with outbox pattern for at-least-once delivery
            }
        }
    }
}
```

**Note on AggregateRoot<TId>:** The existing `AggregateRoot<TId>` in Phase 1 uses `Guid` for all current aggregates. The UnitOfWork queries `ChangeTracker.Entries<AggregateRoot<Guid>>()` — this covers Tag, Post, Comment, User aggregates. If any future aggregate uses a non-Guid key, the scan would need a non-generic base type. For Phase 2, Guid-only is correct.

### Pattern 6: Redis Lua Pattern Invalidation

**What:** SCAN cursor loop in Lua — non-blocking, atomic key discovery and deletion.

**Why not `KEYS *`:** KEYS is O(N) and blocks the Redis event loop. Never use in production.

```csharp
// Blog.Infrastructure/Caching/RedisCacheService.cs
private const string RemoveByPatternScript = @"
    local cursor = '0'
    repeat
        local result = redis.call('SCAN', cursor, 'MATCH', ARGV[1], 'COUNT', 100)
        cursor = result[1]
        local keys = result[2]
        for _, key in ipairs(keys) do
            redis.call('DEL', key)
        end
    until cursor == '0'
    return 1
";

public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
{
    var db = _connection.GetDatabase();
    await db.ScriptEvaluateAsync(RemoveByPatternScript, values: new RedisValue[] { pattern });
}
```

**Note:** `ScriptEvaluateAsync` with `values:` (not `keys:`) is the correct overload for ARGV parameters. StackExchange.Redis automatically handles EVALSHA caching after the first execution — no manual script loading needed.

**Note on Redis keyspace in Testcontainers:** Lua SCAN in a test container operates on the same single Redis instance shared across the test run. The test fixture's `ResetDatabaseAsync()` should also flush Redis (or use a per-test prefix) to prevent cache entries from one test contaminating another. Use `IServer.FlushDatabaseAsync()` from StackExchange.Redis to flush Redis between tests.

### Pattern 7: Integration Test Harness

**The minimal API visibility problem:** Blog.API uses top-level statements (Program.cs has no explicit `class Program`). The compiler generates an `internal` Program class. WebApplicationFactory<Program> in a separate test project cannot see it. Solution: add at the bottom of Program.cs:
```csharp
// Required for WebApplicationFactory in Blog.IntegrationTests
public partial class Program { }
```

**ApiFactory (WebApplicationFactory subclass):**
```csharp
// tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .WithDatabase("blog_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:8")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            });
        });
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
```

**IntegrationTestFixture with Respawn:**
```csharp
// tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs
public class IntegrationTestFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new ApiFactory();
    private Respawner _respawner = null!;
    private NpgsqlConnection _dbConnection = null!;

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        // Apply migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        await db.Database.MigrateAsync();

        // Initialize Respawn for PostgreSQL
        _dbConnection = new NpgsqlConnection(
            Factory.Services.GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection"));
        await _dbConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            // EF Core migrations history table must be excluded
            TablesToIgnore = new Table[] { "__EFMigrationsHistory" }
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);

        // Also flush Redis between tests
        var muxer = Factory.Services.GetRequiredService<IConnectionMultiplexer>();
        await muxer.GetServer(muxer.GetEndPoints().First()).FlushDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbConnection.DisposeAsync();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture> { }
```

### Pattern 8: Pipeline Order Arch Test (SC1)

NetArchTest cannot inspect DI registration order — it operates on type metadata, not runtime DI state. The SC1 verification must use DI container reflection:

```csharp
// tests/Blog.ArchTests/PipelineBehaviorOrderTests.cs
[Fact]
public void PipelineBehaviors_MustBeRegisteredInFixedOrder()
{
    // Build a minimal service collection mirroring AddBlogApplication()
    var services = new ServiceCollection();
    services.AddLogging();
    new BlogApplicationExtensions().RegisterBehaviors(services); // or just call AddBlogApplication

    var provider = services.BuildServiceProvider();

    // MediatR resolves behaviors as IEnumerable<IPipelineBehavior<Req, Res>>
    // The concrete types, in resolution order, must match: Validation → Logging → Authorization → Caching
    var behaviors = provider
        .GetServices<IPipelineBehavior<GetTagListQuery, IReadOnlyList<TagDto>>>()
        .Select(b => b.GetType().GetGenericTypeDefinition())
        .ToList();

    var expected = new[]
    {
        typeof(ValidationBehavior<,>),
        typeof(LoggingBehavior<,>),
        typeof(AuthorizationBehavior<,>),
        typeof(CachingBehavior<,>),
    };

    behaviors.Should().Equal(expected,
        because: "MediatR pipeline behavior order is fixed: Validation → Logging → Authorization → Caching");
}
```

**IMPORTANT:** MediatR 14 with `AddOpenBehavior` registers behaviors such that DI's `GetServices()` returns them in the order they were registered (first-registered first in the enumerable). MediatR then wraps them in that order, so the first in the list is the outermost behavior (executes first). This means the test above compares directly against the `expected` array without reversal.

Verify this behavior against MediatR's actual implementation when writing the test — run it against a real service provider with a simple probe request if needed.

### Anti-Patterns to Avoid

- **Registering behaviors outside `AddMediatR(cfg => ...)`:** Using `services.AddTransient(typeof(IPipelineBehavior<,>), ...)` after the `AddMediatR` call has undefined ordering relative to behaviors registered via `AddOpenBehavior`. All 4 behaviors must be in one `AddMediatR` call.
- **Dispatching domain events before SaveChangesAsync:** Phase 1 design is clear — collect, clear, save, dispatch. Reversing this order means events fire even when the DB write fails.
- **Using `KEYS *` for cache invalidation:** Must use Lua SCAN loop. The project rules explicitly forbid `KEYS`.
- **Caching IAllowAnonymous queries that return user-specific data:** GetTagListQuery is safe (truly public). Future queries must review cache key scoping if user context matters.
- **Flushing the entire Redis in prod-like tests:** `FlushDatabaseAsync()` is acceptable in test isolation — never in production code.
- **Missing `public partial class Program {}`:** WebApplicationFactory<Program> will throw a compile-time error in the test project without it.
- **Sharing a single `IServiceScope` across test methods:** Each test should create a fresh scope from `Factory.Services.CreateScope()` to avoid DI lifetime leaks.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Input validation in behaviors | Custom validation middleware | FluentValidation 12 + ValidationBehavior | FluentValidation handles nullable types, conditional rules, complex rule sets, and has DI integration for async validators |
| Database reset between tests | Manual TRUNCATE in teardown | Respawn 7.0 | Respawn computes FK deletion order automatically; avoids cascade violation errors; ~20ms reset vs. schema rebuild |
| Redis pattern delete | Manual KEYS + DEL loop | Lua SCAN+DEL script | KEYS blocks Redis server; manual loop has race conditions; Lua script is atomic server-side |
| JWT creation in tests | Real OAuth flow | `GenerateJwt(role)` helper with `System.IdentityModel.Tokens.Jwt` | No external OAuth dependency; creates valid signed tokens with any role and claims |
| Container lifecycle management | Manual Docker setup in CI | Testcontainers | Testcontainers pulls images, assigns random ports, handles cleanup; CI-safe with no compose dependencies |

**Key insight:** Database reset and Redis flush together define test isolation. Without both, a cache hit from a previous test's data can make a test pass incorrectly — the worst kind of false positive.

---

## Common Pitfalls

### Pitfall 1: MediatR Version Mismatch Between Blog.Domain and Blog.Application

**What goes wrong:** Blog.Domain references `MediatR 12.*` (set in Phase 1). Blog.Application adds `MediatR 14.1.0`. NuGet resolves the higher version for all projects, but the `12.*` wildcard in Blog.Domain's .csproj may prevent a clean build if the MediatR 14 API has changes that break the `IDomainEvent : INotification` bridge.

**Why it happens:** MediatR has had API changes between major versions. Version 12 removed the automatic scanning extension (`MediatR.Extensions.Microsoft.DependencyInjection` was merged into the core package). Version 14 added .NET 10 support.

**How to avoid:** Update Blog.Domain's .csproj to reference `MediatR 14.1.0` explicitly (not `12.*`) before adding Blog.Application. The `IDomainEvent : INotification` interface is stable — no breaking change between 12 and 14 for this pattern.

**Warning signs:** Build error mentioning `INotification` type resolution conflict or missing type.

### Pitfall 2: AggregateRoot<TId> Generic Scan in UnitOfWork

**What goes wrong:** `ChangeTracker.Entries<AggregateRoot<Guid>>()` only returns entries whose concrete type inherits `AggregateRoot<Guid>`. If an aggregate uses a different key type (e.g., `AggregateRoot<int>`), its events are silently skipped.

**Why it happens:** Generic covariance — `AggregateRoot<int>` does not inherit from `AggregateRoot<Guid>`.

**How to avoid:** For Phase 2, all aggregates (Tag, Post, Comment, User) use `AggregateRoot<Guid>` — confirmed in Phase 1 code. The Guid scan is correct. Document this assumption in UnitOfWork with a comment.

**Warning signs:** Domain events for an aggregate never fire; cache not invalidated after a command.

### Pitfall 3: Respawn Wipes EF Core Migrations History

**What goes wrong:** `__EFMigrationsHistory` table is deleted by Respawn. On the next test, `MigrateAsync()` sees an empty history and re-runs all migrations, failing with "table already exists."

**Why it happens:** Respawn deletes all tables it finds unless explicitly excluded.

**How to avoid:** Always include `TablesToIgnore = new Table[] { "__EFMigrationsHistory" }` in `RespawnerOptions`. Migrations are applied once at fixture init, not per test.

**Warning signs:** Integration test suite passes individually but fails when run together; PostgreSQL exceptions about duplicate table creation.

### Pitfall 4: WebApplicationFactory<Program> Compile Error

**What goes wrong:** `WebApplicationFactory<Program>` in the test project throws a compile-time error: `'Program' is inaccessible due to its protection level`.

**Why it happens:** Top-level statements in Program.cs compile to an `internal class Program`. Test projects in a separate assembly cannot access internal types.

**How to avoid:** Add `public partial class Program { }` at the very bottom of Program.cs. This is a one-line change that makes the type public across assembly boundaries.

**Warning signs:** CS0122 compiler error in Blog.IntegrationTests.csproj.

### Pitfall 5: Cache Hit in Test Serving Stale Data from Previous Test

**What goes wrong:** Test A seeds a tag and calls GetTagListQuery — response is cached. Test B calls ResetDatabaseAsync (Respawn deletes DB rows) but does NOT flush Redis. Test B calls GetTagListQuery — gets cached response with Test A's data, not an empty list.

**Why it happens:** Respawn only resets the relational database. Redis is a separate store.

**How to avoid:** `ResetDatabaseAsync()` in the test fixture must flush Redis via `IServer.FlushDatabaseAsync()` in addition to Respawn. See Pattern 7 above.

**Warning signs:** Tests pass individually but fail when run in sequence; GetTagListQuery returns more items than seeded in the current test.

### Pitfall 6: FluentValidation 12 Breaking Change — AbstractValidator<T>

**What goes wrong:** FluentValidation 12 changed the default cascade behavior and some validator configuration APIs from v11.

**Why it happens:** FluentValidation 12 is a major version bump. If any existing team documentation references v11 patterns, the upgrade may behave differently.

**How to avoid:** Use `AbstractValidator<T>` and `RuleFor()` as documented for v12. The `DependencyInjectionExtensions` package provides `AddValidatorsFromAssembly()` which auto-registers all validators. Do not use the deprecated `FluentValidation.AspNetCore` package (stuck at 11.3.1) — use `FluentValidation.DependencyInjectionExtensions` 12.1.1 instead.

**Warning signs:** Validators not being picked up; validation not triggering in the pipeline.

---

## Code Examples

### GetTagListQuery (Primary Pipeline Test Vehicle)

```csharp
// Blog.Application/Features/Tags/Queries/GetTagList/GetTagListQuery.cs
public record GetTagListQuery : IRequest<IReadOnlyList<TagDto>>, ICacheableQuery, IAllowAnonymous
{
    public string CacheKey => "tag:list:all";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}

// GetTagListQueryHandler.cs
public class GetTagListQueryHandler(BlogDbContext context)
    : IRequestHandler<GetTagListQuery, IReadOnlyList<TagDto>>
{
    public async Task<IReadOnlyList<TagDto>> Handle(
        GetTagListQuery request, CancellationToken ct)
    {
        return await context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Slug.Value))
            .ToListAsync(ct);
    }
}
```

### CreateTagCommand (Write-Side Test Vehicle)

```csharp
// Blog.Application/Features/Tags/Commands/CreateTag/CreateTagCommand.cs
public record CreateTagCommand(string Name) : IRequest<TagDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "Editor" };
}

// CreateTagCommandHandler.cs
public class CreateTagCommandHandler(ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<CreateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var slug = Slug.Create(request.Name);
        var tag = Tag.Create(request.Name, slug);
        await tags.AddAsync(tag, ct);
        await uow.CommitAsync(ct);  // SaveChanges + domain event dispatch
        return new TagDto(tag.Id, tag.Name, tag.Slug.Value);
    }
}

// CreateTagCommandValidator.cs
public class CreateTagCommandValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required")
            .MaximumLength(100).WithMessage("Tag name must not exceed 100 characters");
    }
}
```

### TagCreatedEvent Cache Invalidation Handler

```csharp
// Blog.Application/Features/Tags/EventHandlers/TagCreatedCacheInvalidationHandler.cs
public class TagCreatedCacheInvalidationHandler(IRedisCacheService cache)
    : INotificationHandler<TagCreatedEvent>
{
    public async Task Handle(TagCreatedEvent notification, CancellationToken ct)
    {
        await cache.RemoveByPatternAsync("tag:list:*", ct);
    }
}
```

### IRedisCacheService Interface

```csharp
// Blog.Application/Abstractions/IRedisCacheService.cs
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

### Result<T>

```csharp
// Blog.Application/Common/Result.cs
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error, string? code = null)
        { IsSuccess = false; Error = error; ErrorCode = code; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error, string? code = null) => new(error, code);
}
```

### AddBlogInfrastructure Extension Method

```csharp
// Blog.Infrastructure/DependencyInjection.cs
public static IServiceCollection AddBlogInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // EF Core (BlogDbContext already registered in Program.cs Phase 1 — keep there or move here)
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<ITagRepository, TagRepository>();
    services.AddScoped<IPostRepository, PostRepository>();
    services.AddScoped<ICommentRepository, CommentRepository>();
    services.AddScoped<IUserRepository, UserRepository>();

    // Redis
    services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
    services.AddScoped<IRedisCacheService, RedisCacheService>();

    // Application abstractions
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddSingleton<IDateTimeService, DateTimeService>();
    services.AddScoped<IEmailService, NoOpEmailService>();
    services.AddScoped<IStorageService, NoOpStorageService>();

    // HTTP context accessor (needed by CurrentUserService)
    services.AddHttpContextAccessor();

    return services;
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `MediatR.Extensions.Microsoft.DependencyInjection` separate package | Merged into MediatR core | MediatR 12 | `AddMediatR()` is now in the core package; no separate DI extension package needed |
| `services.AddTransient(typeof(IPipelineBehavior<,>), ...)` | `cfg.AddOpenBehavior(typeof(...))` inside `AddMediatR()` | MediatR 12 | Cleaner, co-located registration; order within the cfg lambda is explicit |
| Respawn `Checkpoint` class | Respawn `Respawner` class with `Respawner.CreateAsync()` | Respawn 7.0 | API changed; `new Checkpoint()` pattern is obsolete |
| FluentValidation.AspNetCore for DI | FluentValidation.DependencyInjectionExtensions | FV 12 | AspNetCore package frozen at 11.3.1; DI extensions is the correct package for v12+ |
| `new Respawner(options)` synchronous | `await Respawner.CreateAsync(conn, options)` | Respawn 5+ | Async init required; build-time connection inspection needs await |

**Deprecated/outdated:**
- `MediatR.Extensions.Microsoft.DependencyInjection`: No longer a separate package since MediatR 12. If seen in any project.json, remove it.
- `new Checkpoint()` Respawn API: Removed in Respawn 7.0. Use `Respawner.CreateAsync()`.
- `FluentValidation.AspNetCore`: Frozen at 11.3.1, not compatible with FV 12 validators. Use `FluentValidation.DependencyInjectionExtensions` only.

---

## Open Questions

1. **MediatR behavior resolution order: first-registered = first in IEnumerable?**
   - What we know: MediatR uses `IEnumerable<IPipelineBehavior<TRequest, TResponse>>` resolved from DI. ASP.NET Core DI returns registrations in registration order (first registered = first in the list). MediatR then wraps them from the end of the list inward, so the last in the list is the innermost wrapper (runs last before the handler). This means the first-registered behavior is the outermost — it runs first.
   - What's unclear: Whether `AddOpenBehavior` within the `AddMediatR(cfg => ...)` lambda preserves the call order vs. collecting them and adding in bulk.
   - Recommendation: Write a one-line smoke test in SC1 that sends a real request through a minimal service provider to confirm execution order observationally, then lock in the arch test against the confirmed order.

2. **Respawn TablesToIgnore for PostgreSQL — string vs Table type**
   - What we know: Respawn 7.0 `TablesToIgnore` accepts `Table[]`. `Table` has a constructor taking table name (string) and optional schema.
   - What's unclear: Whether `new Table("__EFMigrationsHistory")` or `new Table("public", "__EFMigrationsHistory")` is needed for PostgreSQL schema qualification.
   - Recommendation: Use `new Table("public", "__EFMigrationsHistory")` to be explicit about schema. Test by running the fixture once and confirming migrations do not re-run on second test.

3. **IdentityDbContext in Phase 2 — does it need to exist yet?**
   - What we know: IUnitOfWork in Phase 2 is defined but its cross-context usage (Register, Ban) is Phase 3. The `UnitOfWork.CommitAsync()` only operates on BlogDbContext in Phase 2.
   - What's unclear: Whether the `cross-context-transaction` skill's `UnitOfWork` pattern (which takes both contexts) should be partially implemented now or as a stub.
   - Recommendation: Implement IUnitOfWork with BlogDbContext-only in Phase 2. Add the `ExecuteAsync(Func<IdentityDbContext, BlogDbContext, Task>)` overload as a Phase 3 concern. The interface in Blog.Application should not reference `IdentityDbContext` (Infrastructure type) — keep it as `CommitAsync()` only.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none — conventions only |
| Quick run command | `dotnet test tests/Blog.ArchTests` or `dotnet test tests/Blog.IntegrationTests --filter "Category=Smoke"` |
| Full suite command | `dotnet test` (all test projects) |

### Phase Requirements to Test Map

Phase 2 has no standalone requirement IDs. Success criteria map to test scenarios:

| SC | Behavior | Test Type | Project | Automated Command |
|----|----------|-----------|---------|-------------------|
| SC1 | MediatR pipeline executes in order: Validation → Logging → Authorization → Caching | Arch (DI reflection) | Blog.ArchTests | `dotnet test tests/Blog.ArchTests` |
| SC2 | IUnitOfWork rolls back both DbContexts on failure | Integration | Blog.IntegrationTests | `dotnet test tests/Blog.IntegrationTests --filter "UnitOfWork"` |
| SC3 | GetTagListQuery served from Redis on second call | Integration | Blog.IntegrationTests | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` |
| SC4 | CreateTagCommand fires TagCreatedEvent which invalidates tag:list:* | Integration | Blog.IntegrationTests | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` |
| SC5 | Lua SCAN+DEL script clears correct key patterns | Integration | Blog.IntegrationTests | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` |

### Sampling Rate
- Per task commit: `dotnet test tests/Blog.ArchTests` (fast — no containers)
- Per wave merge: `dotnet test` (all test projects)
- Phase gate: Full suite green before `/gsd:verify-work`

### Wave 0 Gaps (files that must exist before SC tests can run)

- [ ] `tests/Blog.IntegrationTests/Blog.IntegrationTests.csproj` — new project, does not exist yet
- [ ] `tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs` — WebApplicationFactory subclass
- [ ] `tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs` — Testcontainers + Respawn
- [ ] `tests/Blog.IntegrationTests/Fixtures/IntegrationTestCollection.cs` — collection definition
- [ ] `tests/Blog.IntegrationTests/Helpers/JwtTokenHelper.cs` — GenerateJwt(role)
- [ ] `apps/blog-api/src/Blog.Application/Blog.Application.csproj` — new project, does not exist yet
- [ ] `apps/blog-api/src/Blog.API/Program.cs` — needs `public partial class Program {}` added

---

## Sources

### Primary (HIGH confidence)
- NuGet Gallery — MediatR 14.1.0: https://www.nuget.org/packages/MediatR
- NuGet Gallery — FluentValidation 12.1.1: https://www.nuget.org/packages/fluentvalidation/
- NuGet Gallery — FluentValidation.DependencyInjectionExtensions 12.1.1: https://www.nuget.org/packages/fluentvalidation.dependencyinjectionextensions/
- NuGet Gallery — StackExchange.Redis 2.12.1: https://www.nuget.org/packages/StackExchange.Redis/
- NuGet Gallery — Testcontainers.PostgreSql 4.11.0: https://www.nuget.org/packages/Testcontainers.PostgreSql
- NuGet Gallery — Testcontainers.Redis 4.10.0: https://www.nuget.org/packages/Testcontainers.Redis
- NuGet Gallery — Respawn 7.0.0: https://www.nuget.org/packages/respawn
- Jimmy Bogard — AutoMapper 16 + MediatR 14 release blog: https://www.jimmybogard.com/automapper-16-0-0-and-mediatr-14-0-0-released-with-net-10-support/
- .claude/skills/add-cacheable-query/SKILL.md — ICacheableQuery interface + CachingBehavior pattern (project-verified)
- .claude/skills/cross-context-transaction/SKILL.md — IUnitOfWork interface and UnitOfWork implementation pattern (project-verified)
- .claude/skills/add-mediator-handler/SKILL.md — CQRS handler pattern, IAuthorizedRequest, IAllowAnonymous (project-verified)
- .claude/skills/add-integration-test/SKILL.md — IntegrationTestFixture, collection fixture, Testcontainers pattern (project-verified)

### Secondary (MEDIUM confidence)
- Respawn GitHub README (verified via NuGet release page): `Respawner.CreateAsync()` API, `RespawnerOptions`, `DbAdapter.Postgres`, `TablesToIgnore`
- StackExchange.Redis Scripting docs: `ScriptEvaluateAsync` with `values:` for ARGV, automatic EVALSHA caching
- Microsoft Learn — Test Minimal APIs: `public partial class Program {}` requirement for WebApplicationFactory access
- MediatR GitHub issues #399, #879, #944 — `AddOpenBehavior` registration order = execution order (first registered = first executed)

### Tertiary (LOW confidence — needs runtime validation)
- MediatR behavior resolution order (first in IEnumerable = first executed): Inferred from DI container behavior + MediatR source; marked in Open Questions for runtime validation in SC1 test

---

## Metadata

**Confidence breakdown:**
- Standard stack versions: HIGH — verified directly from NuGet gallery
- MediatR 14 pipeline registration: HIGH — consistent across multiple sources; minor uncertainty on exact GetServices() ordering documented in Open Questions
- Respawn 7 API: HIGH — multiple sources confirm `Respawner.CreateAsync()` and `RespawnerOptions`
- Testcontainers 4.x patterns: HIGH — official NuGet and skill file consistent
- Lua SCAN+DEL script: HIGH — Redis official docs + StackExchange.Redis scripting docs
- WebApplicationFactory partial Program: HIGH — Microsoft Learn + multiple community sources

**Research date:** 2026-03-16
**Valid until:** 2026-07-16 (MediatR and FluentValidation are stable; Testcontainers modules less frequent releases)
