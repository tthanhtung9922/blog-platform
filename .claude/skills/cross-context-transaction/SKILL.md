# cross-context-transaction

Implement the shared DbConnection pattern for operations spanning IdentityDbContext and BlogDbContext (ADR-007) to ensure atomicity across ASP.NET Identity and Domain contexts.

## Arguments

- `operation` (required) — The cross-context operation: `register`, `ban`, `delete-account`, or custom
- `description` (optional) — What the operation does across both contexts

## Instructions

You are implementing a cross-context transaction for the blog-platform. The project has TWO separate DbContexts (ADR-006):

- **IdentityDbContext** — ASP.NET Identity (`AspNetUsers` table) for authentication
- **BlogDbContext** — Domain entities (`users` table) for business logic

These share only a GUID ID. Operations like user registration and ban must be atomic across both.

### The Problem

```
IdentityDbContext → AspNetUsers table (password hash, email, lockout)
BlogDbContext     → users table (display_name, role, bio, avatar)

Shared: only the GUID ID
```

Without a shared transaction, a crash between the two SaveChanges calls leaves data inconsistent (e.g., IdentityUser created but no Domain User).

### Solution: Shared DbConnection (ADR-007)

```csharp
// apps/blog-api/src/Blog.Infrastructure/Persistence/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private IdentityDbContext? _identityContext;
    private BlogDbContext? _blogContext;

    public UnitOfWork(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BlogDb")!;
    }

    public async Task<T> ExecuteAsync<T>(Func<IdentityDbContext, BlogDbContext, Task<T>> operation, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Create both contexts sharing the same connection + transaction
            var identityContext = CreateIdentityContext(connection, transaction);
            var blogContext = CreateBlogContext(connection, transaction);

            var result = await operation(identityContext, blogContext);

            await identityContext.SaveChangesAsync(ct);
            await blogContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ExecuteAsync(Func<IdentityDbContext, BlogDbContext, Task> operation, CancellationToken ct = default)
    {
        await ExecuteAsync(async (identity, blog) =>
        {
            await operation(identity, blog);
            return true;
        }, ct);
    }

    private IdentityDbContext CreateIdentityContext(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connection)
            .Options;

        var context = new IdentityDbContext(options);
        context.Database.UseTransaction(transaction);
        return context;
    }

    private BlogDbContext CreateBlogContext(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseNpgsql(connection)
            .Options;

        var context = new BlogDbContext(options);
        context.Database.UseTransaction(transaction);
        return context;
    }
}
```

### IUnitOfWork Interface

```csharp
// apps/blog-api/src/Blog.Application/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<IdentityDbContext, BlogDbContext, Task<T>> operation, CancellationToken ct = default);
    Task ExecuteAsync(Func<IdentityDbContext, BlogDbContext, Task> operation, CancellationToken ct = default);
}
```

### Usage: User Registration

```csharp
// apps/blog-api/src/Blog.Application/Features/Auth/Commands/Register/RegisterCommandHandler.cs
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthTokenDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtService;

    public async Task<AuthTokenDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        return await _unitOfWork.ExecuteAsync(async (identityCtx, blogCtx) =>
        {
            // 1. Create IdentityUser
            var identityUser = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = request.Email,
                Email = request.Email,
            };

            var identityResult = await _userManager.CreateAsync(identityUser, request.Password);
            if (!identityResult.Succeeded)
                throw new ValidationException(identityResult.Errors);

            // 2. Create Domain User (same ID)
            var user = User.Create(
                Guid.Parse(identityUser.Id),
                request.DisplayName);

            blogCtx.Users.Add(user);

            // 3. Both SaveChanges happen in UnitOfWork.ExecuteAsync
            //    If either fails → transaction rolls back BOTH

            // 4. Generate tokens
            return _jwtService.GenerateTokens(identityUser, user);
        }, ct);
    }
}
```

### Usage: Ban User

```csharp
public class BanUserCommandHandler : IRequestHandler<BanUserCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public async Task Handle(BanUserCommand request, CancellationToken ct)
    {
        await _unitOfWork.ExecuteAsync(async (identityCtx, blogCtx) =>
        {
            // 1. Lock out IdentityUser
            var identityUser = await identityCtx.Users.FindAsync(request.UserId.ToString());
            if (identityUser is null) throw new UserNotFoundException(request.UserId);

            identityUser.LockoutEnd = DateTimeOffset.MaxValue;  // Permanent lockout
            identityUser.LockoutEnabled = true;

            // 2. Deactivate Domain User
            var user = await blogCtx.Users.FindAsync(request.UserId);
            if (user is null) throw new UserNotFoundException(request.UserId);

            user.Deactivate();  // Sets IsActive = false

            // Both committed atomically by UnitOfWork
        }, ct);
    }
}
```

### Registration (DI)

```csharp
// apps/blog-api/src/Blog.Infrastructure/DependencyInjection.cs
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

### When to Use Cross-Context Transactions

| Operation | Spans Contexts? | Use UnitOfWork? |
|---|---|---|
| User registration | Yes (Identity + Domain) | YES |
| User ban/deactivate | Yes (Identity lockout + Domain flag) | YES |
| Delete account | Yes (Identity + Domain cascade) | YES |
| Update profile | No (Domain only) | NO — use BlogDbContext directly |
| Change password | No (Identity only) | NO — use UserManager directly |
| Create post | No (Domain only) | NO — use repository |

### Key Rules

1. **Only use for cross-context operations** — Do not use UnitOfWork for single-context operations
2. **Shared connection, shared transaction** — Both contexts use the same NpgsqlConnection and NpgsqlTransaction
3. **Rollback on any failure** — If either SaveChanges fails, the entire transaction rolls back
4. **IdentityUser has no FK to User** — They share only a GUID ID (ADR-006)
5. **Future migration path** — When moving to microservices (Phase 3+), replace with Saga/compensating actions (ADR-007, Option B)
