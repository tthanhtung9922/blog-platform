# Project Research Summary

**Project:** Vietnamese-first production blog platform (self-hosted)
**Domain:** Content publishing platform — CMS + public reader + editorial workflow
**Researched:** 2026-03-12
**Confidence:** HIGH

## Executive Summary

This is a production-grade blog platform optimized for Vietnamese content creators, built as a self-hosted monorepo with separate backends for the public reader and the CMS dashboard. The well-established approach for this domain is Clean Architecture + CQRS on the backend (ASP.NET Core 10 + EF Core 10 + PostgreSQL 18), with two distinct Next.js 16.1 applications — one SSG/ISR-optimized public reader and one interactive CMS admin — connected through an OpenAPI-generated shared type contract. The stack is fully specified in the project ADRs and has been verified against current official documentation; all core technologies have reached stable or LTS releases as of March 2026.

The recommended approach is to build in a strict dependency order: monorepo scaffold and Domain layer first (no external dependencies), then Infrastructure and Application pipeline, then Auth (the root dependency for all features), then the Post feature (the primary content unit), and finally social features, admin tooling, and observability. Authentication is the critical path — every subsequent feature depends on a correct, secure auth layer with 3-layer RBAC (API controller + MediatR `AuthorizationBehavior` + CASL frontend). The most significant differentiator of this platform over competitors (Ghost, Medium, Hashnode) is the Vietnamese-first full-text search using PostgreSQL `unaccent` with a custom configuration — a low-effort, high-value feature that no major competitor provides.

The key risks are structural: orphaned `IdentityUser` records if cross-context transactions are not wrapped in a shared `NpgsqlConnection`; stale ISR cache if the `blog-api` does not actively trigger Next.js `revalidatePath` after publish events; stored XSS if `body_html` is ever accepted from the client instead of being generated server-side from `body_json`; and silent API/frontend contract drift if `shared-contracts` type generation is not enforced in CI. All four risks are preventable at the scaffold and auth phases with correct architecture test coverage and the patterns documented in the ADRs.

## Key Findings

### Recommended Stack

The stack is modern, fully LTS-aligned, and internally consistent. ASP.NET Core 10 (LTS until November 2028) pairs naturally with EF Core 10 + Npgsql 10 targeting PostgreSQL 18, which brings virtual generated columns (zero-cost `reading_time` computation) and a built-in `uuidv7()` function. Redis 8 (GA October 2025) ships with JSON and probabilistic structures built-in, removing the need for RedisStack modules, and is 87% faster than Redis 7.x. Next.js 16.1 with Turbopack stable and React 19.2 is the correct choice for both frontends, with the `'use cache'` directive replacing the implicit caching confusion of earlier versions. TypeScript 6.0 reaches GA March 17, 2026 — five days after this research; pin to `6.0.x` once GA ships.

One critical correction from the project's CLAUDE.md: the monorepo plugin is `@nx/dotnet` (official, bundled with Nx 22+), NOT `@nx-dotnet/core` (community plugin, deprecated September 2025). This is a hard requirement — the community plugin is unmaintained and has no path forward.

**Core technologies:**
- ASP.NET Core 10 LTS: REST API backend — 3-year LTS window, Clean Architecture + CQRS fits naturally
- PostgreSQL 18: primary database — virtual generated columns, native `uuidv7()`, up to 3x read I/O improvement
- Redis 8: distributed cache + rate limiting — built-in data structures, 87% faster, required for SCAN-based pattern invalidation
- Next.js 16.1 (x2): public reader (SSG/ISR) + CMS admin (interactive) — Turbopack stable, explicit `'use cache'` directive
- Tiptap v3: rich text editor + read-only renderer — stable since January 2026, SSR-compatible, ProseMirror JSON format
- CASL >= 6.8.0: frontend RBAC — pinned due to CVE-2026-1774; isomorphic with shared `permissions.ts`
- Nx 22 + `@nx/dotnet`: monorepo build system — official .NET plugin (experimental but the only supported path)

### Expected Features

The research identified 11 gaps in the current API specification that must be addressed before a complete MVP can be built. The most critical are: Tags CRUD API (required before authors can categorize posts), image/media upload endpoint (required for in-editor image embedding), password reset flow (required for a complete auth system), and an `is_featured` toggle endpoint (required for homepage curation).

**Must have (table stakes):**
- Full auth flow: register, email verification, login, OAuth (Google/GitHub), password reset, refresh/revoke tokens — auth is the root dependency for all other features
- Post CRUD with draft/publish/archive lifecycle and editorial approval gate (Editor/Admin-only publish)
- Tags CRUD API + assignment + filtering — not yet defined in OpenAPI spec; blocks post creation
- Rich text editing via Tiptap v3 with heading, bold, italic, lists, code blocks, links, images
- Public SSG/ISR blog: post list, post detail, tag/author filtering, featured posts
- SEO: per-page meta tags, OG images, canonical URLs, XML sitemap — frontend-only, but completely absent from spec
- Comment system with 1-level nesting and moderation queue (approve/reject)
- Like + bookmark toggle reactions
- User profiles: public page + self-edit (display name, avatar, bio, social links)
- 3-layer RBAC: 4 roles (Admin/Editor/Author/Reader) enforced at API + MediatR + CASL
- Admin: user list, role assignment, ban
- Vietnamese FTS search (PostgreSQL `unaccent` + custom `vietnamese` config)
- Post versioning (data capture on each save; restore UI deferred to v1.x)
- Client-side autosave in CMS editor (periodic PUT to draft endpoint)
- Password reset email via Postal transactional email

**Should have (competitive differentiators):**
- Vietnamese-first full-text search — no competitor provides this natively; immediate quality advantage
- Self-hosted, no vendor lock-in (MinIO, Postal, future Meilisearch) — meaningful for Vietnamese creators concerned about platform dependence
- Editorial workflow (Editor approval before publish) — distinguishes this from self-publishing platforms
- SSG/ISR public site with sub-second loads — critical for congested Vietnamese mobile networks
- Prometheus + Grafana + Loki observability — differentiates from basic self-hosted blogs

**Defer (v2+):**
- In-editor image upload via drag-drop/paste to MinIO — workaround: external URL input at launch
- My bookmarks list view — toggle works at launch; dedicated list view adds polish post-launch
- Email newsletter + notifications — full product in itself; requires user base to justify
- Author analytics dashboard — requires event pipeline
- Dark mode — Tailwind v4 supports it; design work deferred
- Meilisearch migration (Phase 3) — upgrade PostgreSQL FTS when scale demands it
- Multi-author publications, paid membership, custom domains (Phase 3-4)

### Architecture Approach

The architecture follows Clean Architecture with strict inward-only dependency flow: Domain (no dependencies) → Application (depends on Domain only) → Infrastructure (implements Domain interfaces) → Presentation (wires DI). The backend uses CQRS via MediatR with a four-behavior pipeline in fixed order: `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior`. The two frontend apps share an OpenAPI-generated type contract in `shared-contracts`, preventing silent drift between the API and the frontend. Cache invalidation is event-driven: Domain Events dispatch post-commit to invalidate Redis patterns via atomic Lua scripts, and trigger Next.js ISR revalidation via webhook.

**Major components:**
1. `Blog.Domain` — Aggregates (Post, Comment, User), Value Objects (Slug, Tag, Email, ReadingTime), Domain Events, repository interfaces; zero external dependencies
2. `Blog.Application` — CQRS commands/queries via MediatR, four-behavior pipeline, `ICacheableQuery` opt-in caching interface
3. `Blog.Infrastructure` — EF Core 10 + PostgreSQL 18, Redis 8 cache service, MinIO storage, Postal email, PostgreSQL FTS search (Phase 1)
4. `Blog.API` — REST controllers, JWT middleware, DI wiring; no business logic
5. `blog-web` (Next.js 16.1) — SSG/ISR public reader; Server Components for post content; client-side only for comments and reactions
6. `blog-admin` (Next.js 16.1) — CMS dashboard; Tiptap v3 editor; CASL permission gates; `@tanstack/react-query` for optimistic updates
7. `shared-contracts` — Auto-generated TypeScript types from OpenAPI spec; `roles.ts`; `permissions.ts`; never hand-edited
8. `shared-ui` — Pure presentational React components; no API calls, no auth logic

### Critical Pitfalls

1. **Orphaned IdentityUser on registration failure** — wrap `IdentityService.CreateAsync()` + `IUserRepository.AddAsync()` in a shared `NpgsqlConnection` + `NpgsqlTransaction` via `IUnitOfWork`; write an integration test that forces failure at step 2 and asserts no orphaned `AspNetUsers` row. Severity: CRITICAL. Phase: 1.

2. **MediatR pipeline behavior order breaks authorization** — register all four behaviors in a single, commented extension method; add an architecture test asserting registration order; write an integration test asserting unauthenticated requests get 401, not cached data. Severity: CRITICAL. Phase: 1.

3. **CASL-only frontend authorization (no API enforcement)** — every command handler must have a unit test asserting that insufficient roles receive `ForbiddenException`; `AuthorizationBehavior` must throw on unapproved requests, not be a stub. Severity: CRITICAL. Phase: 1.

4. **ISR two-layer cache staleness after publish** — create a `RevalidationService` in `blog-api` that fires `POST /api/revalidate` to `blog-web` after every `PostPublishedEvent`, `PostUpdatedEvent`, and `PostArchivedEvent`; back this with an E2E Playwright test. Severity: HIGH. Phase: 2.

5. **Stored XSS via `body_html` accepted from client** — `body_html` must only be computed server-side from `body_json` using Tiptap's static renderer, then sanitized with `Ganss.Xss` before storage; DOMPurify on `blog-web` as defense-in-depth. Never accept `body_html` as an input field. Severity: CRITICAL. Phase: 2.

6. **Nx project graph missing `shared-contracts` dependency** — declare `implicitDependencies: ["shared-contracts"]` in `project.json` for both frontends; register `gen-types.sh` as an Nx executor; add a CI gate that runs `gen-types.sh` and asserts `git diff --exit-code`. Severity: HIGH. Phase: 1.

## Implications for Roadmap

Based on the dependency graph established in architecture research, the suggested phase structure is:

### Phase 1: Monorepo Foundation + Domain Layer
**Rationale:** Domain layer has zero external dependencies and is the invariant foundation; architecture tests prevent layer pollution from day one. The Nx project graph and `shared-contracts` type generation pipeline must be correct before any code is written — fixing project graph configuration retroactively is painful and error-prone.
**Delivers:** Nx monorepo scaffold with `@nx/dotnet`; `Blog.Domain` aggregates, value objects, domain events, repository interfaces; PostgreSQL 18 setup with EF Core migrations and Vietnamese `unaccent` extension; Docker Compose for local services; `Blog.ArchTests` enforcing layer boundaries; correct `implicitDependencies` in Nx project graph.
**Addresses:** Post, Comment, User domain models; Slug, Tag, Email, ReadingTime value objects; post versioning data model.
**Avoids:** Pitfall 8 (Nx project graph missing shared-contracts dependency); domain-layer pollution that is expensive to unwind.
**Research flag:** STANDARD — Clean Architecture + EF Core scaffolding patterns are well-documented. Skip `research-phase`.

### Phase 2: Infrastructure + Application Pipeline
**Rationale:** Infrastructure implements Domain interfaces; Application orchestrates use cases. Neither can be built until Domain exists. The MediatR pipeline behavior registration order must be established here — before any handlers are written — to avoid the behavior-ordering pitfall.
**Delivers:** `Blog.Infrastructure` (BlogDbContext, repositories, `IdentityDbContext`, `IUnitOfWork`); `Blog.Application` with all four MediatR pipeline behaviors registered in correct order; Redis `RedisCacheService` with Lua-script pattern invalidation; MinIO `StorageService`; `Blog.IntegrationTests` scaffold with Testcontainers.
**Uses:** EF Core 10 + Npgsql 10, StackExchange.Redis 2.11, Minio SDK, Testcontainers 4.x, xUnit 3.2.
**Implements:** CQRS pipeline, `ICacheableQuery` opt-in caching, `IUnitOfWork` cross-context transaction wrapper.
**Avoids:** Pitfall 3 (MediatR pipeline behavior order); Pitfall 5 (domain events before commit — establish post-commit dispatch pattern here).
**Research flag:** STANDARD — MediatR + EF Core + Testcontainers patterns are well-documented. Skip `research-phase`.

### Phase 3: Authentication + RBAC
**Rationale:** Auth is the root dependency for every subsequent feature. Every future integration test, command handler authorization test, and role-gated feature depends on a working auth layer. The cross-context transaction for user registration (Pitfall 1) must be solved here before any other feature builds on top of it.
**Delivers:** ASP.NET Identity + `JwtTokenService` + `CurrentUserService`; `RegisterCommand`, `LoginCommand`, `RefreshTokenCommand`, `LogoutCommand`, password reset flow; `AuthorizationBehavior` fully wired with all 4 role policies; `Blog.API` auth endpoints; NextAuth v5 configured for both `blog-web` and `blog-admin` with JWT forwarding to `blog-api`; `shared-contracts` initial `roles.ts` + `permissions.ts`; Tags CRUD API (required before post creation is complete).
**Addresses:** Full auth flow (table stakes), RBAC (3-layer), password reset (spec gap), email confirmation resend (spec gap), Tags CRUD API (spec gap — unblocks post creation).
**Avoids:** Pitfall 1 (orphaned IdentityUser), Pitfall 3 (CASL-only authorization), Pitfall 4 (NextAuth JWT vs ASP.NET JWT confusion).
**Research flag:** STANDARD — NextAuth v5 + ASP.NET Identity + JWT patterns are well-documented. The CASL isomorphic permission sharing pattern is documented in official CASL docs. Skip `research-phase`.

### Phase 4: Post Feature (Core Content)
**Rationale:** Posts are the primary domain entity; all other features (comments, reactions, search) reference posts. The ISR two-layer cache staleness pitfall is introduced here and must be solved immediately.
**Delivers:** Full Post aggregate lifecycle (create, update, publish, archive, delete); post versioning snapshot on each save; Tiptap v3 editor in `blog-admin`; SSG/ISR post list and post detail in `blog-web`; `is_featured` admin toggle (spec gap); slug customization (spec gap); `RevalidationService` calling `blog-web`'s `/api/revalidate` webhook; OpenAPI spec for post endpoints → `gen-types.sh` run → `shared-contracts` updated; autosave (client-side debounced PUT to draft endpoint).
**Addresses:** Post CRUD + publish workflow, rich text editor, cover image (URL-based), post versioning data capture, autosave, featured posts toggle, public SSG/ISR reader.
**Avoids:** Pitfall 2 (domain events before commit), Pitfall 5 (ISR two-layer cache staleness), Pitfall 6 (stored XSS in body_html), Anti-Pattern 4 (storing only pre-rendered HTML).
**Research flag:** NEEDS `research-phase` — Tiptap v3 server-side static rendering in .NET has no official solution (the renderer is Node.js only). The integration gotcha in PITFALLS.md identifies two options: a Node.js sidecar process, or rendering HTML in a Next.js server action and forwarding sanitized HTML to the API. This architectural decision needs deeper research before implementation.

### Phase 5: Social Features (Comments, Reactions, User Profiles)
**Rationale:** These features add social interactions on top of existing posts and users. Comment and reaction tables reference posts; they cannot be built until Phase 4. Public author profile pages reference user + post data that now exists.
**Delivers:** Comment aggregate (add, delete, moderate, 1-level nested replies); comment moderation queue in `blog-admin`; `ToggleLike` + `ToggleBookmark` reactions (idempotent); user profile edit + public profile page in `blog-web`; bookmark toggle (list view deferred to v1.x); `My bookmarks` list endpoint (`GET /users/me/bookmarks`, spec gap).
**Addresses:** Comment system + moderation (table stakes), like/bookmark reactions (table stakes), user profiles (table stakes), bookmark list endpoint (spec gap).
**Avoids:** Pending comments not visible to non-moderators; cursor-based pagination for high-volume comment threads (UX pitfall).
**Research flag:** STANDARD — Comment nesting (1-level), reaction toggles with unique constraints, and user profile patterns are well-established. Skip `research-phase`.

### Phase 6: Admin Features + Search
**Rationale:** User management and search are operational features that add value but are not blocking for core content creation. PostgreSQL FTS requires the `unaccent` extension and custom Vietnamese configuration to be verified with integration tests before release — a non-trivial setup that warrants its own phase.
**Delivers:** Admin user list, role assignment, `BanUser` (cross-context, security_stamp invalidation for JWT); PostgreSQL FTS with custom `vietnamese` text search config, GIN index, `SearchPostsQuery`; `ISearchService` interface (abstraction layer enabling Phase 3 Meilisearch swap); image/media upload endpoint (`POST /media/upload` → MinIO); `blog-web` `/search` page; `blog-admin` user management, comment moderation queue, settings.
**Addresses:** Admin user management (table stakes), Vietnamese FTS search (table stakes + differentiator), media upload endpoint (spec gap), `ISearchService` abstraction for future Meilisearch.
**Avoids:** Pitfall 7 (FTS index on HTML content), Vietnamese diacritic normalization bugs, banned user JWT not invalidated, cover image presigned URL expiry.
**Research flag:** NEEDS `research-phase` — Vietnamese PostgreSQL FTS `unaccent` rules file configuration needs verification against all Vietnamese tonal combinations. The PITFALLS research explicitly calls out that the default unaccent dictionary has gaps and recommends community-maintained rules. Validate before CI.

### Phase 7: SEO + Observability + Deployment
**Rationale:** SEO and observability are cross-cutting concerns that finalize production readiness. Building them last avoids reconfiguring Kubernetes manifests as the API surface stabilizes through earlier phases.
**Delivers:** Per-page Next.js metadata API (title, description, og:image, canonical URLs) for all `blog-web` pages; XML sitemap generation (spec gap); RSS feed (differentiator, spec gap); Docker multi-stage builds; Kubernetes base + dev/staging/prod overlays with HPA; GitHub Actions: CI, CD staging, CD prod (manual approval), `gen-types.yml` auto-PR workflow; Prometheus + Grafana + Loki dashboards; Redis-backed distributed rate limiting middleware; TLS via cert-manager.
**Addresses:** SEO (table stakes — absent from spec), XML sitemap (spec gap), RSS feed (differentiator), infrastructure and observability (differentiator).
**Avoids:** Nx affected builds broken by missing project boundaries (reinforced by CI gates added here).
**Research flag:** STANDARD — Next.js metadata API, Next.js sitemap generation, Kubernetes + Kustomize, GitHub Actions patterns are all well-documented. Skip `research-phase`.

### Phase Ordering Rationale

- **Phases 1-2 are pure backend scaffold** — no business logic, no HTTP surface. They must complete before any feature handler can be written.
- **Auth (Phase 3) before any feature** — every feature test, every RBAC check, every integration test depends on an authenticated user. This order prevents having to retroactively add auth to handlers.
- **Posts (Phase 4) before social features** — comments, reactions, and search all reference posts. There is no comment without a post.
- **Search (Phase 6) after content stabilizes** — the PostgreSQL FTS index is over `posts.title` and `posts.excerpt`; those columns need real content to validate Vietnamese diacritic normalization.
- **SEO and deployment (Phase 7) last** — the API surface must be stable before writing SEO metadata for all page types, and Kubernetes manifests should not be revisited every time a new endpoint is added.
- **Anti-features deferred deliberately** — real-time comments, email newsletters, author analytics, scheduled publishing, and multi-tenant organizations are all explicitly excluded from Phase 1 to avoid premature infrastructure complexity.

### Research Flags

Phases needing `research-phase` during planning:
- **Phase 4 (Post Feature):** Tiptap v3 server-side HTML rendering from ProseMirror JSON has no official .NET implementation. The two options (Node.js sidecar vs. server-action HTML pre-rendering in `blog-admin`) each have significant architectural implications. Needs decision before implementation begins.
- **Phase 6 (Search):** Vietnamese `unaccent` rules file completeness. The default PostgreSQL unaccent dictionary has documented gaps for Vietnamese tonal marks. A custom rules file from community sources must be verified with integration tests against real Vietnamese diacritic combinations before the FTS feature is released.

Phases with standard patterns (skip `research-phase`):
- **Phase 1 (Foundation):** Clean Architecture + EF Core + Nx scaffold — extensively documented.
- **Phase 2 (Infrastructure):** MediatR pipeline behaviors + Testcontainers + Redis cache-aside — well-documented.
- **Phase 3 (Auth):** NextAuth v5 + ASP.NET Identity + JWT + CASL — official documentation is comprehensive.
- **Phase 5 (Social):** Comment nesting, reaction toggles, user profiles — standard patterns.
- **Phase 7 (SEO + Deployment):** Next.js metadata API, Kubernetes + Kustomize, GitHub Actions — all well-documented.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies verified against official docs and release notes as of March 2026. TypeScript 6.0 is RC (GA March 17, 2026); `@nx/dotnet` is experimental — both carry minor version-lock risk. |
| Features | HIGH | Core features grounded in project ADRs + OpenAPI spec. Competitor analysis is MEDIUM confidence (community sources). 11 spec gaps identified are validated against the existing OpenAPI specification. |
| Architecture | HIGH | Architecture is fully specified in ADR-001 through ADR-009. Research validated patterns against external sources. The Tiptap .NET rendering gap is a genuine unresolved architectural decision. |
| Pitfalls | HIGH | All critical pitfalls tied to specific documented vulnerabilities (CVE-2025-29927, Tiptap XSS issue #3673) or verified patterns (Redis SCAN vs KEYS, EF Core cross-context transactions). Recovery strategies are actionable. |

**Overall confidence:** HIGH

### Gaps to Address

- **Tiptap v3 server-side rendering in .NET:** No official .NET port of `@tiptap/html` `generateHTML()` exists. The two candidate approaches (Node.js sidecar, server-action pre-rendering) each have different security and operational tradeoffs. This architectural decision must be made and documented as an ADR before Phase 4 begins.
- **Vietnamese `unaccent` rules file completeness:** The default PostgreSQL unaccent dictionary has gaps for Vietnamese diacritics. A community-maintained rules file must be sourced (e.g., `vinh0604/postgres-unaccent-vi`) and validated with integration tests that cover all 6 Vietnamese tonal marks (sắc, huyền, hỏi, ngã, nặng, flat) across all vowels.
- **`@nx/dotnet` experimental status:** The plugin is actively developed but API may change across Nx 22.x minor releases. Monitor Nx 22.x patch notes; do not pin to a specific minor version.
- **TypeScript 6.0 RC timing:** GA is March 17, 2026 — 5 days from research date. If scaffold begins before GA, pin to `typescript@rc` and upgrade to `6.0.x` on GA. Fallback is `5.9.x` which is fully compatible with the rest of the stack.
- **Meilisearch Vietnamese tokenization (Phase 3 only):** Meilisearch uses whitespace-separator pipeline for Vietnamese (no diacritic normalization). This is acceptable as a Phase 3 upgrade from PostgreSQL FTS, but search quality against Vietnamese diacritics must be explicitly tested before committing to the migration.
- **11 API specification gaps:** Tags CRUD, `is_featured` toggle, image upload endpoint, password reset flow, email confirmation resend, my bookmarks list, post versions list, admin tags endpoint, XML sitemap, slug override field, RSS feed — all must be added to the OpenAPI spec before or during the phase that implements them.

## Sources

### Primary (HIGH confidence)
- `docs/blog-platform/03-architecture-decisions.md` (ADR-001 through ADR-009) — all architecture decisions, RBAC matrix, caching strategy, search strategy, cross-context transaction strategy
- `docs/blog-platform/06-database-schema.md` — full ERD, table definitions, FTS index design
- `docs/blog-platform/09-api-contract--openapi-specification.md` — OpenAPI 3.1 specs for all endpoints (source of gap analysis)
- [ASP.NET Core 10 release notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview) — LTS status, new features
- [PostgreSQL 18 release announcement](https://www.postgresql.org/about/news/postgresql-18-released-3142/) — virtual generated columns, uuidv7(), I/O improvements
- [Redis 8 GA blog](https://redis.io/blog/redis-8-ga/) — built-in data structures, performance improvements
- [Next.js 16 + 16.1 release blogs](https://nextjs.org/blog/next-16) — Turbopack stable, `'use cache'` directive
- [Tiptap 3.0 stable release notes](https://tiptap.dev/blog/release-notes/tiptap-3-0-is-stable) — breaking changes, SSR support
- [TypeScript 6.0 beta announcement](https://devblogs.microsoft.com/typescript/announcing-typescript-6-0-beta/) — GA date, breaking changes
- [CVE-2025-29927: Next.js middleware bypass](https://projectdiscovery.io/blog/nextjs-middleware-authorization-bypass) — frontend auth cannot be sole guard
- [Tiptap XSS in link extension — GitHub Issue #3673](https://github.com/ueberdosis/tiptap/issues/3673) — `javascript:` URI vulnerability

### Secondary (MEDIUM confidence)
- [Milan Jovanovic — Clean Architecture Folder Structure](https://www.milanjovanovic.tech/blog/clean-architecture-folder-structure) — build order patterns
- [MediatR pipeline behavior ordering — codewithmukesh.com](https://codewithmukesh.com/blog/mediatr-pipeline-behaviour/) — registration order = execution order
- [Next.js ISR on-demand revalidation docs](https://nextjs.org/docs/app/guides/incremental-static-regeneration) — revalidateTag, revalidatePath
- [Vietnamese FTS PostgreSQL — blog.tuando.me](https://blog.tuando.me/vietnamese-full-text-search-on-postgresql) — custom configuration, unaccent rules
- [Nx 22 release blog](https://nx.dev/blog/nx-22-release) — `@nx/dotnet` official plugin introduction
- [Migrate from @nx-dotnet/core to @nx/dotnet](https://nx.dev/docs/technologies/dotnet/guides/migrate-from-nx-dotnet-core) — deprecation confirmation
- [Ghost alternatives 2025](https://hyvor.com/blog/ghost-alternatives) — competitor feature analysis

### Tertiary (LOW confidence)
- [Vietnam digital platform opportunities](https://mmcommunications.vn/en/make-money-online-2025-vietnam-digital-platform-opportunities-n464) — Vietnamese creator market context (single source)

---
*Research completed: 2026-03-12*
*Ready for roadmap: yes*
