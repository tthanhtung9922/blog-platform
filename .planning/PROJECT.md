# Blog Platform

## What This Is

A production-grade blog platform targeting Vietnamese content creators and readers. It provides a public reading experience (`blog-web`) with SSG/ISR performance and SEO, and a full-featured CMS dashboard (`blog-admin`) for authors and editors to create and manage content. The platform is built as a Nx monorepo with an ASP.NET Core 10 backend and two Next.js 16.1 frontends.

## Core Value

Readers can discover and read high-quality Vietnamese content instantly; authors can write and publish rich content through a powerful CMS — all on a self-hosted, open-source stack with no vendor lock-in.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Authentication: email/password registration with email verification, login with JWT + refresh tokens, OAuth (Google, GitHub), password reset, session persistence
- [ ] RBAC: 4 roles (Admin, Editor, Author, Reader) enforced at API, Application (MediatR), and Frontend (CASL) layers
- [ ] Post lifecycle: create (Draft), edit, publish (Editor/Admin only), archive — with slug generation, excerpt, cover image, tags, reading time
- [ ] Rich text editing: Tiptap v3 in Admin CMS (ProseMirror JSON), pre-rendered HTML stored for SSG performance
- [ ] Post versioning: content history saved on each update
- [ ] Public blog: SSG/ISR pages for post list and detail, tag filtering, author filtering, featured posts
- [ ] Comment system: nested replies (1 level), moderation (approve/reject by Editor/Admin), delete by owner or Editor/Admin
- [ ] Reactions: like toggle and bookmark toggle per post (authenticated users)
- [ ] User profiles: display name, bio, avatar, website, social links — public profile page
- [ ] Admin: user list (paginated, filterable by role/status), role assignment, user ban
- [ ] Search: PostgreSQL FTS with custom Vietnamese configuration (unaccent extension)
- [ ] Infrastructure: Docker + Kubernetes deployment, GitHub Actions CI/CD
- [ ] Observability: Prometheus + Grafana + Loki monitoring stack
- [ ] Caching: Redis 8 cache-aside via MediatR CachingBehavior (opt-in via ICacheableQuery), pattern-based invalidation via Domain Events

### Out of Scope

- Real-time features (websockets, live comments) — Phase 2+ complexity
- Email notifications / newsletter — Phase 2
- Analytics dashboard for authors — Phase 2
- Full-text search via Meilisearch — Phase 3 migration from PostgreSQL FTS
- Multi-author publications / organizations — Phase 3
- Paid membership / paywall — Phase 3
- AI-powered features (auto-tag, recommendations) — Phase 3
- Multi-tenant / SaaS architecture — Phase 4
- SSO / SAML — Phase 4
- Mobile app — not planned

## Context

- Architecture is fully documented in `docs/blog-platform/` including ADRs, database schema, API contracts, and deployment runbooks
- No application code exists yet — all docs are specifications ready for implementation
- Content is Vietnamese-first; FTS must support diacritics via PostgreSQL `unaccent` extension
- `IdentityUser` (ASP.NET Identity) and `User` (Domain aggregate) are **separate models** sharing only a GUID — see ADR-006
- Cross-context transactions (Register, Ban) use shared `DbConnection` — see ADR-007
- Cache is opt-in: queries implement `ICacheableQuery` to participate in CachingBehavior — see ADR-008
- Tiptap content stored as ProseMirror JSON (`body_json`) + pre-rendered HTML (`body_html`) in `post_contents` table

## Constraints

- **Tech Stack**: ASP.NET Core 10 + Next.js 16.1 + PostgreSQL 18 + Redis 8 + Nx monorepo — defined in CLAUDE.md, not negotiable
- **Frontend**: Tailwind CSS v4 (CSS-first config, no `tailwind.config.ts`), shadcn/ui, Tiptap v3
- **Auth**: NextAuth v5 (frontend) + ASP.NET Identity (backend) — separate concerns
- **Authorization**: CASL >= 6.8.0 (frontend) — pinned due to CVE-2026-1774 fix
- **Self-hosted**: All infrastructure must be self-hostable (MinIO not S3, Postal not SendGrid as primary, Meilisearch not Elasticsearch)
- **Testing**: xUnit 3.2 + Testcontainers for backend, Playwright 1.58 for E2E

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Nx monorepo | Share contracts/types between FE and BE; affected builds for CI speed | — Pending |
| Clean Architecture + DDD | Testability, maintainability, domain isolation; blog has rich business rules | — Pending |
| 2 separate Next.js apps | blog-web optimizes SSG/SEO; blog-admin optimizes interactivity; independent deploys | — Pending |
| RBAC at 3 layers | Defense in depth — API + MediatR AuthorizationBehavior + CASL frontend | — Pending |
| IdentityUser ≠ Domain User | Decouple auth provider from domain logic; enables future auth provider swap | — Pending |
| Shared DbConnection for cross-context tx | ACID compliance for Register/Ban spanning IdentityDbContext + BlogDbContext | — Pending |
| ICacheableQuery opt-in caching | Prevent accidental caching of real-time queries; explicit TTL declaration | — Pending |
| PostgreSQL FTS (Phase 1) → Meilisearch (Phase 3) | Avoid Elasticsearch SSPL license; PostgreSQL FTS sufficient for Phase 1 volume | — Pending |

---
*Last updated: 2026-03-12 after initialization*
