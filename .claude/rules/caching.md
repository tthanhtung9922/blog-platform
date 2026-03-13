---
description: >
  Apply these rules whenever implementing, modifying, or reviewing any caching logic,
  Redis operations, ICacheableQuery implementations, cache invalidation handlers,
  or CachingBehavior pipeline code. Also applies when adding new queries that might
  benefit from caching.
---

# Caching Rules

## MUST

- **Caching is opt-in only (ADR-008).** A query is cached by `CachingBehavior` only if it implements `ICacheableQuery`. Queries without this interface bypass caching entirely.
  ```csharp
  // Opt-in: this query will be cached
  public record GetPostBySlugQuery(string Slug) : IRequest<PostDto>, ICacheableQuery
  {
      public string CacheKey => $"post:slug:{Slug}";
      public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
  }

  // Not cached: no ICacheableQuery
  public record GetCurrentUserQuery : IRequest<CurrentUserDto> { }
  ```
- **Cache keys follow the convention defined in `CacheKeys.cs`:**
  ```
  post:slug:{slug}              — 1 hour TTL
  post:id:{id}                  — 1 hour TTL
  post-list:page:{p}:size:{s}   — 5 min TTL
  post-list:tag:{tag}:{p}       — 5 min TTL
  post-list:author:{id}:{p}     — 5 min TTL
  user:profile:{username}       — 30 min TTL
  comments:post:{postId}:{p}    — 2 min TTL
  ```
- **Cache invalidation is triggered by Domain Events**, not manually in command handlers. Each Domain Event has a corresponding `INotificationHandler` that calls `IRedisCacheService.RemoveByPatternAsync(pattern)`.
  ```
  PostPublishedEvent  → invalidate post:slug:*, post:id:*, post-list:*
  PostUpdatedEvent    → invalidate post:slug:{slug}, post:id:{id}
  CommentAddedEvent   → invalidate comments:post:{postId}:*
  UserProfileUpdated  → invalidate user:profile:{username}
  ```
- **Wildcard invalidation uses Lua scripts** for atomicity — `SCAN` + `DEL` pattern, never blocking `KEYS` command.

## SHOULD

- Keep cache TTLs short for frequently-changing data (comments: 2 min) and longer for stable data (post detail: 1 hour).
- When adding a new cacheable query, add its key pattern to `CacheKeys.cs` and update the invalidation map in the relevant Domain Event handler.

## NEVER

- Never use Redis `KEYS *` command in production — it blocks the server.
  ```
  KEYS post:*           // NEVER — blocking O(N) scan
  SCAN 0 MATCH post:*   // CORRECT — non-blocking cursor-based
  ```
- Never cache `GetCurrentUser` or `GetUserList` (admin) — these must return real-time data.
- Never invalidate cache directly in command handlers — always go through Domain Event notification handlers.
  ```csharp
  // NEVER — in CreatePostCommandHandler
  await _cache.RemoveAsync("post-list:page:1:size:10");

  // CORRECT — in PostPublishedEventHandler (INotificationHandler)
  await _cache.RemoveByPatternAsync("post-list:*");
  ```
- Never cache queries with user-specific side effects or time-dependent results without careful key design.
