# add-api-endpoint

Create a complete REST API endpoint spanning all 4 Clean Architecture layers (Domain → Application → Infrastructure → Presentation) with proper CQRS, validation, authorization, and caching patterns.

## Arguments

- `entity` (required) — The domain entity this endpoint operates on (e.g., `Post`, `Comment`, `User`)
- `action` (required) — The operation: `list`, `get`, `create`, `update`, `delete`, or custom action name (e.g., `publish`, `archive`, `moderate`)
- `method` (required) — HTTP method: `GET`, `POST`, `PUT`, `DELETE`
- `route` (optional) — Custom route path override (defaults to convention-based)
- `roles` (optional) — Comma-separated roles that can access: `Admin`, `Editor`, `Author`, `Reader`, `Anonymous`
- `cacheable` (optional) — Whether to implement `ICacheableQuery` (only for GET endpoints)

## Instructions

You are creating a REST API endpoint for the blog-platform. This is an ASP.NET Core 10 backend using Clean Architecture + DDD with CQRS via MediatR.

### Step 1 — Determine the endpoint type

Based on the `action` and `method`, determine whether this is a **Command** (POST/PUT/DELETE — state-changing) or **Query** (GET — read-only).

### Step 2 — Create files in order (bottom-up)

#### Layer 1: Domain (if new entity/event needed)

Only create Domain files if the entity or event doesn't already exist.

**Aggregate/Entity location:** `apps/blog-api/src/Blog.Domain/Aggregates/{EntityPlural}/{Entity}.cs`
**Domain Event location:** `apps/blog-api/src/Blog.Domain/DomainEvents/{EventName}Event.cs`
**Repository interface:** `apps/blog-api/src/Blog.Domain/Repositories/I{Entity}Repository.cs`

#### Layer 2: Application — CQRS Handler

**Commands** go in: `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/Commands/{Action}{Entity}/`
**Queries** go in: `apps/blog-api/src/Blog.Application/Features/{EntityPlural}/Queries/{Action}{Entity}/`

Each folder contains 2-3 files:

**For Commands:**
```csharp
// {Action}{Entity}Command.cs
public record {Action}{Entity}Command({properties}) : IRequest<{ResponseType}>;

// {Action}{Entity}CommandHandler.cs
public class {Action}{Entity}CommandHandler : IRequestHandler<{Action}{Entity}Command, {ResponseType}>
{
    private readonly I{Entity}Repository _repository;
    // inject other dependencies

    public async Task<{ResponseType}> Handle({Action}{Entity}Command request, CancellationToken ct)
    {
        // 1. Load aggregate
        // 2. Execute domain logic
        // 3. Persist changes
        // 4. Raise domain events
        // 5. Return result
    }
}

// {Action}{Entity}CommandValidator.cs
public class {Action}{Entity}CommandValidator : AbstractValidator<{Action}{Entity}Command>
{
    public {Action}{Entity}CommandValidator()
    {
        // FluentValidation rules
    }
}
```

**For Queries:**
```csharp
// {Action}{Entity}Query.cs — optionally implement ICacheableQuery
public record {Action}{Entity}Query({parameters}) : IRequest<{ResponseType}>;

// {Action}{Entity}QueryHandler.cs
public class {Action}{Entity}QueryHandler : IRequestHandler<{Action}{Entity}Query, {ResponseType}>
{
    // Use repository or raw DbContext for read queries
}
```

**If cacheable**, the Query record must implement `ICacheableQuery`:
```csharp
public record GetPostBySlugQuery(string Slug) : IRequest<PostDetailDto>, ICacheableQuery
{
    public string CacheKey => $"post:slug:{Slug}";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}
```

#### Layer 3: Infrastructure (if new repository method needed)

**Repository implementation:** `apps/blog-api/src/Blog.Infrastructure/Persistence/Repositories/{Entity}Repository.cs`

#### Layer 4: Presentation — Controller

**Controller location:** `apps/blog-api/src/Blog.API/Controllers/{EntityPlural}Controller.cs`

Add/update the controller action:

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class {EntityPlural}Controller : ControllerBase
{
    private readonly IMediator _mediator;

    // GET list — paginated
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<{Entity}Dto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new Get{Entity}ListQuery(page, pageSize));
        return Ok(result);
    }

    // POST create — requires authorization
    [HttpPost]
    [Authorize(Policy = "Can{Action}{Entity}")]
    [ProducesResponseType(typeof({Entity}Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] Create{Entity}Command command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }
}
```

### Step 3 — Apply authorization

Based on the `roles` argument, apply authorization at TWO backend layers:

1. **Controller:** `[Authorize(Policy = "...")]` attribute
2. **MediatR AuthorizationBehavior:** Command/Query implements `IAuthorizedRequest` with required role/permission

**RBAC Permission Matrix Reference:**

| Permission | Admin | Editor | Author | Reader |
|---|:---:|:---:|:---:|:---:|
| Read published posts | Yes | Yes | Yes | Yes |
| Create/edit own posts | Yes | Yes | Yes | No |
| Publish posts | Yes | Yes | No | No |
| Delete others' posts | Yes | No | No | No |
| Approve/delete comments | Yes | Yes | No | No |
| Manage users & roles | Yes | No | No | No |
| View analytics | Yes | Yes | Own only | No |
| System settings | Yes | No | No | No |

### Step 4 — Response format

All responses must follow these conventions:

- **Success list:** `{ "items": [...], "totalCount": N, "page": N, "pageSize": N, "totalPages": N }`
- **Success single:** Return the DTO directly (200 for GET/PUT, 201 for POST, 204 for DELETE)
- **Validation error (422):** ProblemDetails with `errors` dictionary (field → messages[])
- **Not found (404):** ProblemDetails
- **Unauthorized (401) / Forbidden (403):** ProblemDetails

### Step 5 — Create/update DTO

**DTO location:** `apps/blog-api/src/Blog.Application/DTOs/{Entity}Dto.cs`

Map domain properties to DTO using camelCase JSON naming (ASP.NET Core default).

### Naming Conventions

- Commands: `{Action}{Entity}Command` (e.g., `CreatePostCommand`, `PublishPostCommand`)
- Queries: `Get{Entity}{Criterion}Query` (e.g., `GetPostBySlugQuery`, `GetPostListQuery`)
- Handlers: `{Command|Query}Handler` (e.g., `CreatePostCommandHandler`)
- Validators: `{Command|Query}Validator` (e.g., `CreatePostCommandValidator`)
- DTOs: `{Entity}Dto`, `{Entity}DetailDto` (e.g., `PostDto`, `PostDetailDto`)
- Controllers: `{EntityPlural}Controller` (e.g., `PostsController`)

### Cache Key Conventions (if cacheable)

```
post:slug:{slug}                    → 1 hour
post:id:{id}                        → 1 hour
post-list:page:{p}:size:{s}         → 5 min
post-list:tag:{tag}:{p}             → 5 min
post-list:author:{id}:{p}           → 5 min
user:profile:{username}             → 30 min
comments:post:{postId}:{p}          → 2 min
```

### MediatR Pipeline Behavior Order

All requests pass through these behaviors in order:
1. `ValidationBehavior` — FluentValidation (runs for all)
2. `LoggingBehavior` — Structured logging (runs for all)
3. `AuthorizationBehavior` — RBAC check (runs if `IAuthorizedRequest`)
4. `CachingBehavior` — Redis cache-aside (runs if `ICacheableQuery`)

### API Route Conventions

| Pattern | Example |
|---|---|
| List | `GET /api/v1/posts` |
| Get by slug | `GET /api/v1/posts/{slug}` |
| Create | `POST /api/v1/posts` |
| Update | `PUT /api/v1/posts/{id}` |
| Delete | `DELETE /api/v1/posts/{id}` |
| Action | `POST /api/v1/posts/{id}/publish` |
| Nested | `GET /api/v1/posts/{postId}/comments` |
| Nested create | `POST /api/v1/posts/{postId}/comments` |
