# Architecture Research

**Domain:** Production Blog Platform (Vietnamese content, self-hosted)
**Researched:** 2026-03-12
**Confidence:** HIGH (architecture is fully specified in ADRs; research validates and elaborates)

## Standard Architecture

### System Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                                    │
├───────────────────────────────┬──────────────────────────────────────────┤
│  blog-web (Next.js 16.1)      │  blog-admin (Next.js 16.1)               │
│  SSG/ISR — public reader      │  CSR/SSR — CMS dashboard                 │
│  - Post list, post detail     │  - Rich text editor (Tiptap v3)          │
│  - Tag/author filtering       │  - Post lifecycle management             │
│  - Search, comments           │  - Comment moderation                    │
│  - Auth: NextAuth v5 (Reader) │  - User/role admin                       │
│  - CASL permissions (read)    │  - CASL permissions (write)              │
└───────────────┬───────────────┴──────────────────┬───────────────────────┘
                │ shared-contracts                  │ shared-ui
                │ (OpenAPI-generated TS types,       │ (Avatar, Badge,
                │  permissions.ts, roles.ts)         │  Button, etc.)
                └──────────────────┬────────────────┘
                                   │ HTTPS REST (OpenAPI 3.1)
                                   ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER (Blog.API)                     │
│  Controllers: Posts, Comments, Reactions, Users, Auth                   │
│  Middleware: ExceptionHandling → RateLimiting (Redis) → RequestLogging   │
└──────────────────────────────────┬───────────────────────────────────────┘
                                   │ MediatR dispatch
                                   ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                       APPLICATION LAYER (Blog.Application)               │
│  MediatR Pipeline (in order):                                            │
│    1. ValidationBehavior (FluentValidation)                              │
│    2. LoggingBehavior                                                    │
│    3. AuthorizationBehavior (policy checks)                              │
│    4. CachingBehavior (Redis, opt-in via ICacheableQuery)                │
│                                                                          │
│  Features (CQRS): Posts | Comments | Reactions | Users | Auth           │
│  Abstractions: ICurrentUserService, IEmailService, IStorageService       │
└──────────┬────────────────────────────────────────┬───────────────────────┘
           │ repository interfaces                   │ domain services
           ▼                                         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                          DOMAIN LAYER (Blog.Domain)                      │
│  Aggregates: Post (+ PostContent, PostVersion)                           │
│              Comment (+ Reply)                                           │
│              User (+ UserProfile)                                        │
│  Value Objects: Slug, Tag, Email, ReadingTime                            │
│  Domain Events: PostPublished, PostArchived, CommentAdded,               │
│                 UserRegistered, UserProfileUpdated                        │
│  Repository Interfaces: IPostRepository, ICommentRepository,             │
│                         IUserRepository                                  │
└──────────────────────────────────────────────────────────────────────────┘
           │ implements
           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      INFRASTRUCTURE LAYER (Blog.Infrastructure)          │
│  Persistence:  BlogDbContext (EF Core 10) → PostgreSQL 18                │
│  Identity:     IdentityDbContext + IdentityService + JwtTokenService     │
│  Caching:      RedisCacheService → Redis 8 (cache-aside, SCAN+DEL)      │
│  Storage:      MinioStorageService → MinIO                               │
│  Email:        PostalEmailService (primary) / SendGridEmailService (fb)  │
│  Search:       PostgresFullTextSearch (Phase 1) / Meilisearch (Phase 3) │
│  Cross-ctx tx: IUnitOfWork (shared NpgsqlConnection + transaction)       │
└──────────────────────────────────────────────────────────────────────────┘
           │ connects to
           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         INFRASTRUCTURE SERVICES                          │
│  PostgreSQL 18 │ Redis 8 │ MinIO │ Postal (email) │ Kubernetes           │
│  GitHub Actions CI/CD │ Prometheus + Grafana + Loki (observability)      │
└──────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Key Constraint |
|-----------|----------------|----------------|
| `blog-web` | Public reading experience via SSG/ISR; comments and reactions via client-side API calls | SSG pages only for published content; on-demand ISR revalidation triggered via `api/revalidate` |
| `blog-admin` | CMS dashboard with Tiptap v3 editor, post lifecycle, moderation, user/role management | All routes protected; CASL permission checks before write operations |
| `shared-contracts` | Auto-generated TypeScript types from OpenAPI spec; `roles.ts`; `permissions.ts` | Source of truth is `blog-api` OpenAPI spec — never edit generated types by hand |
| `shared-ui` | Primitive React components used by both frontends | No API calls, no auth logic — pure presentational |
| `Blog.Domain` | Core business rules: aggregates, value objects, domain events, repository interfaces | Zero external dependencies — no EF Core, no ASP.NET, no NuGet references outside domain utilities |
| `Blog.Application` | Use cases as CQRS commands/queries via MediatR; pipeline behaviors for cross-cutting concerns | Depends only on Domain; never references Infrastructure directly |
| `Blog.Infrastructure` | All I/O: database, Redis, MinIO, email, search | Implements Domain interfaces; depends on Application abstractions |
| `Blog.API` | HTTP surface: controllers, middleware, DI wiring | Entry point; wires DI registrations; does not contain business logic |

## Recommended Project Structure

The project structure is fully specified in `docs/blog-platform/02-folder-structure.md`. The critical structural rules are:

```
apps/
├── blog-api/src/
│   ├── Blog.Domain/           # No outward project references
│   ├── Blog.Application/      # References: Domain only
│   ├── Blog.Infrastructure/   # References: Domain + Application
│   └── Blog.API/              # References: Application + Infrastructure
├── blog-web/                  # References: shared-contracts, shared-ui
└── blog-admin/                # References: shared-contracts, shared-ui

libs/
├── shared-contracts/          # Generated — do not hand-edit
└── shared-ui/                 # Pure presentational components

scripts/
└── gen-types.sh               # Regenerates shared-contracts from OpenAPI spec
```

### Structure Rationale

- **Domain at the center:** `Blog.Domain` has no outbound references. This is the invariant; all other layers adapt to it.
- **Application as orchestrator:** `Blog.Application` has no Infrastructure references — only Domain and its own abstractions (`IEmailService`, `IStorageService`). This enables testing handlers in isolation with mock implementations.
- **Infrastructure as adapter:** All third-party integrations (EF Core, Redis, MinIO) live here and implement interfaces owned by Domain or Application.
- **Separate frontend apps:** `blog-web` is optimized for CDN-level SSG caching; `blog-admin` is optimized for real-time interactivity. Merging them would force compromises on both.
- **`shared-contracts` as integration seam:** The OpenAPI spec is the contract. `gen-types.sh` regenerates TypeScript types on every API change. This prevents silent contract drift between frontend and backend.

## Architectural Patterns

### Pattern 1: CQRS with MediatR Pipeline Behaviors

**What:** Commands (write) and Queries (read) are separate request objects dispatched via MediatR. Cross-cutting concerns (validation, logging, authorization, caching) are injected as ordered pipeline behaviors.

**When to use:** Any operation that modifies state (command) or reads state (query). The pipeline ensures validation runs before authorization, and authorization runs before caching — never cached invalid or unauthorized responses.

**Pipeline registration order (critical — MediatR executes in registration order):**
```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

**Trade-offs:** Adds boilerplate per feature (Command + Handler + Validator classes). Returns significant dividends in testability — each handler is a pure class testable without HTTP context.

### Pattern 2: Domain Events for Cache Invalidation

**What:** After `SaveChanges()` succeeds, MediatR dispatches Domain Events (e.g., `PostPublishedEvent`). `INotificationHandler` implementations call `IRedisCacheService.RemoveByPatternAsync()` with wildcard patterns to invalidate affected cache keys.

**When to use:** Any mutation that should invalidate cached read results. This decouples write handlers from cache logic — write handlers raise events; cache invalidation handlers respond.

**Trade-offs:** Asynchronous invalidation means a brief window where stale cache may be served between commit and invalidation. Acceptable for a blog; unacceptable for financial data.

**Key pattern — Lua script for atomic wildcard invalidation:**
```
post-list:*  →  SCAN + DEL via Lua (never KEYS * in production)
```

### Pattern 3: Dual DbContext with Shared Connection for Cross-Context ACID Transactions

**What:** `IdentityDbContext` (ASP.NET Identity — `AspNetUsers`) and `BlogDbContext` (domain — `users`) share a single `NpgsqlConnection` and transaction during Register and Ban operations.

**When to use:** Any operation that must atomically write to both `AspNetUsers` and domain tables. Without this, a failure between the two writes creates an orphaned `IdentityUser` (auth record exists, domain record does not).

```csharp
// IUnitOfWork wraps: shared connection + transaction
// IdentityDbContext.Database.UseTransaction(transaction)
// BlogDbContext.Database.UseTransaction(transaction)
// → single await transaction.CommitAsync()
```

**Trade-offs:** Couples both DbContexts to same PostgreSQL instance. This is correct for Phase 1 monolith. Phase 3+ microservices migration switches to Saga/compensating actions (ADR-007 Option B).

### Pattern 4: ICacheableQuery Opt-In Caching

**What:** Only queries that implement `ICacheableQuery` are cached by `CachingBehavior`. Queries declare their own `CacheKey` and `CacheDuration`. Queries without this interface pass through the behavior unchanged.

**When to use:** Public read queries with predictable cache keys (post by slug, post list, user profile). Never for: `GetCurrentUser`, `GetUserList` (admin), or any query with security-sensitive real-time requirements.

**Trade-offs:** Developer must explicitly opt in — safer than opt-out (prevents accidental caching of sensitive queries).

### Pattern 5: SSG with On-Demand ISR Revalidation

**What:** `blog-web` pre-renders published posts at build time (SSG). On-demand ISR via `POST /api/revalidate` allows targeted cache invalidation when a post is published or updated, without a full rebuild.

**When to use:** This is the standard pattern for blog platforms — CDN-served static HTML achieves the fastest possible Time-to-First-Byte for readers.

**Trade-offs:** New posts are not instantly visible without triggering revalidation. The `blog-api` should call the `blog-web` revalidation endpoint as part of `PostPublishedEvent` handling.

## Data Flow

### Request Flow — Public Post Read (Cached Path)

```
Reader browser
    ↓ (CDN hit → static HTML, ~0ms TTFB)
blog-web SSG page (pre-rendered at build/revalidation)
    ↓ (cache miss — post content below the fold, dynamic)
blog-web API client (typed fetch, shared-contracts types)
    ↓ HTTPS GET /api/posts/{slug}
Blog.API PostsController
    ↓ MediatR.Send(new GetPostBySlugQuery(slug))
ValidationBehavior → passes (no input to validate for slug)
LoggingBehavior → records request
AuthorizationBehavior → passes (public endpoint)
CachingBehavior → checks Redis key "post:slug:{slug}"
    → Cache HIT: return cached PostDto (~1ms)
    → Cache MISS: execute handler
        GetPostBySlugQueryHandler
            ↓ IPostRepository.GetBySlugAsync(slug)
            ↓ BlogDbContext → PostgreSQL SELECT
            ← PostDto
        CachingBehavior stores PostDto in Redis (TTL: 1h)
    ← PostDto
Blog.API serializes → JSON response
blog-web renders PostContent (Tiptap read-only EditorContent)
```

### Request Flow — Post Publish (Write Path with Cache Invalidation)

```
Editor in blog-admin
    ↓ clicks "Publish"
blog-admin API client → HTTPS PUT /api/posts/{id}/publish
    ↓ JWT in Authorization header
Blog.API PostsController.Publish()
    ↓ MediatR.Send(new PublishPostCommand(id))
ValidationBehavior → validates command
LoggingBehavior → records
AuthorizationBehavior → checks CanPublishPostPolicy (Editor/Admin only)
CachingBehavior → bypasses (Commands do not implement ICacheableQuery)
PublishPostCommandHandler
    ↓ IPostRepository.GetByIdAsync(id)
    ↓ post.Publish() → raises PostPublishedEvent
    ↓ IPostRepository.UpdateAsync(post)
    ↓ BlogDbContext.SaveChangesAsync()
    ↓ MediatR dispatches PostPublishedEvent
PostPublishedEventHandler
    ↓ IRedisCacheService.RemoveByPatternAsync("post:slug:{slug}")
    ↓ IRedisCacheService.RemoveByPatternAsync("post-list:*")
    ↓ IRedisCacheService.RemoveByPatternAsync("post-list:author:{authorId}:*")
    ↓ blog-web revalidation webhook → POST /api/revalidate?slug={slug}
← 200 OK to blog-admin
```

### Request Flow — User Registration (Cross-Context Transaction)

```
Visitor in blog-web
    ↓ submits registration form
blog-web → HTTPS POST /api/auth/register
Blog.API AuthController.Register()
    ↓ MediatR.Send(new RegisterCommand(email, password, displayName))
RegisterCommandHandler
    ↓ IUnitOfWork.BeginTransactionAsync()
        ↓ IdentityService.CreateAsync(email, password)
          → writes to AspNetUsers (IdentityDbContext, shared conn)
        ↓ IUserRepository.AddAsync(new User(identityId))
          → writes to users (BlogDbContext, shared conn)
        ↓ IUnitOfWork.CommitAsync()
          → single PostgreSQL COMMIT (atomic)
    ↓ raises UserRegisteredEvent
UserRegisteredEventHandler
    ↓ IEmailService.SendVerificationEmailAsync(email)
← 201 Created
```

### Key Data Flows Summary

1. **Read path (public):** CDN → blog-web SSG → Redis cache → PostgreSQL. Cache eliminates most DB reads.
2. **Write path (admin):** blog-admin → Blog.API → MediatR pipeline → Domain aggregate → EF Core → PostgreSQL → Domain Events → cache invalidation + ISR revalidation.
3. **Auth flow:** NextAuth v5 issues session; JWT passed to Blog.API; `CurrentUserService` extracts identity; `AuthorizationBehavior` checks CASL-equivalent ASP.NET policies.
4. **Type contracts:** OpenAPI spec in Blog.API → `gen-types.sh` → `shared-contracts/api.types.ts` → consumed by both blog-web and blog-admin.
5. **Media upload:** blog-admin → `IStorageService` → MinIO; URL stored in `posts.cover_image_url`.

## Suggested Build Order

Dependencies dictate this order. Each phase unblocks the next.

### Phase 1: Monorepo Foundation + Domain Layer

Build first because everything else depends on it.

- Nx workspace scaffold with `@nx-dotnet/core` plugin
- `Blog.Domain`: aggregates, value objects, domain events, repository interfaces, exceptions
- Database schema + EF Core migrations (PostgreSQL 18 setup, Vietnamese FTS config, `unaccent` extension)
- Docker Compose for local dev (PostgreSQL, Redis, MinIO)
- `Blog.ArchTests` (NetArchTest rules) — enforce layer dependency rules early, not retroactively

**Why first:** Domain layer has zero external dependencies. It is the invariant foundation. Architecture tests prevent the layer from being accidentally polluted.

### Phase 2: Infrastructure + Application Shell

Build after Domain because Infrastructure implements Domain interfaces, and Application uses them.

- `Blog.Infrastructure`: BlogDbContext, EF configurations, repository implementations, IdentityDbContext, `IUnitOfWork`
- `Blog.Application`: MediatR setup, all four pipeline behaviors (in correct registration order), `ICacheableQuery` interface
- `Blog.Infrastructure.Caching`: Redis, `RedisCacheService`, `CacheKeys.cs`
- `Blog.Infrastructure.Storage`: MinIO integration
- `Blog.UnitTests` + `Blog.IntegrationTests` scaffolding (Testcontainers)

**Why second:** Cannot implement Application use cases until Infrastructure adapters exist to inject. Cannot write integration tests until EF Core and repositories are wired.

### Phase 3: Auth Feature (Cross-Context Critical Path)

Build before any other features because auth underpins all RBAC enforcement.

- ASP.NET Identity setup + `IdentityService` + `JwtTokenService`
- `RegisterCommand`, `LoginCommand`, `RefreshTokenCommand` handlers
- `CurrentUserService`
- `AuthorizationBehavior` fully wired
- `shared-contracts`: initial `roles.ts`, `permissions.ts`
- Blog.API auth endpoints + JWT middleware

**Why third:** Every subsequent feature (post creation, comment moderation, admin) needs an authenticated user context. Building auth first means all feature tests can exercise the full auth flow.

### Phase 4: Post Feature (Core Business Logic)

The primary domain feature — all other features relate to posts.

- `Post` aggregate fully implemented (create, publish, archive, versioning)
- `Post` commands: CreatePost, UpdatePost, PublishPost, ArchivePost, DeletePost
- `Post` queries: GetPostBySlug, GetPostList, GetPostsByTag, GetPostsByAuthor (with `ICacheableQuery`)
- `PostPublishedEvent`, `PostArchivedEvent`, `PostUpdatedEvent` handlers (cache invalidation)
- `blog-api` Posts and Tags controllers
- OpenAPI spec for post endpoints → run `gen-types.sh` → populate `shared-contracts`
- `blog-web`: SSG post list and post detail pages (read-only, public)
- `blog-admin`: post list, new post, edit post pages with Tiptap v3 editor
- Tiptap content: store `body_json` (ProseMirror) + `body_html` (pre-rendered); render in blog-web via `EditorContent` read-only mode

**Why fourth:** Posts are the primary content unit. Tag, comment, and reaction features all depend on posts existing.

### Phase 5: Social Features (Comments, Reactions, User Profiles)

Build after posts because comments and reactions reference posts.

- `Comment` aggregate: AddComment, DeleteComment, ModerateComment, nested reply (1 level)
- `CommentAddedEvent`, `CommentDeletedEvent` → cache invalidation for `comments:post:{postId}:*`
- Reactions: ToggleLike, ToggleBookmark (idempotent, unique constraint enforcement)
- User profile: UpdateProfile command, GetUserProfile query (with `ICacheableQuery`)
- Public profile page (`blog-web`: `/authors/[username]`)
- Comment list and form in `blog-web` (client-side, requires NextAuth session for submit)

**Why fifth:** These features add social interactions on top of existing posts/users. No earlier feature depends on them.

### Phase 6: Admin Features (User Management, Search)

Build after core features stabilize.

- Admin: GetUserList, AssignRole, BanUser commands (shared DbConnection for BanUser cross-context)
- PostgreSQL FTS: custom `vietnamese` text search configuration, GIN index, `SearchPostsQuery`
- `blog-admin`: user list, user detail, role assignment, comment moderation queue, settings
- Media upload flow: `blog-admin` → Blog.API → MinIO
- `blog-web`: search page (`/search`)

**Why sixth:** User management and search are operational features, not user-facing primary paths. They can be built and released after core content features are stable.

### Phase 7: Infrastructure + Observability

Deploy the platform and instrument it.

- Docker multi-stage builds for all three apps
- Kubernetes manifests (base + dev/staging/prod overlays), HPA configs
- GitHub Actions: CI (lint, unit tests, integration tests, build), CD staging, CD prod (manual approval)
- Prometheus + Grafana + Loki: metrics, dashboards, log aggregation
- Rate limiting middleware (Redis-backed distributed rate limit)
- `gen-types.yml` GitHub Actions workflow (auto-PR on API changes)

**Why seventh:** Infrastructure and observability are operational concerns. Building them last avoids re-configuring Kubernetes manifests as the API surface changes during earlier phases.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| PostgreSQL 18 | EF Core 10 via `Npgsql` provider; `BlogDbContext` + `IdentityDbContext` | Vietnamese FTS requires `unaccent` extension pre-created in migration |
| Redis 8 | `StackExchange.Redis` client wrapped in `RedisCacheService` | Use `SCAN + DEL` Lua script for wildcard invalidation — never `KEYS *` |
| MinIO | `Minio` .NET SDK via `IStorageService` | Store bucket name + object key, not full URL — generate presigned URLs at read time |
| Postal (email) | SMTP or HTTP API via `IEmailSender` | `SendGridEmailService` as fallback; swap without changing handlers |
| NextAuth v5 | `blog-web` and `blog-admin` use separate NextAuth configs; JWT tokens passed to Blog.API | NextAuth session ≠ Blog.API JWT — NextAuth manages UI session, Blog.API validates its own JWTs |
| Meilisearch | Phase 3 replacement for PostgreSQL FTS via `ISearchService` interface | Interface must be defined in Phase 6 (PostgreSQL FTS) to enable swap without handler changes |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| blog-web ↔ Blog.API | HTTPS REST, types from `shared-contracts` | ISR revalidation webhook is a separate internal call from Blog.API back to blog-web |
| blog-admin ↔ Blog.API | HTTPS REST with JWT, types from `shared-contracts` | All mutating calls require `Authorization: Bearer` header |
| Blog.API ↔ Blog.Application | MediatR in-process dispatch | No HTTP boundary — same process |
| Blog.Application ↔ Blog.Domain | Direct method calls on aggregates; interfaces injected | Domain never calls Application — dependency flows inward only |
| Blog.Application ↔ Blog.Infrastructure | Interface abstractions (`IPostRepository`, `IRedisCacheService`) | Application never instantiates Infrastructure types directly |
| IdentityDbContext ↔ BlogDbContext | Shared `NpgsqlConnection` + `DbTransaction` via `IUnitOfWork` | No FK between `AspNetUsers` and `users` tables — logical link only (ADR-006, ADR-007) |
| Domain Events ↔ Cache Invalidation | MediatR `INotificationHandler` in-process | PostPublished → cache invalidation + ISR webhook are in same async handler chain |
| Blog.API ↔ Kubernetes | Container health checks, Prometheus metrics endpoint (`/metrics`) | HPA scales on CPU; consider custom metrics (request queue depth) for Redis-backed workloads |

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0-1k users | Current monolith is correct. No changes needed. SQLite feasible for dev but PostgreSQL is specified. |
| 1k-10k users | Enable Redis cache for all public queries. Add read replica for PostgreSQL. CDN in front of blog-web static assets. |
| 10k-100k users | Horizontal scale Blog.API pods (HPA already configured). Meilisearch migration (Phase 3) reduces PostgreSQL FTS load. Separate MinIO to dedicated nodes. |
| 100k+ users | Consider extracting comment/reaction service as separate process (highest write volume). Saga pattern for cross-service transactions. Meilisearch already in place. |

### Scaling Priorities

1. **First bottleneck:** PostgreSQL reads under concurrent public traffic. Fix: ensure all public queries use `ICacheableQuery` with appropriate TTLs. Redis absorbs read traffic before hitting DB.
2. **Second bottleneck:** PostgreSQL FTS under search volume. Fix: Phase 3 Meilisearch migration (already planned in ADR-009). `ISearchService` abstraction makes swap a drop-in.
3. **Third bottleneck:** Blog.API pod CPU on writes. Fix: HPA already configured. Ensure write handlers are async and non-blocking.

## Anti-Patterns

### Anti-Pattern 1: Domain Layer References Infrastructure

**What people do:** Import `BlogDbContext` or `RedisCache` directly into Domain aggregates or domain services for convenience.
**Why it's wrong:** Destroys testability — cannot unit test domain logic without a database. Violates the Dependency Rule — outer layers may depend on inner layers, never the reverse.
**Do this instead:** Define `IPostRepository` in Domain. Implement `PostRepository : IPostRepository` in Infrastructure. Inject via DI.

### Anti-Pattern 2: Conflating IdentityUser with Domain User

**What people do:** Extend `IdentityUser` with blog-specific properties (bio, role, display name) or use `IdentityUser` directly in Application handlers.
**Why it's wrong:** ASP.NET Identity schema owns the `AspNetUsers` table structure. Blog domain properties in `IdentityUser` create a migration/upgrade dependency on Identity schema changes. Also prevents future auth provider swap.
**Do this instead:** `IdentityUser` handles only authentication credentials. `User` domain aggregate handles profile and business rules. Linked by shared GUID (ADR-006).

### Anti-Pattern 3: Caching All Queries (Opt-Out Instead of Opt-In)

**What people do:** Apply `CachingBehavior` to all queries and exclude sensitive ones with a blocklist.
**Why it's wrong:** Easy to forget to exclude a query. `GetCurrentUser` cached = user sees stale session data. Admin queries cached = stale user list in moderation panel.
**Do this instead:** ICacheableQuery opt-in (ADR-008). `CachingBehavior` skips any query not implementing the interface.

### Anti-Pattern 4: Storing Only pre-rendered HTML, Not ProseMirror JSON

**What people do:** Store only `body_html` (pre-rendered from Tiptap) for simplicity.
**Why it's wrong:** Once you lose the ProseMirror JSON (`body_json`), you cannot re-edit the post in the Tiptap editor — the rich text structure is gone. You also cannot migrate to a different renderer later.
**Do this instead:** Always store both `body_json` (primary, editable) and `body_html` (derived, for SSG performance). `body_html` is regenerated from `body_json` on each update.

### Anti-Pattern 5: `KEYS *` in Redis for Cache Invalidation

**What people do:** Use `KEYS *` or `KEYS post-list:*` to find keys to delete during cache invalidation.
**Why it's wrong:** `KEYS` is a blocking O(N) command. On a Redis instance with many keys, this blocks all other Redis operations for the duration of the scan.
**Do this instead:** Use `SCAN` iterator + batch `DEL`, wrapped in a Lua script for atomicity. Redis 8 supports this pattern (ADR-005).

### Anti-Pattern 6: Hand-Editing Generated Types in shared-contracts

**What people do:** Modify `api.types.ts` (auto-generated from OpenAPI) to add convenience types or fix perceived errors.
**Why it's wrong:** Next run of `gen-types.sh` overwrites all hand edits. Drift between OpenAPI spec and actual types causes silent runtime bugs.
**Do this instead:** Fix the OpenAPI spec in Blog.API if the generated types are wrong. Add extension types in a separate non-generated file in `shared-contracts/src/`.

## Sources

- ADR-001 through ADR-009: `docs/blog-platform/03-architecture-decisions.md` (HIGH confidence — project specification)
- Database schema and ERD: `docs/blog-platform/06-database-schema.md` (HIGH confidence — project specification)
- Folder structure: `docs/blog-platform/02-folder-structure.md` (HIGH confidence — project specification)
- Clean Architecture layer build order: [Milan Jovanovic — Clean Architecture Folder Structure](https://www.milanjovanovic.tech/blog/clean-architecture-folder-structure) (MEDIUM confidence — verified against ADR-002)
- MediatR pipeline behavior registration order: [Code-Maze — CQRS Validation Pipeline](https://code-maze.com/cqrs-mediatr-fluentvalidation/) + [MediatR Pipeline Behavior — codewithmukesh](https://codewithmukesh.com/blog/mediatr-pipeline-behaviour/) (HIGH confidence — matches ADR-005 pipeline order)
- Next.js SSG/ISR for blog architecture: [bitskingdom — When to Use SSR, SSG, or ISR](https://bitskingdom.com/blog/nextjs-when-to-use-ssr-vs-ssg-vs-isr/) (MEDIUM confidence — consistent with ADR-003)
- OpenAPI → TypeScript codegen in monorepos: [hey-api/openapi-ts](https://github.com/hey-api/openapi-ts) (MEDIUM confidence — tool referenced in gen-types.sh approach)

---
*Architecture research for: Production Blog Platform (ASP.NET Core 10 + Next.js 16.1 + Nx monorepo)*
*Researched: 2026-03-12*
