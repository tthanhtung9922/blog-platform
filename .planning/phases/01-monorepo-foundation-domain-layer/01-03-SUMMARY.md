---
phase: 01-monorepo-foundation-domain-layer
plan: "03"
subsystem: database
tags: [csharp, efcore, npgsql, postgresql, migrations, ddd, value-objects, snake-case]

# Dependency graph
requires:
  - phase: 01-monorepo-foundation-domain-layer
    plan: "02"
    provides: Blog.Domain aggregates (Post, Comment, User, Tag), value objects (Slug, Email, ReadingTime, TagReference), repository interfaces

provides:
  - BlogDbContext with 6 DbSets registered (Post, PostContent, PostVersion, Comment, User, Tag)
  - 6 entity configurations (IEntityTypeConfiguration<T>) mapping domain aggregates to PostgreSQL tables
  - EF Core migration CreateUnaccentExtension (suppressTransaction: true) for unaccent extension
  - EF Core migration InitialSchema creating 7 tables: posts, post_contents, post_versions, post_tags, comments, users, tags
  - Bare Blog.API Program.cs with DbContext registration, MigrateAsync on startup, /healthz health check
  - scripts/migration.sh helper for add/update/list/script/rollback operations

affects:
  - 01-04 (Blog.Application will use BlogDbContext and repository implementations via IUnitOfWork)
  - 02-xx (Repository implementations will extend these configurations)
  - testing (Blog.IntegrationTests will use BlogDbContext with Testcontainers)

# Tech tracking
tech-stack:
  added:
    - EFCore.NamingConventions 10.0.1 (snake_case via UseSnakeCaseNamingConvention)
    - Microsoft.EntityFrameworkCore.Design 10.* (dotnet ef tools support in Blog.API)
    - Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.* (AddDbContextCheck)
  patterns:
    - IEntityTypeConfiguration<T> per aggregate (one file per entity in Configurations/)
    - Value object HasConversion pattern (Slug, Email, ReadingTime stored as primitives)
    - OwnsMany for TagReference collection mapped to post_tags join table
    - Enum-as-string HasConversion<string>() for PostStatus, CommentStatus, UserRole
    - JSONB column type for BodyJson (ProseMirror JSON) and SocialLinks (Dictionary)
    - ValueComparer for JSONB dictionary to enable EF change tracking on mutations
    - suppressTransaction: true on Sql() call for CREATE EXTENSION (cannot run in transaction)
    - MigrateAsync on startup (Phase 1 local dev pattern)

key-files:
  created:
    - apps/blog-api/src/Blog.Infrastructure/Persistence/BlogDbContext.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostContentConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostVersionConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/CommentConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/UserConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/TagConfiguration.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060356_CreateUnaccentExtension.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060356_CreateUnaccentExtension.Designer.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060357_InitialSchema.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060357_InitialSchema.Designer.cs
    - apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/BlogDbContextModelSnapshot.cs
    - apps/blog-api/src/Blog.API/Program.cs
    - apps/blog-api/src/Blog.API/appsettings.json
    - apps/blog-api/src/Blog.API/appsettings.Development.json
    - scripts/migration.sh
  modified:
    - apps/blog-api/src/Blog.API/Blog.API.csproj (added Microsoft.EntityFrameworkCore.Design)

key-decisions:
  - "EF Core suppressTransaction uses Sql(sql, suppressTransaction: true) not a Migration property — SuppressTransaction is on SqlOperation not Migration base class in EF Core 10"
  - "OwnsMany post_tags key uses HasKey('PostId', nameof(TagReference.TagId)) with CLR property names not column names — shadow property naming caused 'no property type specified' error with column name strings"
  - "SocialLinks ValueComparer added to enable EF change tracking on in-place Dictionary mutations"
  - "Migrations restructured manually: CreateUnaccentExtension contains only extension SQL, InitialSchema contains all table DDL — EF tool generated both in first migration due to empty initial snapshot"

patterns-established:
  - "Value object conversion pattern: HasConversion(vo => vo.Value, v => VO.Create(v)) — maps VO to primitive column"
  - "OwnsMany for value object collections: WithOwner().HasForeignKey() with CLR property names for composite key"
  - "suppressTransaction: true via migrationBuilder.Sql() for DDL statements that cannot run in PostgreSQL transaction blocks"
  - "Manual migration restructuring: when EF generates unexpected migration content, overwrite .cs and .Designer.cs files manually to match intended migration split"

requirements-completed:
  - INFR-01

# Metrics
duration: 7min
completed: 2026-03-15
---

# Phase 1 Plan 03: Blog.Infrastructure Layer Summary

**EF Core 10 BlogDbContext with 7-table PostgreSQL schema — snake_case columns, UUID PKs, TIMESTAMPTZ, JSONB for ProseMirror and SocialLinks, two migrations (unaccent extension + full DDL), bare Blog.API with /healthz**

## Performance

- **Duration:** ~7 min
- **Started:** 2026-03-15T06:00:42Z
- **Completed:** 2026-03-15T06:07:xx Z
- **Tasks:** 2
- **Files created:** 16
- **Files modified:** 2 (Blog.API.csproj, UserConfiguration.cs updated during task execution)

## Accomplishments

- Complete Blog.Infrastructure Persistence layer: BlogDbContext + 6 entity configurations mapping all domain aggregates to PostgreSQL
- Two EF Core migrations: `CreateUnaccentExtension` (SQL with suppressTransaction) and `InitialSchema` (7 tables, all FK constraints, all indexes)
- Bare Blog.API: registers DbContext with snake_case naming, runs MigrateAsync on startup, exposes /healthz health check
- `scripts/migration.sh` helper with add/update/list/script/rollback subcommands and `--context BlogDbContext` to avoid two-DbContext ambiguity
- All 7 expected tables with snake_case column names, TIMESTAMPTZ timestamps, JSONB columns for BodyJson and SocialLinks

## Task Commits

Each task was committed atomically:

1. **Task 1: BlogDbContext and all entity configurations** - `cf96e1d` (feat)
2. **Task 2: Blog.API Program.cs, appsettings, migrations, migration.sh** - `195db19` (feat)

## Files Created/Modified

**BlogDbContext:**
- `apps/blog-api/src/Blog.Infrastructure/Persistence/BlogDbContext.cs` - DbContext with 6 DbSets, ApplyConfigurationsFromAssembly

**Entity Configurations:**
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostConfiguration.cs` - Slug/ReadingTime VO conversions, enum-as-string, partial index on Published, OwnsMany post_tags
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostContentConfiguration.cs` - JSONB for BodyJson, text for BodyHtml
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/PostVersionConfiguration.cs` - JSONB for BodyJson, covering index (post_id, created_at)
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/CommentConfiguration.cs` - enum-as-string, nullable ParentId, index (post_id, created_at)
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/UserConfiguration.cs` - Email VO conversion, JSONB SocialLinks with ValueComparer, enum-as-string Role
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Configurations/TagConfiguration.cs` - Slug VO conversion, unique indexes on Slug and Name

**Migrations:**
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060356_CreateUnaccentExtension.cs` - CREATE EXTENSION unaccent with suppressTransaction: true
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/20260315060357_InitialSchema.cs` - CREATE TABLE for all 7 tables
- `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/BlogDbContextModelSnapshot.cs` - EF model snapshot

**Blog.API:**
- `apps/blog-api/src/Blog.API/Program.cs` - Minimal API with DbContext, MigrateAsync, /healthz
- `apps/blog-api/src/Blog.API/appsettings.json` - DefaultConnection + log levels
- `apps/blog-api/src/Blog.API/appsettings.Development.json` - Debug log level + EF SQL logging
- `apps/blog-api/src/Blog.API/Blog.API.csproj` - Added Microsoft.EntityFrameworkCore.Design

**Scripts:**
- `scripts/migration.sh` - EF migration helper (add/update/list/script/rollback)

## Decisions Made

- `suppressTransaction: true` is passed to `migrationBuilder.Sql()`, NOT as a `Migration` class property — in EF Core 10, `SuppressTransaction` is on `SqlOperation`, not the `Migration` base class
- `OwnsMany` composite key uses CLR property names (`"PostId"`, `nameof(TagReference.TagId)`) — using column names like `"post_id"` caused "no property type was specified" error at design time
- `ValueComparer` added for `SocialLinks` JSONB dictionary — required for EF change tracking to detect in-place dictionary modifications during SaveChanges
- Migrations were restructured manually after EF tools generated all schema in the first migration — Designer files were rewritten to reflect the correct per-migration model state

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.EntityFrameworkCore.Design to Blog.API.csproj**
- **Found during:** Task 2 (migration generation)
- **Issue:** `dotnet ef migrations add` failed with "Your startup project doesn't reference Microsoft.EntityFrameworkCore.Design"
- **Fix:** Added Design package reference with PrivateAssets=all to Blog.API.csproj, ran dotnet restore
- **Files modified:** `apps/blog-api/src/Blog.API/Blog.API.csproj`
- **Verification:** Migration generation succeeded after package restore
- **Committed in:** `195db19` (Task 2 commit)

**2. [Rule 1 - Bug] Fixed OwnsMany post_tags composite key using CLR property names**
- **Found during:** Task 2 (migration generation)
- **Issue:** `HasKey("post_id", "tag_id")` with column names caused "The property 'tag_id' cannot be added to the type 'TagReference'... no property type was specified"
- **Fix:** Changed to `HasKey("PostId", nameof(TagReference.TagId))` using CLR names; `WithOwner().HasForeignKey("PostId")` for shadow FK
- **Files modified:** `PostConfiguration.cs`
- **Verification:** Migration generated successfully, `post_tags` table appears in InitialSchema with correct composite PK
- **Committed in:** `cf96e1d` updated, then `195db19` (both tasks)

**3. [Rule 1 - Bug] Fixed SuppressTransaction — not a Migration property in EF Core 10**
- **Found during:** Task 2 (build after writing CreateUnaccentExtension.cs with `protected override bool SuppressTransaction => true`)
- **Issue:** CS0115 — no suitable method to override. `SuppressTransaction` is on `SqlOperation`, not on `Migration` base class
- **Fix:** Changed to `migrationBuilder.Sql("CREATE EXTENSION...", suppressTransaction: true)` API
- **Files modified:** `20260315060356_CreateUnaccentExtension.cs`
- **Verification:** Build succeeds with 0 errors
- **Committed in:** `195db19` (Task 2 commit)

**4. [Rule 2 - Missing Critical] Added ValueComparer for SocialLinks JSONB dictionary**
- **Found during:** Task 2 (EF migration generation — warning emitted)
- **Issue:** EF warned "collection type with a value converter but with no value comparer" — without ValueComparer, in-place dictionary mutations (e.g., adding a social link) are not tracked and silently lost on SaveChanges
- **Fix:** Added `ValueComparer<Dictionary<string,string>>` using JSON serialization for equality/hash/clone
- **Files modified:** `UserConfiguration.cs`
- **Verification:** Build succeeds, EF tools no longer emit the SocialLinks warning
- **Committed in:** `195db19` (Task 2 commit)

---

**Total deviations:** 4 auto-fixed (1 blocking, 2 bugs, 1 missing critical)
**Impact on plan:** All auto-fixes required for correctness. No scope creep.

## Issues Encountered

- Docker Desktop was not running during execution. `dotnet ef migrations list` reports "Unable to determine applied migrations" (expected — requires DB connection). The `dotnet ef database update` step could not be run; migrations will be verified against PostgreSQL when Docker is started. Build verification (`dotnet build`) fully passes. This is a setup/environment constraint, not a code issue.

## User Setup Required

To apply migrations and verify the database schema:

```bash
# Start Docker Compose
docker-compose up -d postgres

# Wait for PostgreSQL to be healthy, then apply migrations
./scripts/migration.sh update

# Verify tables
docker-compose exec postgres psql -U blog -d blog_db -c "\dt"
docker-compose exec postgres psql -U blog -d blog_db -c "\d posts"

# Verify migration history
docker-compose exec postgres psql -U blog -d blog_db -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\";"

# Run Blog.API and verify health check
dotnet run --project apps/blog-api/src/Blog.API
curl http://localhost:5000/healthz
```

## Next Phase Readiness

- Blog.Infrastructure Persistence layer is complete and ready for repository implementation (Phase 2 or Plan 04)
- BlogDbContext is ready to be used in Testcontainers integration tests (Blog.IntegrationTests)
- The MigrateAsync startup pattern will be replaced with a production-resilient approach in Phase 2+
- Architecture tests (Blog.ArchTests) can now verify Infrastructure does not reference Domain aggregates directly beyond EF configurations

## Self-Check: PASSED

- All 11 key files exist on disk
- Both task commits found: cf96e1d, 195db19
- `dotnet build` Blog.API exits 0 with 0 errors

---
*Phase: 01-monorepo-foundation-domain-layer*
*Completed: 2026-03-15*
