# run-ef-migration

Create, apply, review, or rollback EF Core migrations following the project's naming conventions, backward-compatibility rules, and production safety procedures.

## Arguments

- `action` (required) — One of: `create`, `apply`, `rollback`, `status`, `script`
- `name` (required for `create`) — Migration name following naming conventions (e.g., `AddPostVersionsTable`)
- `target` (optional for `rollback`) — Target migration name to rollback to
- `environment` (optional) — `local`, `staging`, `production` (defaults to `local`)

## Instructions

You are managing EF Core migrations for the blog-platform PostgreSQL database. Migrations span two separate DbContexts: `BlogDbContext` (domain entities) and `IdentityDbContext` (ASP.NET Identity).

### Migration Commands

All commands are run from `apps/blog-api/src/Blog.API`:

**Create a migration:**
```bash
dotnet ef migrations add {MigrationName} \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --output-dir Persistence/Migrations
```

**Apply migrations (local):**
```bash
dotnet ef database update \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj
```

**Check migration status:**
```bash
dotnet ef migrations list \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj
```

**Rollback to a specific migration:**
```bash
dotnet ef database update {TargetMigrationName} \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj
```

**Generate idempotent SQL script (for production):**
```bash
dotnet ef migrations script {FromMigration} {ToMigration} \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --idempotent \
  --output migrations.sql
```

### Migration Naming Conventions

| Pattern | Example | Usage |
|---|---|---|
| `Add{Entity}` | `AddPostVersionsTable` | New table |
| `Add{Column}To{Table}` | `AddCoverImageUrlToPosts` | New column |
| `Remove{Column}From{Table}` | `RemoveIsDeletedFromPosts` | Drop column |
| `Rename{Entity}` | `RenameTagsSlugColumn` | Column rename |
| `AddIndex{Name}` | `AddIndexPostsPublishedAt` | Index creation |
| `Alter{Column}In{Table}` | `AlterExcerptLengthInPosts` | Type/constraint change |
| `Seed{Data}` | `SeedDefaultRoles` | Data seeding |
| `Create{Extension}` | `CreateUnaccentExtension` | PostgreSQL extension |

### Backward Compatibility Matrix

| Change | Compatible? | Strategy |
|---|---|---|
| Add nullable column | YES | Deploy migration first, code after |
| Add NOT NULL + default | YES | Ensure default value is sensible |
| **Delete column** | NO | 2-phase: (1) code stops using → deploy, (2) migration drops column |
| **Rename column** | NO | 2-phase: (1) add new column + copy data → deploy, (2) drop old column |
| Type change | DEPENDS | Thorough testing, may need data migration script |
| Add index | YES | Use `CREATE INDEX CONCURRENTLY` for large tables |
| Delete index | YES | Ensure no queries depend on it |

### Large Table Operations (> 1M rows)

EF Core cannot use `CONCURRENTLY` keyword. Use raw SQL in migration:

```csharp
public partial class AddIndexPostsNewColumn : Migration
{
    // CRITICAL: CONCURRENTLY cannot run inside a transaction
    protected override bool SuppressTransaction => true;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_posts_new_column
            ON posts (new_column);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP INDEX CONCURRENTLY IF EXISTS idx_posts_new_column;
        ");
    }
}
```

### Pre-Deployment Checklist

Before applying any migration:

1. [ ] **Review generated SQL** — Read both `Up()` and `Down()` methods
2. [ ] **Check backward compatibility** — Use 2-phase pattern for breaking changes
3. [ ] **CONCURRENTLY for large tables** — Raw SQL + `SuppressTransaction = true`
4. [ ] **Data migration script** — If transforming data (separate from schema migration)
5. [ ] **Backup database** — Before production deployment
6. [ ] **Generate idempotent script** — Use `--idempotent` flag for production

### Rollback Procedures

**Local/Staging:**
```bash
dotnet ef database update {TargetMigrationName} \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj
```

**Production (manual SQL):**
```bash
# 1. Generate rollback SQL script
dotnet ef migrations script {CurrentMigration} {TargetMigration} \
  --idempotent \
  --output rollback.sql

# 2. Review the script carefully (check for data loss!)

# 3. Backup before applying
pgbackrest --stanza=blog-db --type=incr backup

# 4. Apply rollback
psql -h $DB_HOST -U $DB_USER -d blog_db -f rollback.sql

# 5. Verify
psql -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;"
```

**Emergency PITR (Point-in-Time Recovery):**
```bash
pgbackrest --stanza=blog-db \
  --type=time \
  --target="{timestamp-before-migration}" \
  restore

# WARNING: Must redeploy API code matching the old schema version
```

### Database Schema Conventions

- Tables: `snake_case` plural (`posts`, `post_contents`, `user_profiles`)
- Columns: `snake_case` (`created_at`, `is_featured`, `author_id`)
- Primary keys: Always `id` (UUID, `gen_random_uuid()`)
- Foreign keys: `{referenced_table_singular}_id`
- Indexes: `idx_{table}_{columns}`
- Unique constraints: `uq_{table}_{columns}`
- All tables include `created_at TIMESTAMPTZ NOT NULL DEFAULT now()` and `updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`

### EF Core Configuration

Entity configurations go in `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`:

```csharp
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Title)
            .HasColumnName("title")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Slug)
            .HasColumnName("slug")
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(s => s.Value, v => Slug.FromExisting(v));

        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        // Partial index for published posts
        builder.HasIndex(p => p.Status)
            .HasFilter("status = 'Published'");
    }
}
```

### CI/CD Integration

Migrations are validated in CI before merge:

1. `has-pending-model-changes` — Detects unmigrated model changes
2. Apply all migrations (Up validation)
3. Rollback all migrations (Down validation)
4. Re-apply (idempotency check)
5. Generate SQL script artifact

**Deployment flow:**
- PR merged → CI validates migrations → Staging auto-applies + smoke test → Production requires MANUAL APPROVAL
- Destructive migrations require 2 reviewers
- Migration SQL is attached as CI artifact for review
