# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

This repository is currently in the **planning & specification phase** — all content is documentation only. No application code exists yet. The docs define the complete architecture, schema, and API contracts for the platform.

## Repository Layout

```
blog-platform/
├── docs/
│   ├── blog-platform/          # Architecture docs (ADRs, schema, API spec, runbooks)
│   └── git-commit-message-best-practices.md
```

The planned monorepo structure (not yet scaffolded) will be:
```
├── apps/
│   ├── blog-api/               # ASP.NET Core 10 backend
│   ├── blog-web/               # Next.js 16.1 public reader (SSG/ISR)
│   └── blog-admin/             # Next.js 16.1 CMS dashboard
├── libs/
│   ├── shared-contracts/       # OpenAPI-generated TypeScript types
│   └── shared-ui/              # Shared React component library
├── deploy/
│   ├── docker/
│   └── k8s/                    # Base + overlays (dev/staging/prod)
└── scripts/
    └── gen-types.sh            # Regenerate TypeScript types from OpenAPI spec
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 10 LTS, C# |
| Database | PostgreSQL 18 |
| Cache | Redis 8 |
| Object Storage | MinIO |
| Frontend | Next.js 16.1, TypeScript 6.0, Tailwind CSS v4 |
| Rich Text | Tiptap v3 (JSON format, not Markdown) |
| UI Components | shadcn/ui |
| Auth | NextAuth v5, ASP.NET Identity |
| Authorization | CASL >= 6.8.0 (frontend), Policy-based (backend) |
| Testing | xUnit 3.2 + Testcontainers (.NET), Playwright 1.58 (E2E) |
| Monorepo | Nx with @nx-dotnet/core plugin |
| CI/CD | GitHub Actions |

## Planned Commands

```bash
# Start all services
docker-compose up

# Backend tests
dotnet test Blog.UnitTests
dotnet test Blog.IntegrationTests
dotnet test Blog.ArchTests

# E2E tests
npx playwright test

# Regenerate TypeScript types from OpenAPI spec
scripts/gen-types.sh

# EF Core migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update

# Nx builds
nx build blog-api
nx build blog-web
nx build blog-admin
```

## Architecture — Key Design Decisions

### Backend: Clean Architecture + DDD

Four layers: **Domain → Application → Infrastructure → Presentation**

- **Domain** (`Blog.Domain`): Aggregates (Post, Comment, User), Value Objects (Slug, Email, ReadingTime, Tag), Domain Events, Repository interfaces only
- **Application** (`Blog.Application`): CQRS via MediatR. Pipeline behaviors in order: ValidationBehavior (FluentValidation) → LoggingBehavior → AuthorizationBehavior → CachingBehavior
- **Infrastructure** (`Blog.Infrastructure`): EF Core, Redis, MinIO, email (Postal + SendGrid fallback)
- **Presentation** (`Blog.API`): REST controllers, middleware

**Critical:** `IdentityUser` (ASP.NET Identity) and `User` (Domain aggregate) are **separate models in separate tables**, sharing only a GUID ID. Do not conflate them — see ADR-006.

### Caching

Cache-aside via Redis. Caching is **opt-in**: queries must implement `ICacheableQuery` to be cached (see ADR-008). Pattern-based cache invalidation is triggered by Domain Events.

### Frontend: Two Separate Next.js Apps

- **blog-web**: Public reader — uses SSG/ISR for performance. Posts rendered from Tiptap JSON (use `@tiptap/react` EditorContent in read-only mode; sanitize HTML output with DOMPurify).
- **blog-admin**: CMS dashboard — interactive with Tiptap v3 editor, CASL-based permission checks.

**Tailwind CSS v4** uses CSS-first configuration (no `tailwind.config.ts`).

### Authorization (3 Layers)

RBAC is enforced at all three layers: API controllers, MediatR AuthorizationBehavior, and frontend CASL. Roles: Admin, Editor, Author, Reader. See `docs/blog-platform/03-architecture-decisions.md` ADR-004 for the full permission matrix.

### Search

Phase 1: PostgreSQL FTS with custom Vietnamese configuration + unaccent extension. Phase 3: migrate to Meilisearch (see ADR-009).

### Cross-Context Transactions

Shared `DbConnection` (not DbContext) for operations spanning ASP.NET Identity and Domain contexts, e.g., user registration and ban (see ADR-007).

## Key Documentation

| File | Purpose |
|------|---------|
| `docs/blog-platform/03-architecture-decisions.md` | All 9 ADRs with rationale |
| `docs/blog-platform/06-database-schema.md` | Full ERD and table definitions |
| `docs/blog-platform/09-api-contract--openapi-specification.md` | OpenAPI 3.1 specs for all endpoints |
| `docs/blog-platform/02-folder-structure.md` | Annotated monorepo layout |
| `docs/blog-platform/11-data-migration-runbook.md` | EF Core migration workflow and rollback |
| `docs/blog-platform/07-disaster-recovery--backup.md` | PostgreSQL/Redis/MinIO backup strategies |

## Commit Convention

Follow Conventional Commits (see `docs/git-commit-message-best-practices.md`):

```
<type>[optional scope]: <description>
```

Types: `feat`, `fix`, `refactor`, `chore`, `perf`, `ci`, `ops`, `build`, `docs`, `style`, `revert`, `test`
