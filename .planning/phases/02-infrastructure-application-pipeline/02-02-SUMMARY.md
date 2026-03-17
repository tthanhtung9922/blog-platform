---
phase: 02-infrastructure-application-pipeline
plan: "02"
subsystem: backend-infrastructure
tags: [infrastructure, repositories, unit-of-work, redis, caching, di]
dependency_graph:
  requires: ["02-01"]
  provides: ["IRedisCacheService impl", "IUnitOfWork impl", "4 repository impls", "ICurrentUserService impl", "AddBlogInfrastructure()"]
  affects: ["03-identity-authentication", "all application handlers via DI"]
tech_stack:
  added: ["StackExchange.Redis 2.12.1", "Microsoft.AspNetCore.Authentication.JwtBearer 10.*", "Microsoft.AspNetCore.Http.Abstractions 2.*"]
  patterns: ["Repository pattern", "Unit of Work with domain event dispatch", "Lua SCAN+DEL cache invalidation", "NoOp stub pattern"]
key_files:
  created:
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Repositories/TagRepository.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Repositories/PostRepository.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Repositories/CommentRepository.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Repositories/UserRepository.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/UnitOfWork.cs
    - apps/blog-api/src/Blog.Infrastructure/Caching/RedisCacheService.cs
    - apps/blog-api/src/Blog.Infrastructure/Caching/CacheKeys.cs
    - apps/blog-api/src/Blog.Infrastructure/Services/CurrentUserService.cs
    - apps/blog-api/src/Blog.Infrastructure/Services/DateTimeService.cs
    - apps/blog-api/src/Blog.Infrastructure/Services/NoOp/NoOpEmailService.cs
    - apps/blog-api/src/Blog.Infrastructure/Services/NoOp/NoOpStorageService.cs
    - apps/blog-api/src/Blog.Infrastructure/DependencyInjection.cs
  modified:
    - apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj
decisions:
  - "UnitOfWork clears domain events BEFORE SaveChangesAsync to prevent double-dispatch on retry; handler failures after commit are logged and swallowed (primary write succeeded)"
  - "RedisCacheService.RemoveByPatternAsync uses Lua SCAN+DEL with ScriptEvaluateAsync(values:) — never KEYS which blocks Redis event loop"
  - "NoOpEmailService and NoOpStorageService log debug messages and return Task.CompletedTask / placeholder URL — not exceptions — so Phase 2 works without SMTP or MinIO"
  - "DateTimeService is registered as Singleton (stateless); all repository/cache services are Scoped to match EF Core DbContext lifetime"
metrics:
  duration: "4 min"
  completed: "2026-03-17"
  tasks: 2
  files: 13
---

# Phase 02 Plan 02: Infrastructure Implementations Summary

Blog.Infrastructure fully implemented: 4 EF Core repository implementations + UnitOfWork with collect→clear→save→dispatch domain event pattern + RedisCacheService with Lua SCAN+DEL invalidation + CurrentUserService reading JWT claims + NoOp stubs for email/storage + AddBlogInfrastructure() DI registration.

## Tasks Completed

### Task 1: Update csproj and implement 4 repositories + UnitOfWork
- **Commit:** `51850ff`
- **Files:** 6 (1 modified, 5 created)
- Updated Blog.Infrastructure.csproj: added Blog.Application project reference, StackExchange.Redis 2.12.1, JwtBearer 10.*, HttpAbstractions 2.*
- TagRepository, PostRepository, CommentRepository, UserRepository — each wraps BlogDbContext with EF Core calls; list queries use AsNoTracking()
- PostRepository.GetPublishedAsync returns `(Items, TotalCount)` tuple for pagination; filters by `PostStatus.Published`, orders by `PublishedAt` descending
- UnitOfWork: ChangeTracker.Entries<AggregateRoot<Guid>>() scans tracked entities, clears events before save, dispatches after SaveChangesAsync succeeds

### Task 2: RedisCacheService, CacheKeys, services, and AddBlogInfrastructure()
- **Commit:** `a3d7320`
- **Files:** 7 (7 created)
- RedisCacheService: GetAsync with JSON deserialization + null/exception guard; SetAsync with TTL; RemoveAsync key delete; RemoveByPatternAsync via Lua SCAN+DEL
- CacheKeys: static key builder methods + Patterns nested class with wildcard constants for Domain Event handlers
- CurrentUserService: reads ClaimTypes.NameIdentifier (Guid) + ClaimTypes.Role from IHttpContextAccessor
- DateTimeService: thin wrapper over DateTimeOffset.UtcNow
- NoOpEmailService, NoOpStorageService: log debug + return gracefully
- DependencyInjection.AddBlogInfrastructure(): wires all repos, UoW, Redis singleton multiplexer, and all service implementations

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed ambiguous JsonSerializer.Deserialize overload for RedisValue**
- **Found during:** Task 2 build verification
- **Issue:** `JsonSerializer.Deserialize<T>(value!)` where `value` is a `RedisValue` — C# compiler could not resolve between `Deserialize(ReadOnlySpan<byte>, ...)` and `Deserialize(string, ...)` overloads (CS0121)
- **Fix:** Added explicit `(string)` cast: `JsonSerializer.Deserialize<T>((string)value!)`
- **Files modified:** `apps/blog-api/src/Blog.Infrastructure/Caching/RedisCacheService.cs`
- **Commit:** included in `a3d7320`

## Verification Results

- `dotnet build Blog.Infrastructure.csproj` exits 0 with 0 errors, 0 warnings
- UnitOfWork line order confirmed: `ClearDomainEvents()` (line 30) → `SaveChangesAsync` (line 33) → `publisher.Publish` (line 42)
- RedisCacheService uses `SCAN` in Lua script, `ScriptEvaluateAsync` with `values:` param — no `KEYS` command
- DependencyInjection.cs registers all 4 repos, IUnitOfWork, IConnectionMultiplexer (singleton), IRedisCacheService, ICurrentUserService, IDateTimeService, IEmailService, IStorageService

## Self-Check: PASSED

Files verified present:
- `apps/blog-api/src/Blog.Infrastructure/Persistence/UnitOfWork.cs` — FOUND
- `apps/blog-api/src/Blog.Infrastructure/Caching/RedisCacheService.cs` — FOUND
- `apps/blog-api/src/Blog.Infrastructure/Caching/CacheKeys.cs` — FOUND
- `apps/blog-api/src/Blog.Infrastructure/DependencyInjection.cs` — FOUND
- `apps/blog-api/src/Blog.Infrastructure/Services/CurrentUserService.cs` — FOUND

Commits verified: `51850ff` and `a3d7320` both present in git log.
