# Stack Research

**Domain:** Production blog platform (Vietnamese-first content, self-hosted)
**Researched:** 2026-03-12
**Confidence:** HIGH — all core technologies verified against official docs and release notes

---

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| ASP.NET Core | 10.0 (LTS) | REST API backend | Released November 2025. LTS support until November 2028. The LTS cadence gives 3 years of security patches without forced upgrades. Clean Architecture + CQRS pattern fits naturally with minimal API style or controller style. |
| EF Core + Npgsql | 10.0.x | ORM + PostgreSQL driver | Npgsql.EntityFrameworkCore.PostgreSQL 10.x targets EF Core 10. Explicitly supports PostgreSQL 18 features: virtual generated columns, uuidv7() generation, and SetPostgresVersion(18, 0) for feature flags. Use `Npgsql.EntityFrameworkCore.PostgreSQL` (not the legacy `Npgsql` driver directly). |
| PostgreSQL | 18.x (current: 18.2) | Primary database | Released September 2025. Virtual generated columns (no storage cost) ideal for computed fields like `reading_time`. New I/O subsystem gives up to 3x read performance improvement. uuidv7() is now a built-in function — prefer over application-side generation. |
| Redis | 8.0.x | Distributed cache + rate-limit store | GA since October 2025. Ships with JSON, TimeSeries, and probabilistic structures (Bloom filter) built-in — no longer requires RedisStack modules. Up to 87% faster commands vs Redis 7.x. `SCAN` + `DEL` pattern for cache invalidation is supported; do not use `KEYS *` in production. |
| Next.js | 16.1 | Public blog (blog-web) + Admin CMS (blog-admin) | Released October 2025. Turbopack stable as default bundler (50%+ faster builds). React 19.2 and React Compiler stable. New `'use cache'` directive makes caching opt-in and explicit — replaces the implicit caching that caused confusion in Next.js 14/15. App Router is mature. |
| TypeScript | 6.0 (GA: March 17, 2026) | Type safety for all frontend code | Beta released February 2026; RC released March 6, 2026; GA set for March 17, 2026 — five days from the time of this research. TypeScript 6.0 is the last JS-based compiler release before TypeScript 7.0 (Go rewrite). No disruptive breaking changes for new projects. Use `"strict": true`. |
| Tailwind CSS | v4.x | Utility-first CSS | CSS-first configuration via `@theme` directive in `globals.css`. No `tailwind.config.ts` — the file was intentionally removed. Full builds 5x faster, incremental builds 100x faster via the Oxide engine. Production-ready with v4.1 patch releases stabilizing edge cases. Minimum browser: Safari 16.4+, Chrome 111+, Firefox 128+. |
| shadcn/ui | latest (2025 release) | Accessible component primitives | Fully compatible with Tailwind v4 and React 19. All components updated for `@theme` directive and `data-slot` attributes. Initialize via CLI (`npx shadcn@latest init`) which auto-detects Tailwind v4. |
| Tiptap | v3.x (stable since Jan 2026) | Rich text editor (blog-admin) + read-only renderer (blog-web) | v3 stable. Ships `TableKit` (unified extension package), Floating UI replacing tippy.js, and improved SSR support (editor can render without a DOM for read-only view). Breaking change: `history` option renamed to `undoRedo`. |
| NextAuth (Auth.js) | v5.x (beta, production-stable) | Authentication for both Next.js apps | The "beta" label reflects API stabilization work, not production risk. v5 is actively maintained; v4 is on security-patches only. Works natively with Next.js 16 App Router and Route Handlers. |
| CASL | >= 6.8.0 | Frontend authorization (RBAC) | 6.8.0 is pinned due to CVE-2026-1774 security fix. CASL is isomorphic — the same permission definitions in `shared-contracts/permissions.ts` are shared between `blog-web` and `blog-admin`. |
| ASP.NET Identity | (bundled with .NET 10) | Backend authentication store | Provides password hashing, email confirmation, lockout, OAuth provider mapping. Kept intentionally separate from the Domain `User` aggregate — see ADR-006. Do not conflate `IdentityUser` with `User`. |
| MediatR | 12.5.x | CQRS dispatcher (backend) | MediatR 12.5.0 required by `MediatR.Extensions.Microsoft.AspNetCore 7.0.0`. Pipeline behaviors apply in order: ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior. |
| FluentValidation | 12.1.x | Request validation (backend) | Latest stable: 12.1.1 (December 2025). Requires .NET 8+, fully compatible with .NET 10. No external dependencies. Use with `FluentValidation.DependencyInjectionExtensions` for automatic validator registration. |
| MinIO | latest stable | Object storage (images, media) | Self-hosted S3-compatible storage. No vendor lock-in. Use the `Minio` .NET SDK (`Minio` on NuGet). |

---

### Supporting Libraries — Backend (.NET)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `StackExchange.Redis` | 2.11.x | Redis client for .NET | Primary Redis client. Use for `RedisCacheService` and rate-limiting middleware. Supports Redis 8.4 features. Use `ConnectionMultiplexer.Connect()` with retry policy. |
| `Npgsql` | 9.0.x | Low-level PostgreSQL ADO driver | Pulled transitively by EF Core Npgsql provider. Use directly only for shared `DbConnection` in cross-context transactions (ADR-007). |
| `NetArchTest.eNhancedEdition` | 1.4.x | Architecture rule enforcement | Use in `Blog.ArchTests` to verify Clean Architecture layer boundaries (Domain must not reference Infrastructure, etc.). Prefer the eNhancedEdition fork over the original `NetArchTest.Rules` — it is actively maintained and supports .NET 10. |
| `Testcontainers` | 4.x | Disposable Docker containers for integration tests | Use in `Blog.IntegrationTests`. Provides `PostgreSqlContainer`, `RedisContainer`. Pairs with `Testcontainers.Xunit` for xUnit v3 lifecycle integration. |
| xUnit | 3.2.x | Unit and integration test runner | xUnit v3 has first-class parallel test execution and native `IClassFixture` / `ICollectionFixture` improvements. Use `Testcontainers.XunitV3` package (not the v2 compatibility shim). |
| `Microsoft.AspNetCore.OpenApi` | 10.0.x | OpenAPI 3.1 document generation | Built into ASP.NET Core 10; no need for Swashbuckle for basic use. Generates the OpenAPI spec consumed by `gen-types.sh`. |
| Serilog | 4.x | Structured logging | Use with `Serilog.Sinks.Console` (local dev) and `Serilog.Sinks.Grafana.Loki` (production). Integrate via `UseSerilog()` in `Program.cs`. |
| Postal | latest stable | Transactional email (primary) | Self-hosted SMTP relay. Active maintenance confirmed February 2026. Use via `IEmailSender` abstraction so SendGrid can be swapped in without changing handlers. |

---

### Supporting Libraries — Frontend (Next.js / TypeScript)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `@tiptap/react` | 3.x | Tiptap React bindings | Use `useEditor({ editable: false, content: jsonContent })` + `<EditorContent>` in `blog-web` for read-only rendering of ProseMirror JSON. Use full editor in `blog-admin`. |
| `dompurify` | 3.x | HTML sanitization | Use only if rendering Tiptap's HTML output via `dangerouslySetInnerHTML`. Prefer JSON rendering (Option B in ADR) to avoid needing this entirely. |
| `@casl/react` | 4.x | React bindings for CASL | Provides `<Can>` component and `useAbility` hook for `PermissionGate.tsx`. Must match `@casl/ability` version. |
| `swr` or `@tanstack/react-query` | latest | Client-side data fetching | Use for comment reactions and other client-side mutations in `blog-web`. For `blog-admin` where data management is heavier, prefer `@tanstack/react-query`. |
| `openapi-typescript` | 7.x | TypeScript type generation from OpenAPI | Used by `gen-types.sh` to produce `libs/shared-contracts/src/api.types.ts` from the backend's OpenAPI 3.1 spec. Run via `npx openapi-typescript`. |
| `next-themes` | latest | Dark/light mode toggle | Works with Tailwind v4 CSS variables. Wraps app in `ThemeProvider` and writes `class="dark"` or `class="light"` to `<html>`. |
| `zod` | 3.x | Runtime schema validation | Use for form validation in `blog-admin` (paired with `react-hook-form`). Generates TypeScript types that align with OpenAPI-generated types. |
| `react-hook-form` | 7.x | Form state management | Use in `blog-admin` for post creation, comment moderation, user management forms. Integrates with `zod` via `@hookform/resolvers`. |

---

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Nx | 22.x | Monorepo build system, affected builds, task caching | Use `@nx/dotnet` (official plugin, available since Nx 22) — NOT `@nx-dotnet/core` (community plugin, deprecated). See critical note below. |
| `@nx/dotnet` | bundled with Nx 22+ | .NET project graph, build/test targets in Nx | Currently marked experimental in Nx 22 but actively developed with fixes through 22.4.0. Provides intelligent caching for .NET builds and accurate cross-project dependency tracking. |
| `@nx/next` | bundled with Nx 22+ | Next.js targets in Nx | Provides `build`, `serve`, `lint` targets for Next.js apps. Works with Turbopack. |
| Docker + Docker Compose | latest stable | Local development services (Postgres, Redis, MinIO) | `docker-compose.yml` at repo root for dev. Use named `docker-compose.emergency-only.yml` for emergency local-staging fallback — never for production. |
| Kubernetes + Kustomize | 1.32.x | Production deployment | Base manifests + overlays (dev/staging/prod). Redis must use `StatefulSet` (not `Deployment`) with PVC for persistence. |
| GitHub Actions | — | CI/CD pipelines | `ci.yml` (PR checks), `cd-staging.yml` (develop branch), `cd-prod.yml` (main with manual approval), `gen-types.yml` (auto-PR for type updates). |
| cert-manager | latest | TLS certificates via Let's Encrypt | Kubernetes cert-manager for automatic TLS. Annotate Ingress resources with `cert-manager.io/cluster-issuer`. |

---

## Installation

```bash
# Frontend (root package.json)
npm install next@16.1 react@19 react-dom@19 typescript@6
npm install @tiptap/react @tiptap/pm @tiptap/starter-kit
npm install next-auth @casl/ability @casl/react
npm install @tanstack/react-query swr zod react-hook-form @hookform/resolvers
npm install next-themes dompurify
npm install -D openapi-typescript tailwindcss@4 @tailwindcss/postcss

# Initialize shadcn/ui (interactive CLI)
npx shadcn@latest init

# Backend (.NET — add to Blog.Infrastructure.csproj)
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.*
dotnet add package StackExchange.Redis --version 2.11.*
dotnet add package MediatR --version 12.5.*
dotnet add package FluentValidation --version 12.1.*
dotnet add package FluentValidation.DependencyInjectionExtensions --version 12.1.*
dotnet add package Minio --version 6.*
dotnet add package Serilog.AspNetCore --version 9.*
dotnet add package Serilog.Sinks.Grafana.Loki

# Testing
dotnet add package xunit --version 3.2.*
dotnet add package Testcontainers --version 4.*
dotnet add package Testcontainers.PostgreSql
dotnet add package Testcontainers.Redis
dotnet add package NetArchTest.eNhancedEdition --version 1.4.*

# Nx workspace
npm install -D nx@22 @nx/next @nx/dotnet
```

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| ASP.NET Core 10 | Go (Fiber/Echo) | Team skill alignment; .NET has richer ecosystem for Clean Architecture + CQRS; ASP.NET Identity integration is first-class |
| ASP.NET Core 10 | Node.js (Express/Fastify) | Type safety and performance of .NET preferred for API layer; avoid JS on both layers to leverage .NET ecosystem strengths |
| PostgreSQL 18 | MySQL 9 | PostgreSQL FTS with Vietnamese `unaccent` config is well-documented; PostgreSQL JSON support superior; MySQL FTS requires additional licensing considerations |
| Redis 8 | Memcached | Redis 8 ships with built-in data structures (JSON, probabilistic); needed for sorted sets (rate limiting), pub/sub patterns, and SCAN-based pattern invalidation |
| Meilisearch (Phase 3) | Elasticsearch | Elasticsearch switched to SSPL license (not true open source). OpenSearch (Apache 2.0) is an alternative, but Meilisearch is simpler, lighter, and designed for search-as-you-type use cases that match a blog's UX needs |
| Meilisearch (Phase 3) | OpenSearch | Both are viable; Meilisearch wins on operational simplicity and resource usage for a single-blog use case |
| `@nx/dotnet` | `@nx-dotnet/core` | `@nx-dotnet/core` is deprecated (last published September 2025); official `@nx/dotnet` is the migration target per Nx's own documentation |
| FluentValidation 12 | DataAnnotations | FluentValidation provides pipeline-level validation via MediatR behavior; DataAnnotations are controller-level and miss application layer concerns |
| CASL | Permission.js / custom | CASL is isomorphic (same code runs on FE and BE concepts); has React bindings; well maintained |
| xUnit 3.2 | NUnit / MSTest | xUnit v3 has superior parallel execution and integrates cleanly with `Testcontainers.XunitV3`. NUnit and MSTest are viable but have more boilerplate for fixture-based patterns. |
| NextAuth v5 | Clerk / Auth0 | Self-hosted constraint eliminates SaaS auth providers. NextAuth v5 is the only production-grade self-hostable option for Next.js with OAuth + JWT + session handling. |
| Postal | MailHog (dev) / self-hosted Stalwart | Postal is a full delivery platform with bounce tracking, routing, and a web UI. MailHog is dev-only (no real delivery). Stalwart is an option for Phase 3 if Postal's Ruby stack becomes a concern. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `@nx-dotnet/core` | Community plugin deprecated as of September 2025. Last published 3.0.2. Nx 22 ships the official `@nx/dotnet`. Migration guide exists at `nx.dev/docs/technologies/dotnet/guides/migrate-from-nx-dotnet-core`. | `@nx/dotnet` (official, bundled with Nx 22+) |
| `KEYS *` in Redis | Blocking O(N) command that locks Redis in production. Pattern-based cache invalidation must use `SCAN` cursor + `DEL` in batches, ideally wrapped in a Lua script for atomicity. | `SCAN` + `DEL` via `IRedisCacheService.RemoveByPatternAsync()` |
| `IdentityUser` in Domain layer | `IdentityUser` is an Infrastructure concern (ASP.NET Identity). Importing it into `Blog.Domain` creates a hard dependency on the Identity framework, preventing auth provider swaps. | Separate `User` aggregate in Domain; link only via shared GUID. See ADR-006. |
| Elasticsearch | SSPL license (not OSI-approved open source). Cannot be self-hosted in a "true" open source sense for SaaS use. | Meilisearch (MIT) for Phase 3 search |
| Swashbuckle for OpenAPI | ASP.NET Core 10 ships built-in OpenAPI 3.1 document generation via `Microsoft.AspNetCore.OpenApi`. Swashbuckle adds dependency overhead and lags behind ASP.NET Core versions. | `Microsoft.AspNetCore.OpenApi` (built-in) |
| `tailwind.config.ts` | Removed in Tailwind v4. CSS-first config uses `@theme {}` directive in `globals.css`. Generating or committing this file will break Tailwind v4 builds. | `globals.css` with `@theme {}` and `@import "tailwindcss"` |
| MDX for post content | The content format is ProseMirror JSON (Tiptap v3), not Markdown/MDX. MDX requires a build-time compiler step and is incompatible with runtime-authored content stored in the database. | `@tiptap/react` `<EditorContent>` with `editable: false` for read-only rendering |
| `MediatR.Extensions.Microsoft.DependencyInjection` (old package) | This package was merged into MediatR 12+. Referencing it separately causes duplicate registration errors. | Use MediatR 12.5.x which has DI support built in via `services.AddMediatR()` |
| `dotnet ef database update` directly in production | High risk. Production database changes must go through the migration runbook (`docs/blog-platform/11-data-migration-runbook.md`): generate script → review → apply in maintenance window. | `dotnet ef migrations script` to generate SQL, then apply via DBA process |

---

## Version-Specific Gotchas

### TypeScript 6.0 is in RC (not yet GA at time of research)

GA is set for March 17, 2026 — five days after this research was written. Using the RC (`typescript@rc`) is low risk for a greenfield project since no production code exists yet. Pin to `6.0.x` in `package.json` once GA ships. If there are delays, `5.9.x` is fully compatible with all other stack choices.

### `@nx/dotnet` is Experimental in Nx 22

Nx officially labels `@nx/dotnet` as experimental, meaning its API may change across 22.x minor releases. The plugin is actively developed (22.4.0 fixed multi-targeting dependency graph issues). Accept experimental status for now; it is the only supported path for .NET in Nx. Do not pin to a specific 22.x version — follow Nx minor releases to get fixes.

### NextAuth v5 "Beta" Label

The "beta" label in NextAuth v5 is misleading. The library is production-stable and is the only maintained version (v4 receives security patches only). The beta label reflects that the Auth.js team is still refining edge-case APIs for non-Next.js adapters. For Next.js 16 specifically, v5 is the correct choice. Do not downgrade to v4.

### Meilisearch and Vietnamese (Phase 3 Warning)

Meilisearch does not have a dedicated Vietnamese tokenization pipeline as of March 2026. It uses the whitespace-separator pipeline for Vietnamese, which works acceptably for Vietnamese (since Vietnamese uses spaces between syllables), but lacks diacritic normalization and stopword filtering. This is acceptable for Phase 3 given the Phase 1 PostgreSQL FTS baseline. For Phase 3 migration, test Meilisearch search quality against Vietnamese diacritics explicitly before committing.

### Tiptap v3 Breaking Changes

Coming from v2: `history` extension option renamed to `undoRedo`. CSS class prefix for collaboration cursors changed from `.collaboration-cursor` to `.collaboration-carets`. The `TableKit` replaces individual table extension imports. No action needed for a greenfield project — just use v3 API from the start.

### Redis 8 Lua Script Atomicity

Pattern-based cache invalidation (e.g., `post-list:*`) must be atomic. Redis `SCAN` + multi-key `DEL` is not atomic across two commands. Wrap in a Lua script loaded via `SCRIPT LOAD` and called via `EVALSHA`. This is documented in ADR-005 and must be implemented in `RedisCacheService.RemoveByPatternAsync()`.

### PostgreSQL 18 `unaccent` Extension

The `unaccent` extension must be explicitly created in the migration (`CREATE EXTENSION IF NOT EXISTS unaccent`). It is not enabled by default even on managed PostgreSQL services. The custom Vietnamese FTS configuration depends on it. Ensure the PostgreSQL user running migrations has `CREATE EXTENSION` privileges (superuser or `pg_extension_owner` role).

### EF Core 10 Virtual Generated Columns (PostgreSQL 18 only)

If `SetPostgresVersion(18, 0)` is configured in `DbContextOptionsBuilder`, omitting the `stored` parameter in EF fluent API generates a virtual generated column (PostgreSQL 18 feature: computed at read time, zero storage). If targeting an older PostgreSQL version for testing environments, explicitly set `stored: true` or be prepared for migration failures on PostgreSQL < 18.

---

## Version Compatibility Matrix

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| `Npgsql.EFCore.PostgreSQL 10.x` | EF Core 10.x, PostgreSQL 18.x, Npgsql 9.x | Explicitly targets PG 18 via `SetPostgresVersion(18,0)` |
| `StackExchange.Redis 2.11.x` | Redis 8.x, .NET 8+ | Supports Redis 8.4 CAS/CAD operations |
| `MediatR 12.5.x` | .NET 8+, .NET 10 | DI extensions merged in; no separate DI package needed |
| `FluentValidation 12.1.x` | .NET 8+, .NET 10 | Dropped netstandard support; .NET 8+ only |
| `xUnit 3.2.x` | .NET 8+, .NET 10 | Use `Testcontainers.XunitV3` package (not v2 compat shim) |
| `Testcontainers 4.x` | .NET 8+, Docker Engine 24+ | Requires Docker Desktop or rootless Docker on CI |
| `Next.js 16.1` | React 19.2, Node.js 22+ LTS | Turbopack stable; requires Node 22 LTS (24 LTS for Docker images) |
| `TypeScript 6.0` | Next.js 16.x, React 19.x | GA March 17, 2026; use RC or wait for GA |
| `Tailwind CSS v4.x` | Next.js 16.x, PostCSS 8.x | Requires `@tailwindcss/postcss`; drops `tailwindcss` PostCSS plugin |
| `shadcn/ui (2025)` | Tailwind v4, React 19, Next.js 15/16 | Uses `data-slot` attributes; CSS variable tokens via `@theme` |
| `Tiptap v3.x` | React 18/19, Next.js 16 (SSR-compatible) | Floating UI replaces tippy.js; `TableKit` replaces individual table extensions |
| `CASL 6.8.x` | React 18/19, TypeScript 5.x/6.x | `@casl/react` must match `@casl/ability` major version |
| `@nx/dotnet` (Nx 22+) | .NET 10, Nx 22.x | Experimental; follow Nx 22.x patch releases |
| `NetArchTest.eNhancedEdition 1.4.x` | .NET 8+, .NET 10, xUnit v2/v3 | Use this fork; original `NetArchTest.Rules` is unmaintained |

---

## Stack Patterns by Context

**For blog-web (public reader) — performance-first:**
- Use `generateStaticParams` for all post/tag/author pages → SSG at build time
- Use `revalidate` at the page level (ISR) for post list pages (e.g., `revalidate = 300`)
- Use `'use cache'` directive for components that call the API
- Never use client components for post content rendering — keep Server Components for SEO

**For blog-admin (CMS dashboard) — interactivity-first:**
- Use client components for Tiptap editor, form fields, and permission-gated UI
- Use `@tanstack/react-query` for optimistic updates on publish/archive operations
- Gate all dashboard routes via NextAuth `auth()` middleware check in `layout.tsx`

**For cross-context database transactions (Register, Ban):**
- Use shared `NpgsqlConnection` + `BeginTransactionAsync()` — one transaction spanning both DbContexts
- Wrap in `IUnitOfWork` abstraction
- Do not use `TransactionScope` — it has ambient transaction issues with async/await in .NET

**For cache invalidation:**
- Domain Events dispatch after `SaveChanges()` via `INotificationHandler<TEvent>`
- Each handler calls `IRedisCacheService.RemoveByPatternAsync(pattern)` with wildcard
- Wildcard deletion must use Lua script (atomic SCAN + DEL)
- Never invalidate synchronously inside the command handler itself — use domain events to decouple

---

## Sources

- [What's new in .NET 10 — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview) — ASP.NET Core 10 features, LTS status
- [Npgsql EF Core 10 Release Notes](https://www.npgsql.org/efcore/release-notes/10.0.html) — EF Core 10 / PostgreSQL 18 compatibility
- [PostgreSQL 18 Released](https://www.postgresql.org/about/news/postgresql-18-released-3142/) — PostgreSQL 18 feature list, release date (September 2025)
- [Redis 8 is now GA](https://redis.io/blog/redis-8-ga/) — Redis 8 October 2025 GA, built-in data structures
- [Next.js 16 Release Blog](https://nextjs.org/blog/next-16) — Turbopack stable, React 19.2, cache components
- [Next.js 16.1 Release Blog](https://nextjs.org/blog/next-16-1) — Experimental Bundle Analyzer
- [Tailwind CSS v4.0 Blog](https://tailwindcss.com/blog/tailwindcss-v4) — CSS-first config, breaking changes
- [shadcn/ui Tailwind v4 Docs](https://ui.shadcn.com/docs/tailwind-v4) — Tailwind v4 + shadcn/ui compatibility
- [Tiptap 3.0 Stable Release Notes](https://tiptap.dev/blog/release-notes/tiptap-3-0-is-stable) — Breaking changes, new features
- [Announcing TypeScript 6.0 Beta](https://devblogs.microsoft.com/typescript/announcing-typescript-6-0-beta/) — Beta February 2026, GA March 17, 2026
- [TypeScript 6.0 RC — InfoWorld](https://www.infoworld.com/article/4143186/typescript-6-0-reaches-release-candidate-stage.html) — RC stage March 6, 2026
- [MediatR 12.5.0 on NuGet](https://www.nuget.org/packages/MediatR/12.5.0) — MEDIUM confidence (.NET 10 compatibility via transitive support)
- [FluentValidation 12 Upgrade Guide](https://docs.fluentvalidation.net/en/latest/upgrading-to-12.html) — .NET 8+ requirement, breaking changes
- [Nx 22 Release Blog](https://nx.dev/blog/nx-22-release) — Official `@nx/dotnet` plugin introduction
- [Migrate from @nx-dotnet/core to @nx/dotnet](https://nx.dev/docs/technologies/dotnet/guides/migrate-from-nx-dotnet-core) — Deprecation of community plugin
- [Playwright 1.58 Release](https://playwright.dev/docs/release-notes) — Released January 30, 2026
- [CASL @casl/ability on npm](https://www.npmjs.com/package/@casl/ability) — 6.8.0 current (January 2026)
- [Meilisearch Language Docs](https://www.meilisearch.com/docs/learn/resources/language) — Vietnamese tokenization gap confirmed (MEDIUM confidence: documented absence)
- [Postal GitHub](https://github.com/postalserver/postal) — Active maintenance as of February 2026

---

*Stack research for: Vietnamese-first production blog platform*
*Researched: 2026-03-12*
