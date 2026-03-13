# add-mediator-handler

Create a CQRS command or query handler with MediatR, including the request record, handler class, and FluentValidation validator, following the pipeline behavior order.

## Arguments

- `name` (required) — Handler name (e.g., `CreatePost`, `GetPostBySlug`, `PublishPost`)
- `type` (required) — `command` or `query`
- `entity` (required) — Domain entity (e.g., `Post`, `Comment`, `User`)
- `response` (optional) — Response type (e.g., `PostDto`, `PostDetailDto`, `Guid`). Defaults to `{Entity}Dto` for queries, `Guid` for create commands, `Unit` for void commands
- `authorized` (optional) — Whether to implement `IAuthorizedRequest` with roles
- `cacheable` (optional) — Whether to implement `ICacheableQuery` (queries only)

## Instructions

You are creating a MediatR CQRS handler for the blog-platform. Each handler consists of 2-3 files in a dedicated folder.

### File Structure

**Commands:** `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/Commands/{ActionName}/`
**Queries:** `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/Queries/{ActionName}/`

Each folder contains:
```
{ActionName}/
├── {ActionName}Command.cs (or Query.cs)     ← Request record
├── {ActionName}CommandHandler.cs             ← Handler class
└── {ActionName}CommandValidator.cs           ← FluentValidation (commands only, optional for queries)
```

### Command Pattern

```csharp
// CreatePostCommand.cs
public record CreatePostCommand(
    string Title,
    string? Excerpt,
    object BodyJson,
    string? CoverImageUrl,
    List<Guid>? TagIds
) : IRequest<PostDto>;

// CreatePostCommandHandler.cs
public class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, PostDto>
{
    private readonly IPostRepository _postRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public CreatePostCommandHandler(
        IPostRepository postRepository,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _postRepository = postRepository;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<PostDto> Handle(CreatePostCommand request, CancellationToken ct)
    {
        // 1. Create domain entity via factory method
        var slug = Slug.Create(request.Title);
        var post = Post.Create(_currentUser.UserId, request.Title, slug);

        // 2. Set optional properties
        if (request.Excerpt is not null)
            post.SetExcerpt(request.Excerpt);

        // 3. Persist
        await _postRepository.AddAsync(post, ct);

        // 4. Domain events are dispatched automatically by SaveChanges interceptor

        // 5. Map to DTO and return
        return new PostDto
        {
            Id = post.Id,
            Title = post.Title,
            Slug = post.Slug.Value,
            // ... map other properties
        };
    }
}

// CreatePostCommandValidator.cs
public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title must not exceed 256 characters");

        RuleFor(x => x.Excerpt)
            .MaximumLength(512).WithMessage("Excerpt must not exceed 512 characters")
            .When(x => x.Excerpt is not null);

        RuleFor(x => x.BodyJson)
            .NotNull().WithMessage("Content body is required");

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Cover image must be a valid URL")
            .When(x => x.CoverImageUrl is not null);
    }
}
```

### Query Pattern

```csharp
// GetPostBySlugQuery.cs
public record GetPostBySlugQuery(string Slug) : IRequest<PostDetailDto>;

// GetPostBySlugQueryHandler.cs
public class GetPostBySlugQueryHandler : IRequestHandler<GetPostBySlugQuery, PostDetailDto>
{
    private readonly BlogDbContext _context;  // Queries can use DbContext directly for reads

    public GetPostBySlugQueryHandler(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<PostDetailDto> Handle(GetPostBySlugQuery request, CancellationToken ct)
    {
        var post = await _context.Posts
            .AsNoTracking()
            .Include(p => p.Contents)
            .Include(p => p.Author)
            .Where(p => p.Slug == request.Slug && p.Status == PostStatus.Published)
            .Select(p => new PostDetailDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug.Value,
                // ... projection
            })
            .FirstOrDefaultAsync(ct);

        if (post is null)
            throw new PostNotFoundException(request.Slug);

        return post;
    }
}
```

### Adding Authorization (IAuthorizedRequest)

```csharp
public record PublishPostCommand(Guid PostId) : IRequest<PostDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "Editor" };
    public Guid? ResourceOwnerId => null;
}
```

### Adding Caching (ICacheableQuery)

```csharp
public record GetPostBySlugQuery(string Slug) : IRequest<PostDetailDto>, ICacheableQuery
{
    public string CacheKey => $"post:slug:{Slug}";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}
```

### MediatR Pipeline Behavior Order

All requests pass through these behaviors in this exact order:
1. **ValidationBehavior** — Runs FluentValidation, throws 422 on failure
2. **LoggingBehavior** — Logs request name, duration, user context
3. **AuthorizationBehavior** — Checks `IAuthorizedRequest` roles, throws 403 on failure
4. **CachingBehavior** — Cache-aside for `ICacheableQuery`, bypasses handler on cache hit

### Naming Conventions

| Type | Pattern | Example |
|---|---|---|
| Create command | `Create{Entity}Command` | `CreatePostCommand` |
| Update command | `Update{Entity}Command` | `UpdatePostCommand` |
| Delete command | `Delete{Entity}Command` | `DeletePostCommand` |
| Action command | `{Action}{Entity}Command` | `PublishPostCommand`, `ArchivePostCommand` |
| Get by ID/slug | `Get{Entity}By{Field}Query` | `GetPostBySlugQuery` |
| List query | `Get{Entity}ListQuery` | `GetPostListQuery` |
| Filtered list | `Get{Entity}sBy{Filter}Query` | `GetPostsByTagQuery` |

### Key Rules

1. **Commands modify state** — Use repository methods, raise domain events
2. **Queries are read-only** — Can use `DbContext` directly with `AsNoTracking()` and `Select()` projections
3. **Never inject DbContext into command handlers** — Use repository interface (Domain layer abstraction)
4. **Always use `CancellationToken`** — Pass it through all async calls
5. **Validation errors → 422** — FluentValidation is the single source of input validation
6. **Domain exceptions → appropriate HTTP status** — Map in `ExceptionHandlingMiddleware`
