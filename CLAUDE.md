# Blog Platform — Claude Code Rules

## Project Status

Planning & specification phase — documentation only, no application code yet. All docs define the architecture, schema, and API contracts for the platform.

## What This Project Is

A Vietnamese blog platform built as an Nx monorepo: ASP.NET Core 10 backend (Clean Architecture + DDD + CQRS), two Next.js 16.1 frontends (public reader + CMS admin), PostgreSQL 18, Redis 8 cache, MinIO storage.

## Architecture Rules

- **Layer dependency direction is Domain → Application → Infrastructure → Presentation.** Inner layers never reference outer layers. Domain has zero dependency on Infrastructure or any framework.
- **IdentityUser and User (Domain aggregate) are separate models in separate tables**, linked only by a shared GUID. No inheritance, no navigation properties between them. IdentityUser handles authentication; User handles business logic. (ADR-006)
- **Cross-context operations (Register, Ban) use a shared DbConnection** wrapped in IUnitOfWork — not separate transactions. Both IdentityDbContext and BlogDbContext share the same connection and transaction. (ADR-007)
- **MediatR pipeline behavior order is fixed and immutable:** `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior`
- **Caching is opt-in only.** A query is cached only if it implements `ICacheableQuery` with explicit CacheKey and CacheDuration. Never cache implicitly. (ADR-008)
- **RBAC is enforced at all three layers:** API controllers (ASP.NET Authorization Policies), MediatR AuthorizationBehavior, and frontend CASL. All three must stay in sync via `shared-contracts/permissions.ts`. (ADR-004)
- **Two separate Next.js apps** — `blog-web` (public SSG/ISR reader) and `blog-admin` (CMS dashboard). They serve different purposes and are deployed independently.
- **Content is Tiptap v3 ProseMirror JSON**, not Markdown or MDX. Render via `@tiptap/react` EditorContent in read-only mode. Sanitize any HTML output with DOMPurify.
- **Tailwind CSS v4** uses CSS-first configuration in `globals.css`. No `tailwind.config.ts`.
- **Never hardcode secrets**, connection strings, or tokens in code or config files checked into git.

## Domain Language

| Term | Meaning |
|------|---------|
| Post | Aggregate Root — blog article (Draft → Published → Archived) |
| Comment | Aggregate Root — user comment with nested replies via parent_id |
| User | Domain aggregate for business logic — NOT IdentityUser |
| IdentityUser | ASP.NET Identity model for authentication only (Infrastructure layer) |
| Slug | Value Object — immutable URL-friendly identifier |
| ICacheableQuery | Opt-in interface for query caching with CacheKey + CacheDuration |
| Roles | Admin > Editor > Author > Reader (strict hierarchy) |

## Repository Layout

```
blog-platform/
├── apps/
│   ├── blog-api/               # ASP.NET Core 10 (Clean Arch: Domain → Application → Infrastructure → API)
│   ├── blog-web/               # Next.js 16.1 public reader (SSG/ISR)
│   └── blog-admin/             # Next.js 16.1 CMS dashboard
├── libs/
│   ├── shared-contracts/       # OpenAPI-generated TypeScript types + permission definitions
│   └── shared-ui/              # Shared React component library
├── deploy/
│   ├── docker/                 # Multi-stage Dockerfiles
│   └── k8s/                    # Kustomize base + overlays (dev/staging/prod)
├── tests/
│   ├── Blog.UnitTests/         # Domain + Application unit tests
│   ├── Blog.IntegrationTests/  # Testcontainers-based
│   ├── Blog.ArchTests/         # NetArchTest layer enforcement
│   └── load/                   # k6 load test scenarios
└── scripts/
    ├── gen-types.sh             # OpenAPI → TypeScript codegen
    └── migration.sh             # EF Core migration helper
```

## Quick Reference: What Goes Where

| Concept | Location |
|---------|----------|
| Aggregates, Value Objects, Domain Events | `Blog.Domain/Aggregates/`, `ValueObjects/`, `DomainEvents/` |
| Repository interfaces | `Blog.Domain/Repositories/` |
| Commands & Queries (CQRS) | `Blog.Application/Features/<Aggregate>/Commands/` or `Queries/` |
| FluentValidation validators | Same folder as their Command (`<Command>Validator.cs`) |
| MediatR pipeline behaviors | `Blog.Application/Behaviors/` |
| Repository implementations | `Blog.Infrastructure/Persistence/Repositories/` |
| EF Core configurations | `Blog.Infrastructure/Persistence/Configurations/` |
| Cache key definitions | `Blog.Infrastructure/Caching/CacheKeys.cs` |
| Authorization policies | `Blog.Infrastructure/Authorization/Policies/` |
| REST controllers | `Blog.API/Controllers/` |
| CASL permission definitions | `apps/blog-admin/src/lib/permissions/ability.ts` |
| Shared TypeScript types | `libs/shared-contracts/src/` |

## Commit Convention

Conventional Commits: `<type>[optional scope]: <description>`

Types: `feat`, `fix`, `refactor`, `chore`, `perf`, `ci`, `ops`, `build`, `docs`, `style`, `revert`, `test`

Branches: `feat/<name>`, `fix/<name>`, `chore/<name>`, `docs/<name>` — PRs target `dev`, then `dev` → `main`.

## Scoped Rules

> See `.claude/rules/backend-architecture.md` for Clean Arch + DDD + CQRS details.
> See `.claude/rules/caching.md` for Redis cache-aside, opt-in, invalidation rules.
> See `.claude/rules/security-auth.md` for RBAC 3-layer, roles, JWT, CASL rules.
> See `.claude/rules/database.md` for schema conventions and migration workflow.
> See `.claude/rules/frontend.md` for Next.js, Tailwind v4, Tiptap, component rules.
> See `.claude/rules/api-design.md` for REST conventions, OpenAPI, error format.
> See `.claude/rules/git-workflow.md` for commits, branching, PR process.
> See `.claude/rules/testing.md` for xUnit, Testcontainers, Playwright, ArchTest rules.

## Key Documentation

| File | Purpose |
|------|---------|
| `docs/blog-platform/03-architecture-decisions.md` | All 9 ADRs with rationale |
| `docs/blog-platform/06-database-schema.md` | Full ERD and table definitions |
| `docs/blog-platform/09-api-contract--openapi-specification.md` | OpenAPI 3.1 specs for all endpoints |
| `docs/blog-platform/02-folder-structure.md` | Annotated monorepo layout |
| `docs/blog-platform/11-data-migration-runbook.md` | EF Core migration workflow and rollback |
| `docs/blog-platform/07-disaster-recovery--backup.md` | PostgreSQL/Redis/MinIO backup strategies |
