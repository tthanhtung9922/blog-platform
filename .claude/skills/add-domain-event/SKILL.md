# add-domain-event

Create a domain event and its handler(s) for cache invalidation or cross-aggregate side effects, following the event-driven patterns established in the project.

## Arguments

- `name` (required) — Event name without "Event" suffix (e.g., `PostPublished`, `CommentAdded`, `UserRegistered`)
- `entity` (required) — The aggregate that raises this event (e.g., `Post`, `Comment`, `User`)
- `properties` (optional) — Event payload properties (e.g., `PostId:Guid,AuthorId:Guid`)
- `handlers` (optional) — Comma-separated handler purposes: `cache-invalidation`, `notification`, `cross-aggregate`

## Instructions

You are creating a domain event and its handlers for the blog-platform. Domain events signal that something meaningful happened in the domain and trigger side effects like cache invalidation, notifications, or cross-aggregate updates.

### Step 1 — Create the Domain Event

**Location:** `apps/blog-api/src/Blog.Domain/DomainEvents/{EventName}Event.cs`

```csharp
public record PostPublishedEvent(Guid PostId) : IDomainEvent;

public record CommentAddedEvent(Guid CommentId, Guid PostId, Guid AuthorId) : IDomainEvent;

public record UserRegisteredEvent(Guid UserId, string Email) : IDomainEvent;

// Base interface
public interface IDomainEvent : MediatR.INotification { }
```

### Step 2 — Raise the Event from the Aggregate

Events are raised inside aggregate methods and collected in the `_domainEvents` list:

```csharp
// In the aggregate root (e.g., Post.cs)
public void Publish()
{
    if (Status != PostStatus.Draft)
        throw new InvalidOperationException("Only draft posts can be published.");

    Status = PostStatus.Published;
    PublishedAt = DateTimeOffset.UtcNow;
    UpdatedAt = DateTimeOffset.UtcNow;

    _domainEvents.Add(new PostPublishedEvent(Id));
}
```

### Step 3 — Create Event Handlers

**Location:** `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/EventHandlers/`

#### Cache Invalidation Handler

```csharp
// PostPublishedCacheInvalidationHandler.cs
public class PostPublishedCacheInvalidationHandler : INotificationHandler<PostPublishedEvent>
{
    private readonly IRedisCacheService _cache;
    private readonly ILogger<PostPublishedCacheInvalidationHandler> _logger;

    public PostPublishedCacheInvalidationHandler(
        IRedisCacheService cache,
        ILogger<PostPublishedCacheInvalidationHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(PostPublishedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation("Invalidating caches for PostPublished: {PostId}", notification.PostId);

        await _cache.RemoveByPatternAsync("post:slug:*", ct);
        await _cache.RemoveByPatternAsync("post:id:*", ct);
        await _cache.RemoveByPatternAsync("post-list:*", ct);
    }
}
```

#### Cross-Aggregate Side Effect Handler

```csharp
// UserRegisteredCreateDomainUserHandler.cs
public class UserRegisteredCreateDomainUserHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IUserRepository _userRepository;

    public async Task Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        var user = User.Create(notification.UserId, notification.Email);
        await _userRepository.AddAsync(user, ct);
    }
}
```

### Cache Invalidation Map

Use this reference when creating cache invalidation handlers:

| Domain Event | Keys to Invalidate |
|---|---|
| `PostPublishedEvent` | `post:slug:*`, `post:id:*`, `post-list:*` |
| `PostUpdatedEvent` | `post:slug:{slug}`, `post:id:{id}` |
| `PostArchivedEvent` | `post:slug:*`, `post:id:*`, `post-list:*` |
| `CommentAddedEvent` | `comments:post:{postId}:*` |
| `CommentDeletedEvent` | `comments:post:{postId}:*` |
| `UserProfileUpdatedEvent` | `user:profile:{username}` |

### Step 4 — Domain Event Dispatching

Domain events are dispatched automatically after `SaveChangesAsync()` via an interceptor:

```csharp
// apps/blog-api/src/Blog.Infrastructure/Persistence/Interceptors/DomainEventDispatcherInterceptor.cs
public class DomainEventDispatcherInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return result;

        var entities = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IHasDomainEvents)
            .Select(e => (IHasDomainEvents)e.Entity)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();

        // Clear events before dispatching to prevent re-entrance
        foreach (var entity in entities)
            entity.ClearDomainEvents();

        // Dispatch all events
        foreach (var domainEvent in events)
            await _mediator.Publish(domainEvent, ct);

        return result;
    }
}
```

### Existing Domain Events

| Event | Aggregate | Purpose |
|---|---|---|
| `PostCreatedEvent` | Post | Logging, analytics |
| `PostPublishedEvent` | Post | Cache invalidation, RSS feed update |
| `PostUpdatedEvent` | Post | Cache invalidation |
| `PostArchivedEvent` | Post | Cache invalidation, cleanup |
| `CommentAddedEvent` | Comment | Cache invalidation, notification |
| `UserRegisteredEvent` | User | Create domain User from IdentityUser |

### Key Rules

1. **Events are records** — Immutable, contain only IDs and minimal data
2. **Raise in aggregate methods** — Never raise events outside domain logic
3. **Dispatch after SaveChanges** — Events fire AFTER persistence succeeds
4. **Multiple handlers per event** — One for cache invalidation, another for notifications, etc.
5. **Handlers are in Application layer** — Not Domain (they depend on infrastructure abstractions)
6. **No circular dependencies** — Event handlers should not trigger commands that raise the same event
