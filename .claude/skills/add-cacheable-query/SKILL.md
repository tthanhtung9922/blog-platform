# add-cacheable-query

Implement an opt-in cacheable query using the ICacheableQuery pattern (ADR-008) with proper cache key conventions, TTL, and Domain Event-based cache invalidation.

## Arguments

- `entity` (required) — The entity being queried (e.g., `Post`, `Comment`, `User`)
- `query-type` (required) — Type of query: `by-id`, `by-slug`, `list`, `by-tag`, `by-author`, `by-post`
- `ttl` (optional) — Cache TTL override (defaults to convention-based)

## Instructions

You are implementing a cacheable query for the blog-platform. Caching is **opt-in** via `ICacheableQuery` (ADR-008). The `CachingBehavior` in the MediatR pipeline only caches queries that explicitly implement this interface.

### Step 1 — Create the Query with ICacheableQuery

**Location:** `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/Queries/{QueryName}/`

```csharp
// ICacheableQuery interface
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }  // null = use default TTL (5 min)
}

// Example: GetPostBySlugQuery
public record GetPostBySlugQuery(string Slug) : IRequest<PostDetailDto>, ICacheableQuery
{
    public string CacheKey => $"post:slug:{Slug}";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}

// Example: GetPostListQuery
public record GetPostListQuery(int Page, int PageSize) : IRequest<PaginatedList<PostDto>>, ICacheableQuery
{
    public string CacheKey => $"post-list:page:{Page}:size:{PageSize}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}
```

### Cache Key Conventions

Follow these exact patterns:

| Query | Cache Key Pattern | TTL |
|---|---|---|
| Post by slug | `post:slug:{slug}` | 1 hour |
| Post by ID | `post:id:{id}` | 1 hour |
| Post list (paginated) | `post-list:page:{p}:size:{s}` | 5 min |
| Posts by tag | `post-list:tag:{tagSlug}:{page}` | 5 min |
| Posts by author | `post-list:author:{authorId}:{page}` | 5 min |
| User profile | `user:profile:{username}` | 30 min |
| Comments by post | `comments:post:{postId}:{page}` | 2 min |

### Step 2 — CachingBehavior (already exists in pipeline)

The `CachingBehavior` handles cache-aside automatically:

```csharp
// apps/blog-api/src/Blog.Application/Behaviors/CachingBehavior.cs
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRedisCacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Only cache if the request opts in
        if (request is not ICacheableQuery cacheable)
            return await next();

        // Try cache first
        var cached = await _cache.GetAsync<TResponse>(cacheable.CacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache HIT: {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        // Cache miss — execute query
        _logger.LogDebug("Cache MISS: {CacheKey}", cacheable.CacheKey);
        var result = await next();

        // Store in cache
        var ttl = cacheable.CacheDuration ?? TimeSpan.FromMinutes(5);
        await _cache.SetAsync(cacheable.CacheKey, result, ttl, ct);

        return result;
    }
}
```

**Pipeline order:** Validation → Logging → Authorization → **Caching**

### Step 3 — Cache Invalidation via Domain Events

When data changes, cached entries must be invalidated. This is done through Domain Event handlers.

**Cache Invalidation Map:**

| Domain Event | Keys Invalidated |
|---|---|
| `PostPublishedEvent` | `post:slug:*`, `post:id:*`, `post-list:*` |
| `PostUpdatedEvent` | `post:slug:{slug}`, `post:id:{id}` |
| `PostArchivedEvent` | `post:slug:*`, `post:id:*`, `post-list:*` |
| `CommentAddedEvent` | `comments:post:{postId}:*` |
| `UserProfileUpdatedEvent` | `user:profile:{username}` |

**Domain Event Handler for cache invalidation:**

```csharp
// apps/blog-api/src/Blog.Application/Features/Posts/EventHandlers/PostPublishedCacheInvalidationHandler.cs
public class PostPublishedCacheInvalidationHandler : INotificationHandler<PostPublishedEvent>
{
    private readonly IRedisCacheService _cache;

    public PostPublishedCacheInvalidationHandler(IRedisCacheService cache)
    {
        _cache = cache;
    }

    public async Task Handle(PostPublishedEvent notification, CancellationToken ct)
    {
        // Invalidate specific post cache
        await _cache.RemoveByPatternAsync("post:slug:*", ct);
        await _cache.RemoveByPatternAsync("post:id:*", ct);

        // Invalidate all list caches (new post appears in lists)
        await _cache.RemoveByPatternAsync("post-list:*", ct);
    }
}
```

### Queries that MUST NOT be cached

These queries must NOT implement `ICacheableQuery`:

- `GetCurrentUserQuery` — Must always return real-time user data
- `GetUserListQuery` (admin) — Must show real-time user status
- Any query that returns user-specific data that varies per request

### Step 4 — Redis Cache Service Interface

```csharp
// apps/blog-api/src/Blog.Application/Abstractions/IRedisCacheService.cs
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

**Implementation location:** `apps/blog-api/src/Blog.Infrastructure/Caching/RedisCacheService.cs`

### Performance Budget Reference

| Endpoint | Cached P95 | Uncached P95 | Cache TTL |
|---|---|---|---|
| `GET /posts` | ≤ 30ms | ≤ 150ms | 5 min |
| `GET /posts/{slug}` | ≤ 20ms | ≤ 100ms | 1 hour |
| `GET /posts/{id}/comments` | ≤ 25ms | ≤ 120ms | 2 min |
| `GET /users/{username}` | ≤ 20ms | ≤ 80ms | 30 min |
