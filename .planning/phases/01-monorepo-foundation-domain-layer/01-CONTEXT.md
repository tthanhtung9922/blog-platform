# Phase 1: Monorepo Foundation + Domain Layer - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Scaffold the Nx monorepo with correct project graph boundaries, implement all Domain aggregates and value objects in pure C# (zero infrastructure references), create a minimal Blog.Infrastructure with DbContext + EF Core entity configurations to enable migrations, apply EF Core migrations against PostgreSQL 18 (including the unaccent extension), configure Docker Compose for the local dev environment, and enforce architecture boundaries via Blog.ArchTests from day one.

Blog.Infrastructure in Phase 1 is intentionally minimal: DbContext + EF configs + migrations only. All repository implementations, Redis, IUnitOfWork, and other infrastructure services are Phase 2.

</domain>

<decisions>
## Implementation Decisions

### Domain model — Aggregates and entities

- **Post aggregate:** Post (root) + PostContent (body_json + body_html, 1-to-1 entity) + PostVersion (snapshot on each save entity). All three live inside the Post aggregate boundary; EF Core loads them together.
- **Comment aggregate:** Comment aggregate root with parent_id for 1-level nesting. The Comment domain enforces the nesting constraint: a reply cannot itself have a parent (`Comment.AddReply()` throws `DomainException` if the parent already has a `ParentId`). This is a domain rule, not an Application layer concern.
- **User aggregate:** Full profile fields from day one — Id (Guid, shared with IdentityUser), Email (value object), DisplayName, Bio, AvatarUrl, Website, SocialLinks, Role, IsActive, CreatedAt. Defining all fields in Phase 1 avoids migration changes in later phases.
- **Tag:** Tag has its own aggregate root for CRUD (TagId, Name, Slug value object). Post references tags by TagId only — Post holds a collection of TagIds (value objects), not Tag entities. EF Core maps this via a post_tags join table.
- All aggregate roots inherit from `AggregateRoot<TId>` base class in `Blog.Domain/Common/`.

### Domain model — Value objects

All value objects implemented in Phase 1:
- `Slug` — immutable, URL-safe, validated on construction
- `Email` — immutable, format-validated
- `ReadingTime` — computed from word count
- `Tag` (as a value object on Post, containing TagId + TagSlug)
- PostStatus enum: Draft, Published, Archived
- CommentStatus enum: Pending, Approved, Rejected

### Domain model — Domain events

All domain events defined in Phase 1 as C# `record` types in `Blog.Domain/DomainEvents/`. No handlers yet — handlers are wired in Phase 2+.

Events to define:
- `PostPublishedEvent`, `PostUpdatedEvent`, `PostArchivedEvent`
- `CommentAddedEvent`, `CommentApprovedEvent`, `CommentRejectedEvent`, `CommentDeletedEvent`
- `UserProfileUpdatedEvent`, `UserBannedEvent`
- `TagCreatedEvent`, `TagDeletedEvent`

### Domain model — Repository interfaces

All 4 repository interfaces defined in `Blog.Domain/Repositories/` in Phase 1:
- `IPostRepository`, `ICommentRepository`, `IUserRepository`, `ITagRepository`

These are pure domain contracts. Phase 2 implements them in Blog.Infrastructure.

### Phase 1 vs Phase 2 infrastructure split

Phase 1 creates a **minimal Blog.Infrastructure** scoped to migrations only:
- `BlogDbContext` with all aggregate DbSets registered
- All `IEntityTypeConfiguration<T>` files for every aggregate (snake_case column names, UUID PKs, TIMESTAMPTZ timestamps, EF Core conventions per database rules)
- An `CreateUnaccentExtension` migration that enables `unaccent` with `SuppressTransaction = true` (runs first, before schema migrations)
- The initial schema migration covering all Phase 1 aggregate tables

Phase 1 also creates a **bare Blog.API** project with just Program.cs: registers DbContexts, runs `MigrateAsync()` on startup, adds a health check endpoint. No controllers. Required so `nx build blog-api` passes and migrations run via the app entrypoint.

Phase 2 scope (NOT included in Phase 1): repository implementations, Redis configuration, IUnitOfWork, IIdentityService, IEmailService, IStorageService, all other Infrastructure services.

### Architecture tests

`Blog.ArchTests` (NetArchTest) enforces from Phase 1:
1. **Layer boundary rules** — Domain has no Infrastructure refs; Application has no API refs; Infrastructure has no API refs. Written now and run in CI; pass vacuously in Phase 1 because Phase 2+ layers don't exist yet.
2. **Domain model integrity rules:**
   - Value objects in `Blog.Domain/ValueObjects/` have no public property setters (immutability enforced)
   - Domain events in `Blog.Domain/DomainEvents/` are C# `record` types
   - Classes in `Blog.Domain/Aggregates/` folders inherit from `AggregateRoot<TId>`

### Docker Compose

- **Single `docker-compose.yml`** at repo root for local development. CI uses Testcontainers directly (added in Phase 2) — no compose needed in CI.
- **Services:** PostgreSQL 18, Redis 8, MinIO
- **Postal (email):** Deferred to Phase 3 when auth email flows are built. Not in Phase 1 docker-compose.yml.
- **Fixed ports** (no .env override needed for Phase 1):
  - PostgreSQL: 5432
  - Redis: 6379
  - MinIO API: 9000, MinIO console: 9001
- **PostgreSQL initialization:** A `docker/init.sql` script is mounted via `docker-entrypoint-initdb.d/` that creates the database and enables the unaccent extension. EF Core migrations handle schema after that.
- **MinIO initialization:** A one-shot `mc` (MinIO client) init container runs on startup and creates the `blog-media` bucket, then exits. Developer gets a ready-to-use MinIO with no manual steps.
- **No seed data in Phase 1.** Database starts empty. Seed data (admin user, test content) belongs in Phase 3+ when auth features exist.

### Claude's Discretion

- Exact `AggregateRoot<TId>` base class implementation (domain events collection, `AddDomainEvent()` helper)
- EF Core entity configuration details (cascade deletes, index strategies)
- Nx project.json configuration details (tags, implicit dependencies, affected graph)
- Exact PostgreSQL init.sql content beyond enabling unaccent

</decisions>

<specifics>
## Specific Ideas

- `@nx/dotnet` plugin (Nx 22+) must be used — NOT `@nx-dotnet/core` (deprecated September 2025). This is a confirmed decision from project research (STATE.md).
- The `CreateUnaccentExtension` migration must use `SuppressTransaction = true` — PostgreSQL extensions cannot be created in a transaction.
- `blog-web` and `blog-admin` project.json files must declare `shared-contracts` as an implicit dependency, reflected in the Nx project graph (success criteria #4).
- EF Core entity configurations must use snake_case naming (PostgreSQL convention) — all column and table names via `UseSnakeCaseNamingConvention()` or explicit `.HasColumnName()` per the database rules.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets

- No existing application code — this is Phase 1, starting from scratch.
- Existing files: `CLAUDE.md`, `README.md`, `CONTRIBUTING.md`, `.gitignore` — project-level documentation only.

### Established Patterns

All patterns are defined in CLAUDE.md and `.claude/rules/` (no code exists yet):
- Layer dependency: Domain → Application → Infrastructure → Presentation
- IdentityUser (Infrastructure) and User (Domain) are completely separate — no inheritance, no navigation properties, shared GUID only
- snake_case for all PostgreSQL table and column names
- UUID primary keys via `gen_random_uuid()`
- TIMESTAMPTZ for all timestamp columns

### Integration Points

- `Blog.Domain` → referenced by `Blog.Application` (Phase 2), `Blog.Infrastructure` (Phase 1 minimal + Phase 2 full), `Blog.API` (Phase 1 minimal)
- `Blog.Infrastructure` → referenced by `Blog.API` for DbContext registration and migration runner
- `shared-contracts` → must be declared as implicit dependency in `blog-web/project.json` and `blog-admin/project.json`
- `docker-compose.yml` → used by all developers for local dev; Testcontainers in Phase 2 replaces it in CI

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within Phase 1 scope.

</deferred>

---

*Phase: 01-monorepo-foundation-domain-layer*
*Context gathered: 2026-03-13*
