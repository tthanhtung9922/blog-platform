# add-domain-entity

Scaffold a DDD aggregate root, entity, or value object in the Domain layer with proper patterns, validation, domain events, and repository interface.

## Arguments

- `name` (required) — Entity/VO name in PascalCase (e.g., `Post`, `Slug`, `ReadingTime`)
- `type` (required) — One of: `aggregate`, `entity`, `value-object`, `enum`
- `parent` (optional) — Parent aggregate name if creating a child entity (e.g., `Post` for `PostContent`)
- `properties` (optional) — Comma-separated property list (e.g., `title:string,slug:Slug,status:PostStatus`)

## Instructions

You are creating a DDD domain model element for the blog-platform. The Domain layer has NO dependencies on Infrastructure or Application layers. It contains only pure business logic.

### File Locations

```
apps/blog-api/src/Blog.Domain/
├── Aggregates/{EntityPlural}/
│   ├── {Entity}.cs              ← Aggregate root or child entity
│   └── {Entity}Status.cs        ← Enum (if needed)
├── ValueObjects/
│   └── {ValueObject}.cs         ← Value objects
├── DomainEvents/
│   └── {Event}Event.cs          ← Domain events
├── Repositories/
│   └── I{Entity}Repository.cs   ← Interface only (no implementation)
├── Services/
│   └── {Service}Service.cs      ← Stateless domain services
└── Exceptions/
    └── {Entity}NotFoundException.cs
```

### Creating an Aggregate Root

Aggregate roots are the primary entities that own a consistency boundary. They:
- Have a UUID `Id` property
- Contain factory methods for creation (no public constructors for mutation)
- Raise domain events
- Enforce invariants

```csharp
public class Post
{
    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public string? Excerpt { get; private set; }
    public PostStatus Status { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public ReadingTime? ReadingTime { get; private set; }
    public bool IsFeatured { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation — child entities within the aggregate boundary
    private readonly List<PostContent> _contents = new();
    public IReadOnlyList<PostContent> Contents => _contents.AsReadOnly();

    // Domain events
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Post() { } // EF Core constructor

    // Factory method
    public static Post Create(Guid authorId, string title, Slug slug)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Title = title,
            Slug = slug,
            Status = PostStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        post._domainEvents.Add(new PostCreatedEvent(post.Id));
        return post;
    }

    // Behavior methods
    public void Publish()
    {
        if (Status != PostStatus.Draft)
            throw new InvalidOperationException("Only draft posts can be published.");

        Status = PostStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new PostPublishedEvent(Id));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### Creating a Child Entity

Child entities belong to an aggregate and don't have their own repository. They are accessed through the aggregate root.

```csharp
public class PostContent
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public object BodyJson { get; private set; } = null!;  // Tiptap v3 ProseMirror JSON
    public string BodyHtml { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    private PostContent() { }

    internal static PostContent Create(Guid postId, object bodyJson, string bodyHtml)
    {
        return new PostContent
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            BodyJson = bodyJson,
            BodyHtml = bodyHtml,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
```

### Creating a Value Object

Value objects are immutable, compared by value, and contain validation in their constructor/factory.

```csharp
public class Slug : ValueObject
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty for slug generation.");

        var slug = title
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("đ", "d")       // Vietnamese-specific
            .Replace("Đ", "d");

        // Remove diacritics, special chars, etc.
        slug = RemoveDiacritics(slug);
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');

        return new Slug(slug);
    }

    public static Slug FromExisting(string slug) => new(slug);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}

// Base class for value objects
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) => HashCode.Combine(current, obj));
    }
}
```

### Creating a Domain Event

Domain events signal that something meaningful happened. They trigger cache invalidation and cross-aggregate side effects.

```csharp
// apps/blog-api/src/Blog.Domain/DomainEvents/{EventName}Event.cs
public record PostPublishedEvent(Guid PostId) : IDomainEvent;

public interface IDomainEvent : MediatR.INotification { }
```

### Creating a Repository Interface

Repository interfaces live in Domain. Implementations go in Infrastructure.

```csharp
// apps/blog-api/src/Blog.Domain/Repositories/IPostRepository.cs
public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Post?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Post post, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task DeleteAsync(Post post, CancellationToken ct = default);
}
```

### Creating an Enum

```csharp
// apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostStatus.cs
public enum PostStatus
{
    Draft,
    Published,
    Archived
}
```

### Database Table Mapping Reference

When creating entities, ensure they map to the correct database schema:

- Tables: `snake_case` plural (e.g., `posts`, `post_contents`)
- Columns: `snake_case` (e.g., `created_at`, `is_featured`)
- Primary keys: Always `id` (UUID)
- Foreign keys: `{referenced_table_singular}_id` (e.g., `author_id`, `post_id`)
- Timestamps: Always `TIMESTAMPTZ` with `created_at` and `updated_at`

### Critical Rules

1. **No Infrastructure dependencies** — Domain layer must not reference EF Core, Redis, or any external library (except MediatR for `INotification`)
2. **Private setters** — All properties use `private set` to enforce invariants through methods
3. **Factory methods** — Use `static Create(...)` instead of public constructors for aggregate roots
4. **Parameterless private constructor** — Required for EF Core materialization: `private Post() { }`
5. **Domain events list** — Aggregate roots maintain `_domainEvents` list, cleared after persistence
6. **IdentityUser vs User** — These are SEPARATE models (ADR-006). `User` (Domain) and `IdentityUser` (Infrastructure) share only a GUID ID. Never inherit or reference IdentityUser from Domain.
