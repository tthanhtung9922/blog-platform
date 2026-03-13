# Phase 1: Monorepo Foundation + Domain Layer - Research

**Researched:** 2026-03-13
**Domain:** Nx 22 polyglot monorepo, .NET 10 DDD domain layer, EF Core 10 / PostgreSQL 18, Docker Compose, NetArchTest
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **Post aggregate:** Post (root) + PostContent (body_json + body_html, 1-to-1 entity) + PostVersion (snapshot on each save entity). All three live inside the Post aggregate boundary; EF Core loads them together.
- **Comment aggregate:** Comment aggregate root with parent_id for 1-level nesting. The Comment domain enforces the nesting constraint: a reply cannot itself have a parent (`Comment.AddReply()` throws `DomainException` if the parent already has a `ParentId`). This is a domain rule, not an Application layer concern.
- **User aggregate:** Full profile fields from day one — Id (Guid, shared with IdentityUser), Email (value object), DisplayName, Bio, AvatarUrl, Website, SocialLinks, Role, IsActive, CreatedAt. Defining all fields in Phase 1 avoids migration changes in later phases.
- **Tag:** Tag has its own aggregate root for CRUD (TagId, Name, Slug value object). Post references tags by TagId only — Post holds a collection of TagIds (value objects), not Tag entities. EF Core maps this via a post_tags join table.
- All aggregate roots inherit from `AggregateRoot<TId>` base class in `Blog.Domain/Common/`.
- All value objects implemented in Phase 1: `Slug`, `Email`, `ReadingTime`, `Tag` (as VO on Post), PostStatus enum: Draft/Published/Archived, CommentStatus enum: Pending/Approved/Rejected.
- All domain events defined in Phase 1 as C# `record` types in `Blog.Domain/DomainEvents/`. No handlers yet.
- All 4 repository interfaces defined in `Blog.Domain/Repositories/` in Phase 1: `IPostRepository`, `ICommentRepository`, `IUserRepository`, `ITagRepository`.
- Phase 1 creates a **minimal Blog.Infrastructure**: BlogDbContext + all IEntityTypeConfiguration<T> files + CreateUnaccentExtension migration (SuppressTransaction = true) + initial schema migration.
- Phase 1 also creates a **bare Blog.API** project (Program.cs only): registers DbContexts, runs MigrateAsync() on startup, adds health check. No controllers.
- `Blog.ArchTests` (NetArchTest) enforces layer boundaries + domain model integrity rules from Phase 1.
- **Single `docker-compose.yml`** at repo root: PostgreSQL 18, Redis 8, MinIO. No Postal. Fixed ports (5432, 6379, 9000/9001).
- PostgreSQL init via `docker/init.sql` mounted at `docker-entrypoint-initdb.d/` — creates DB and enables unaccent extension.
- MinIO init via one-shot `mc` init container that creates `blog-media` bucket, then exits.
- No seed data in Phase 1.
- Use `@nx/dotnet` (official, Nx 22+), NOT `@nx-dotnet/core` (deprecated September 2025).

### Claude's Discretion

- Exact `AggregateRoot<TId>` base class implementation (domain events collection, `AddDomainEvent()` helper)
- EF Core entity configuration details (cascade deletes, index strategies)
- Nx project.json configuration details (tags, implicit dependencies, affected graph)
- Exact PostgreSQL init.sql content beyond enabling unaccent

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within Phase 1 scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| INFR-01 | All services (API, PostgreSQL, Redis, MinIO) run locally via Docker Compose | Docker Compose service definitions, PostgreSQL 18 volume path change, MinIO mc init container pattern |
</phase_requirements>

---

## Summary

Phase 1 lays every foundational layer from scratch: a polyglot Nx 22 monorepo combining .NET 10 and Next.js 16.1 projects, a pure C# DDD domain layer with four aggregate roots and all value objects, a minimal EF Core 10 infrastructure layer sufficient only to run migrations, and a Docker Compose environment that boots PostgreSQL 18, Redis 8, and MinIO locally in one command.

The most critical technical facts to know before planning: (1) `@nx/dotnet` (Nx 22+ official plugin) auto-detects `.csproj` files and infers build/test targets — no generators are needed, just standard `dotnet new` to create projects; (2) PostgreSQL 18 changed its `PGDATA` path from `/var/lib/postgresql/data` to a versioned path, so docker-compose volumes must target `/var/lib/postgresql` not the old path; (3) the `CreateUnaccentExtension` migration must set `SuppressTransaction = true` because `CREATE EXTENSION` cannot run inside a transaction; (4) `EFCore.NamingConventions` 10.0.1 provides `UseSnakeCaseNamingConvention()` with full EF Core 10 support; (5) `NetArchTest.Rules` 1.3.2 is the correct architecture test library — the `eNhancedEdition` fork adds immutability checks useful for value object assertions.

**Primary recommendation:** Use `nx add @nx/dotnet` first, create all .NET projects with `dotnet new`, register them in the Nx graph by adding `project.json` alongside each `.csproj`, then build domain layer from the inside out (Value Objects → Aggregates → Events → Repository interfaces → EF configurations → Migrations → bare API).

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Nx | 22.x | Monorepo task runner and project graph | Only version that ships official `@nx/dotnet` plugin |
| @nx/dotnet | Bundled in Nx 22 | Auto-detects .csproj files, infers build/test/watch targets | Official Nx team plugin, replaces deprecated `@nx-dotnet/core` |
| @nx/next | 22.x | Next.js app generation and graph integration | Pairs with Nx 22 monorepo setup |
| .NET SDK | 10.0 | Backend runtime | Project target framework |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | EF Core provider for PostgreSQL | Only provider with EF Core 10 + PostgreSQL 18 support |
| EFCore.NamingConventions | 10.0.1 | `UseSnakeCaseNamingConvention()` for all table/column names | Eliminates manual `HasColumnName()` on every property |
| NetArchTest.Rules | 1.3.2 | Layer boundary enforcement in xUnit tests | Standard .NET architecture test library |
| NetArchTest.eNhancedEdition | 1.4.5 | Extended rules (immutability, stateless checks) | Adds `AreImmutable` rule needed for value object assertions |
| xUnit | 2.9.x | Test framework for ArchTests and UnitTests | Project standard per testing rules |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.NET.Test.Sdk | latest | Required for `dotnet test` target recognition | All test projects |
| xunit.runner.visualstudio | latest | xUnit runner integration | Test projects |
| MediatR | 12.x | `INotification` interface for IDomainEvent | Domain layer needs this single external reference |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| EFCore.NamingConventions | Manual `HasColumnName()` on every property | Manual approach is verbose and error-prone across hundreds of columns |
| NetArchTest.eNhancedEdition | Write custom reflection-based immutability tests | eNhancedEdition saves significant boilerplate for VO immutability assertions |
| `postgres:18` | `postgres:17` | PostgreSQL 18 is the project target; docker image is available on Docker Hub |

**Installation (root package.json / npm):**
```bash
# Initialize Nx workspace (if not already done)
npx create-nx-workspace@latest blog-platform --preset=empty

# Add official plugins
nx add @nx/dotnet
nx add @nx/next

# .NET NuGet packages installed per project via csproj — not npm
```

---

## Architecture Patterns

### Recommended Project Structure

```
blog-platform/
├── apps/
│   ├── blog-api/
│   │   └── src/
│   │       ├── Blog.Domain/           # Pure C#, no framework deps except MediatR.INotification
│   │       │   ├── Common/            # AggregateRoot<TId>, ValueObject base classes
│   │       │   ├── Aggregates/
│   │       │   │   ├── Posts/         # Post.cs, PostContent.cs, PostVersion.cs, PostStatus.cs
│   │       │   │   ├── Comments/      # Comment.cs, CommentStatus.cs
│   │       │   │   ├── Users/         # User.cs
│   │       │   │   └── Tags/          # Tag.cs
│   │       │   ├── ValueObjects/      # Slug.cs, Email.cs, ReadingTime.cs, TagReference.cs
│   │       │   ├── DomainEvents/      # All *Event.cs records
│   │       │   ├── Repositories/      # IPostRepository.cs, ICommentRepository.cs, etc.
│   │       │   └── Exceptions/        # DomainException.cs
│   │       ├── Blog.Infrastructure/   # Phase 1: DbContext + configs + migrations only
│   │       │   └── Persistence/
│   │       │       ├── BlogDbContext.cs
│   │       │       ├── Configurations/ # PostConfiguration.cs, etc.
│   │       │       └── Migrations/
│   │       └── Blog.API/              # Phase 1: Program.cs only (DbContext reg + MigrateAsync + healthcheck)
│   ├── blog-web/                      # Next.js 16.1 — project.json declares shared-contracts implicit dep
│   └── blog-admin/                    # Next.js 16.1 — project.json declares shared-contracts implicit dep
├── libs/
│   ├── shared-contracts/              # project.json registers it in Nx graph
│   └── shared-ui/
├── tests/
│   ├── Blog.UnitTests/
│   └── Blog.ArchTests/                # NetArchTest layer + domain model integrity rules
├── docker/
│   └── init.sql                       # CREATE DATABASE + CREATE EXTENSION unaccent
└── docker-compose.yml
```

### Pattern 1: AggregateRoot<TId> Base Class

**What:** A base class that every aggregate root inherits from. It manages the domain events list and provides the `AddDomainEvent()` helper.
**When to use:** All four aggregate roots (Post, Comment, User, Tag) inherit from this.

```csharp
// Blog.Domain/Common/AggregateRoot.cs
// Source: project skill add-domain-entity/SKILL.md
public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IDomainEvent : MediatR.INotification { }
```

### Pattern 2: Value Object Base Class

**What:** Abstract base that implements structural equality (compared by value, not reference).
**When to use:** Slug, Email, ReadingTime, TagReference all extend this.

```csharp
// Blog.Domain/Common/ValueObject.cs
// Source: project skill add-domain-entity/SKILL.md
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(1, (current, obj) => HashCode.Combine(current, obj));
}
```

### Pattern 3: Aggregate Root with Factory Method and Domain Events

**What:** Aggregates use private constructors + static `Create()` factory methods. EF Core materialization uses a separate private parameterless constructor.
**When to use:** All four aggregate roots.

```csharp
// Source: project skill add-domain-entity/SKILL.md
public class Post : AggregateRoot<Guid>
{
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public PostStatus Status { get; private set; }
    // ... all properties with private set

    private Post() { } // EF Core materializer

    public static Post Create(Guid authorId, string title, Slug slug)
    {
        var post = new Post { Id = Guid.NewGuid(), ... };
        post.AddDomainEvent(new PostCreatedEvent(post.Id));
        return post;
    }
}
```

### Pattern 4: Domain Events as Record Types

**What:** Domain events are C# `record` types — immutable by design, compared by value.
**When to use:** Every domain event, defined in `Blog.Domain/DomainEvents/`.

```csharp
// Blog.Domain/DomainEvents/PostPublishedEvent.cs
// Source: project skill add-domain-entity/SKILL.md
public record PostPublishedEvent(Guid PostId) : IDomainEvent;
public record PostUpdatedEvent(Guid PostId) : IDomainEvent;
public record PostArchivedEvent(Guid PostId) : IDomainEvent;
// etc.
```

### Pattern 5: EF Core Entity Configuration with Snake Case

**What:** Each aggregate/entity gets its own `IEntityTypeConfiguration<T>` class. The `UseSnakeCaseNamingConvention()` call on the DbContext options handles the majority of column naming automatically. Explicit `HasColumnName()` is only needed for columns that EF Core would not name correctly.
**When to use:** Every entity that maps to a database table.

```csharp
// Blog.Infrastructure/Persistence/BlogDbContext.cs
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention();  // EFCore.NamingConventions 10.0.1

// Blog.Infrastructure/Persistence/Configurations/PostConfiguration.cs
// Source: project skill run-ef-migration/SKILL.md
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug)
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(s => s.Value, v => Slug.FromExisting(v));

        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Status)
            .HasMaxLength(20)
            .HasConversion<string>();

        // Partial index — only published posts indexed
        builder.HasIndex(p => p.Status)
            .HasFilter("status = 'Published'");
    }
}
```

### Pattern 6: Unaccent Extension Migration (SuppressTransaction = true)

**What:** PostgreSQL extensions cannot be created inside a transaction. This migration must run first, before the schema migration.
**When to use:** One-time, runs first in CI and local migration sequence.

```csharp
// Blog.Infrastructure/Persistence/Migrations/XXXXXX_CreateUnaccentExtension.cs
public partial class CreateUnaccentExtension : Migration
{
    protected override bool SuppressTransaction => true;  // REQUIRED

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP EXTENSION IF EXISTS unaccent;");
    }
}
```

### Pattern 7: Nx Project Graph Configuration

**What:** `project.json` files register projects in the Nx graph. `implicitDependencies` declares dependencies Nx cannot discover through static analysis (e.g., TypeScript consuming generated types from `shared-contracts`).
**When to use:** Every project that Nx needs to understand.

```json
// apps/blog-web/project.json  (and blog-admin/project.json)
{
  "name": "blog-web",
  "tags": ["type:app", "scope:frontend"],
  "implicitDependencies": ["shared-contracts"]
}
```

```json
// libs/shared-contracts/project.json
{
  "name": "shared-contracts",
  "tags": ["type:lib", "scope:shared"]
}
```

For .NET projects, `@nx/dotnet` auto-detects `.csproj` files and infers targets. A minimal `project.json` alongside the `.csproj` registers the project name and tags:
```json
// apps/blog-api/src/Blog.API/project.json
{
  "name": "blog-api",
  "tags": ["type:app", "scope:backend", "layer:api"]
}
```

### Pattern 8: Docker Compose (PostgreSQL 18 + Redis 8 + MinIO)

**CRITICAL — PostgreSQL 18 changed its PGDATA path.** The volume must target `/var/lib/postgresql` (not the old `/var/lib/postgresql/data`) because PostgreSQL 18 uses `/var/lib/postgresql/18/docker` internally.

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:18
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: blog
      POSTGRES_PASSWORD: blog
      POSTGRES_DB: blog_db
    volumes:
      - postgres-data:/var/lib/postgresql  # CORRECT for PG18 (versioned subdir)
      - ./docker/init.sql:/docker-entrypoint-initdb.d/init.sql

  redis:
    image: redis:8
    ports:
      - "6379:6379"

  minio:
    image: quay.io/minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    command: ["server", "--console-address", ":9001", "/data"]
    environment:
      MINIO_ROOT_USER: minio
      MINIO_ROOT_PASSWORD: minio123
    volumes:
      - minio-data:/data

  minio-init:
    image: quay.io/minio/mc:latest
    depends_on:
      - minio
    restart: on-failure
    entrypoint: >
      /bin/sh -c "
      sleep 5;
      /usr/bin/mc alias set local http://minio:9000 minio minio123;
      /usr/bin/mc mb local/blog-media --ignore-existing;
      exit 0;
      "

volumes:
  postgres-data:
  minio-data:
```

```sql
-- docker/init.sql
CREATE EXTENSION IF NOT EXISTS unaccent;
```

Note: The `CREATE EXTENSION` in `init.sql` is idempotent and runs at container first-boot. The EF Core `CreateUnaccentExtension` migration uses `IF NOT EXISTS` to be safe when both run.

### Pattern 9: NetArchTest Layer Boundary Rules

**What:** Architecture tests that run in CI from Phase 1. They pass vacuously in Phase 1 (Application and API layers are nearly empty) but enforce boundaries as those layers grow in later phases.

```csharp
// tests/Blog.ArchTests/LayerBoundaryTests.cs
// Source: NetArchTest.Rules 1.3.2 GitHub examples
public class LayerBoundaryTests
{
    [Fact]
    public void Domain_ShouldNot_ReferenceBlogInfrastructure()
    {
        var result = Types.InAssembly(typeof(Post).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNot_ReferenceBlogAPI()
    {
        var result = Types.InAssembly(typeof(Post).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNot_ReferenceBlogAPI()
    {
        // Vacuous pass in Phase 1 — Application assembly is empty
        // Enforced from Phase 2+
        var result = Types.InAssembly(typeof(IPostRepository).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Blog.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

### Anti-Patterns to Avoid

- **Using `@nx-dotnet/core`:** Deprecated September 2025. The project.json generators and configuration format differ from `@nx/dotnet`. Only use the official plugin.
- **Mounting `/var/lib/postgresql/data` for PostgreSQL 18:** This is the PostgreSQL 16/17 path. PostgreSQL 18 changed the internal PGDATA to a versioned subdirectory. Mount `/var/lib/postgresql` to future-proof the volume.
- **Omitting `SuppressTransaction = true` from `CreateUnaccentExtension`:** EF Core wraps migrations in a transaction by default. PostgreSQL throws an error if you try to `CREATE EXTENSION` inside a transaction.
- **Calling `MigrateAsync()` without a try/catch or retry logic in production:** For local dev (Phase 1), calling `MigrateAsync()` on startup is fine. In Phase 2, add resilience for production environments.
- **Putting EF Core or framework references in `Blog.Domain`:** The domain layer is allowed exactly one external reference: `MediatR.INotification` for `IDomainEvent`. Nothing else.
- **Public property setters on Value Objects:** Value objects are immutable — all properties must be `public ... { get; }` or `private set`. NetArchTest can verify this.
- **User extends IdentityUser:** ADR-006 is a hard prohibition. `User` in `Blog.Domain` is a plain aggregate root. `IdentityUser` is Infrastructure-layer only.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Snake_case table/column names | Manual `HasColumnName()` on every property | `EFCore.NamingConventions` + `UseSnakeCaseNamingConvention()` | 100+ properties in this schema; any single miss causes a migration/query bug |
| Value equality for Value Objects | Custom `Equals()` / `GetHashCode()` per VO | `ValueObject` base class with `GetEqualityComponents()` | Pattern is standard DDD; hand-rolling it per VO leads to inconsistency |
| MinIO bucket creation | Startup code in the API | `mc` init container in Docker Compose | API shouldn't know about bucket initialization; init container exits cleanly |
| Architecture rule enforcement | Code review only | `NetArchTest.Rules` ArchTests in CI | Rules enforced at every PR, not just when reviewers catch it |
| Layer boundary detection | Manual documentation | `@nx/dotnet` project graph | Nx provides affected-graph, remote caching, and dependency tracking out of the box |

---

## Common Pitfalls

### Pitfall 1: PostgreSQL 18 Volume Mount Path

**What goes wrong:** Docker Compose starts but data is not persisted between restarts, or the container fails to start entirely.
**Why it happens:** PostgreSQL 18 changed PGDATA from `/var/lib/postgresql/data` to `/var/lib/postgresql/18/docker`. If a volume is mounted at the old path, PostgreSQL ignores it or cannot find its own data directory.
**How to avoid:** Mount the parent path: `postgres-data:/var/lib/postgresql` in docker-compose.yml volumes section.
**Warning signs:** Container logs show "initdb" running on every restart, or the data directory appears empty after stopping and restarting.

### Pitfall 2: CreateUnaccentExtension Without SuppressTransaction

**What goes wrong:** `MigrateAsync()` throws `ERROR: CREATE EXTENSION cannot run inside a transaction block`.
**Why it happens:** EF Core wraps each migration in a transaction by default. `CREATE EXTENSION` is a DDL statement that PostgreSQL forbids inside transactions.
**How to avoid:** Set `protected override bool SuppressTransaction => true;` in the migration class body (not a constructor, not an attribute).
**Warning signs:** Migration fails at the first run against a fresh database.

### Pitfall 3: @nx-dotnet/core vs @nx/dotnet Confusion

**What goes wrong:** Generators produce the old configuration format, project detection fails, or CI gives errors about unknown executor plugins.
**Why it happens:** The community plugin (`@nx-dotnet/core`) and the official plugin (`@nx/dotnet`) have different configuration schemas.
**How to avoid:** Run `nx add @nx/dotnet` (no slash between nx and dotnet). Never install `@nx-dotnet/core`. Verify `nx.json` plugins array references `"@nx/dotnet"`.
**Warning signs:** `package.json` contains `@nx-dotnet/core`; `nx.json` executors reference `@nx-dotnet/core:build`.

### Pitfall 4: MediatR Reference in Domain Layer

**What goes wrong:** Architecture tests catch a dependency violation, or the reviewer flags a hidden coupling from Domain to Application/Infrastructure.
**Why it happens:** `IDomainEvent` inherits from `MediatR.INotification`. MediatR must be referenced from the Domain project. This is the only acceptable external reference in `Blog.Domain`.
**How to avoid:** Add `MediatR` NuGet reference to `Blog.Domain.csproj` explicitly, and configure the NetArchTest rule to allow this one exception: `ShouldNot().HaveDependencyOtherThan("System", "MediatR", "Blog.Domain")`.
**Warning signs:** `Blog.Domain.csproj` has no `MediatR` reference but `IDomainEvent : INotification` causes a compile error.

### Pitfall 5: EF Core Migrations with Two DbContexts

**What goes wrong:** Running `dotnet ef migrations add` creates the migration in the wrong DbContext, or the migration runner applies to the wrong schema.
**Why it happens:** Phase 1 has `BlogDbContext`. Phase 3 adds `IdentityDbContext`. Both need separate migration histories. The `--context` flag is easy to forget.
**How to avoid:** Always specify `--context BlogDbContext` or `--context IdentityDbContext` when running EF CLI commands. Document this in the `scripts/migration.sh` helper.
**Warning signs:** Identity tables appear in the BlogDbContext migrations, or vice versa.

### Pitfall 6: Nx Project Detection for .NET Projects

**What goes wrong:** `nx show project blog-api` returns no targets, or `nx build blog-api` fails because Nx can't find the project.
**Why it happens:** The `@nx/dotnet` plugin detects `.csproj` files, but if no `project.json` exists alongside the `.csproj`, the project name defaults to the file path — not the intended name.
**How to avoid:** Place a `project.json` alongside every `.csproj` that you want named/tagged in the Nx graph. The `project.json` only needs `"name"` and `"tags"` fields; `@nx/dotnet` infers all targets from the `.csproj`.
**Warning signs:** `nx graph` does not show `blog-api` as a node, or the node has no outgoing edges.

---

## Code Examples

### BlogDbContext Registration (Minimal, Phase 1)

```csharp
// Blog.Infrastructure/Persistence/BlogDbContext.cs
public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostContent> PostContents => Set<PostContent>();
    public DbSet<PostVersion> PostVersions => Set<PostVersion>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlogDbContext).Assembly);
    }
}
```

```csharp
// Blog.API/Program.cs (minimal Phase 1)
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BlogDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<BlogDbContext>();

var app = builder.Build();

// Run migrations on startup (Phase 1 / local dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/healthz");
app.Run();
```

### EF Core Migration Commands

```bash
# From apps/blog-api/src/Blog.API/
# Create migration
dotnet ef migrations add CreateUnaccentExtension \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --context BlogDbContext \
  --output-dir Persistence/Migrations

dotnet ef migrations add InitialSchema \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --context BlogDbContext \
  --output-dir Persistence/Migrations

# Apply locally
dotnet ef database update \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --context BlogDbContext
```

### Nx Workspace Init and Plugin Setup

```bash
# Create workspace
npx create-nx-workspace@latest blog-platform --preset=empty --nxCloud=skip

# Add plugins
cd blog-platform
nx add @nx/dotnet   # Official .NET plugin (Nx 22+)
nx add @nx/next     # Next.js support

# Create .NET projects using standard dotnet CLI (not nx generators)
dotnet new classlib -n Blog.Domain      -f net10.0 -o apps/blog-api/src/Blog.Domain
dotnet new classlib -n Blog.Infrastructure -f net10.0 -o apps/blog-api/src/Blog.Infrastructure
dotnet new webapi  -n Blog.API         -f net10.0 -o apps/blog-api/src/Blog.API
dotnet new xunit   -n Blog.ArchTests   -f net10.0 -o tests/Blog.ArchTests

# Add project.json alongside each .csproj to register in Nx graph
# @nx/dotnet auto-infers build/test/watch targets from the .csproj
```

### Nx Graph Verification

```bash
# Verify shared-contracts appears as dependency of blog-web and blog-admin
nx graph

# Build blog-api — validates compilation succeeds
nx build blog-api

# Run architecture tests
nx test blog-arch-tests  # (or: dotnet test tests/Blog.ArchTests)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `@nx-dotnet/core` community plugin | `@nx/dotnet` official Nx plugin | September 2025 | Official support, better performance, maintained by Nx team |
| `/var/lib/postgresql/data` Docker volume | `/var/lib/postgresql` (versioned subdirectory) | PostgreSQL 18 | Must update volume mounts; old path will fail or silently lose data |
| `Npgsql.EntityFrameworkCore.PostgreSQL` 9.x | 10.0.1 with EF Core 10 | January 2026 | Full EF Core 10 support, PostgreSQL 18 virtual generated columns, UUIDv7 |
| `EFCore.NamingConventions` 8.x/9.x | 10.0.1 | January 2026 | Full EF Core 10 + Npgsql 10 compatibility |

**Deprecated/outdated:**
- `@nx-dotnet/core`: Deprecated September 2025 — replaced by `@nx/dotnet`.
- `Swagger UI (Swashbuckle)`: Project uses Scalar UI instead per API design rules.
- `postgres:17` and earlier Docker tags: PostgreSQL 18 is the project target image.

---

## Open Questions

1. **Nx 22 @nx/dotnet — exact nx.json plugins configuration schema**
   - What we know: `nx add @nx/dotnet` auto-configures `nx.json`; plugin detects `.csproj` files
   - What's unclear: Whether `targetDefaults` for dotnet targets need manual specification beyond plugin defaults
   - Recommendation: Run `nx add @nx/dotnet` and inspect the generated `nx.json` plugins array during Wave 0; adjust targetDefaults if `nx build blog-api` does not infer correctly

2. **EF Core 10 single-transaction migration behavior**
   - What we know: EF Core 9 introduced single-transaction-per-DbContext migration mode; there are open issues (dotnet/efcore#35096) about transaction-incompatible SQL
   - What's unclear: Whether EF Core 10 resolves this or if `SuppressTransaction = true` is sufficient isolation
   - Recommendation: Keep `SuppressTransaction = true` on `CreateUnaccentExtension`; validate migration sequence in Wave 0 test run

3. **NetArchTest.Rules — .NET 10 compatibility**
   - What we know: Version 1.3.2 is the latest; targets .NET Standard 2.0 (compatible with .NET 10)
   - What's unclear: Whether `eNhancedEdition` 1.4.5 is also .NET Standard 2.0 compatible
   - Recommendation: Use standard `NetArchTest.Rules` 1.3.2 for layer boundary rules; only add `eNhancedEdition` if immutability tests fail to compile

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.x + NetArchTest.Rules 1.3.2 |
| Config file | `tests/Blog.ArchTests/Blog.ArchTests.csproj` (no separate config — dotnet test auto-discovers) |
| Quick run command | `dotnet test tests/Blog.ArchTests/ --no-build` |
| Full suite command | `dotnet test tests/Blog.ArchTests/ && dotnet test tests/Blog.UnitTests/` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INFR-01 | PostgreSQL, Redis, MinIO all reachable after `docker-compose up` | smoke (manual) | `docker-compose up -d && docker-compose ps` | ❌ Wave 0 |
| (implicit) | Domain has no Infrastructure reference | architecture | `dotnet test tests/Blog.ArchTests/` | ❌ Wave 0 |
| (implicit) | Domain has no API reference | architecture | `dotnet test tests/Blog.ArchTests/` | ❌ Wave 0 |
| (implicit) | Value objects have no public setters | architecture | `dotnet test tests/Blog.ArchTests/` | ❌ Wave 0 |
| (implicit) | Domain events are `record` types | architecture | `dotnet test tests/Blog.ArchTests/` | ❌ Wave 0 |
| (implicit) | Aggregates inherit from `AggregateRoot<TId>` | architecture | `dotnet test tests/Blog.ArchTests/` | ❌ Wave 0 |
| (implicit) | `nx build blog-api` succeeds | build | `nx build blog-api` | ❌ Wave 0 |
| (implicit) | EF Core migration applies cleanly to PostgreSQL 18 | integration/smoke | `dotnet ef database update` against running docker-compose | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test tests/Blog.ArchTests/ --no-build`
- **Per wave merge:** `dotnet test tests/Blog.ArchTests/ && nx build blog-api`
- **Phase gate:** All arch tests green + `nx build blog-api` succeeds + EF Core migration applies cleanly against `docker-compose up` PostgreSQL 18

### Wave 0 Gaps

- [ ] `tests/Blog.ArchTests/Blog.ArchTests.csproj` — create project, add NetArchTest.Rules 1.3.2 + xUnit
- [ ] `tests/Blog.ArchTests/LayerBoundaryTests.cs` — covers Domain-does-not-reference-Infrastructure, Application-does-not-reference-API rules
- [ ] `tests/Blog.ArchTests/DomainModelIntegrityTests.cs` — covers value object immutability, domain events are records, aggregates inherit AggregateRoot<TId>
- [ ] `tests/Blog.UnitTests/Blog.UnitTests.csproj` — create project (empty in Phase 1, populated in Phase 2+)
- [ ] `docker-compose.yml` — PostgreSQL 18 + Redis 8 + MinIO + mc init container
- [ ] `docker/init.sql` — CREATE EXTENSION unaccent

---

## Sources

### Primary (HIGH confidence)

- Project skill `add-domain-entity/SKILL.md` — DDD aggregate, value object, domain event, repository interface patterns with exact C# code
- Project skill `run-ef-migration/SKILL.md` — EF Core migration commands, naming conventions, SuppressTransaction pattern
- `CLAUDE.md` + `.claude/rules/` — All architecture rules, layer direction, IdentityUser/User separation, snake_case convention
- [Nx 22 Release Blog](https://nx.dev/blog/nx-22-release) — Confirmed `@nx/dotnet` official plugin in Nx 22
- [Migrate from @nx-dotnet/core](https://nx.dev/docs/technologies/dotnet/guides/migrate-from-nx-dotnet-core) — Confirmed `@nx-dotnet/core` is deprecated
- [Npgsql EF Core 10 Release Notes](https://www.npgsql.org/efcore/release-notes/10.0.html) — EF Core 10 / PostgreSQL 18 compatibility confirmed
- [EFCore.NamingConventions GitHub](https://github.com/efcore/EFCore.NamingConventions) — Version 10.0.1 released January 2026, EF Core 10 support

### Secondary (MEDIUM confidence)

- [PostgreSQL 18 Docker PGDATA path change](https://aronschueler.de/blog/2025/10/30/fixing-postgres-18-docker-compose-startup/) — PGDATA is now `/var/lib/postgresql/18/docker`; volume should mount `/var/lib/postgresql`
- [Docker Hub postgres:18 image](https://hub.docker.com/layers/library/postgres/18/images/) — Confirms `postgres:18` tag is available
- [MinIO bucket auto-creation pattern](https://banach.net.pl/posts/2025/creating-bucket-automatically-on-local-minio-with-docker-compose/) — Exact `mc` init container pattern verified
- [NetArchTest.Rules GitHub](https://github.com/BenMorris/NetArchTest) — Version 1.3.2 latest, .NET Standard 2.0 base
- [NetArchTest.eNhancedEdition](https://github.com/NeVeSpl/NetArchTest.eNhancedEdition) — Version 1.4.5, adds immutability rules

### Tertiary (LOW confidence)

- WebSearch results for Nx 22 `@nx/dotnet` exact `nx.json` plugins schema — could not directly fetch official docs page; specific configuration syntax should be verified after `nx add @nx/dotnet` generates it

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions verified against NuGet and official release notes
- Architecture: HIGH — based on project's own skill files and CLAUDE.md rules
- Docker Compose patterns: HIGH — PostgreSQL 18 path change verified from official Docker Hub + dedicated blog post
- Nx/dotnet integration: MEDIUM — official plugin confirmed in Nx 22, exact `nx.json` config schema not directly verified from docs (docs page returned empty on fetch)
- NetArchTest: MEDIUM — library confirmed at 1.3.2, .NET 10 compatibility via .NET Standard 2.0 base is assumed but not tested

**Research date:** 2026-03-13
**Valid until:** 2026-04-13 (stable tools; Nx patch versions may change)
