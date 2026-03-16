# Roadmap: Blog Platform

## Overview

The platform builds in strict dependency order: the Domain layer (no external dependencies) and monorepo scaffold come first, followed by the Infrastructure and Application pipeline that implements the domain interfaces, then Authentication and RBAC (the root dependency for every subsequent feature), then the Post feature (the primary content entity), the public blog frontend, social features, admin tooling, search, deployment, and finally observability and rate limiting. Each phase delivers a coherent, independently verifiable capability. With fine granularity, the 7 logical groups from research expand to 10 focused phases.

## Phases

- [x] **Phase 1: Monorepo Foundation + Domain Layer** - Nx scaffold, Blog.Domain aggregates, EF Core migrations, PostgreSQL setup, Docker Compose (completed 2026-03-15)
- [ ] **Phase 2: Infrastructure + Application Pipeline** - Blog.Infrastructure, Blog.Application, MediatR 4-behavior pipeline, Redis cache-aside, Testcontainers scaffold
- [ ] **Phase 3: Authentication + RBAC + Tags** - Full auth flow, 3-layer RBAC, Tags CRUD — root dependency for all features
- [ ] **Phase 4: Post Backend API** - Complete Post aggregate lifecycle, versioning, autosave, Tiptap JSON pipeline, RevalidationService
- [ ] **Phase 5: Public Blog Frontend** - blog-web SSG/ISR post list, post detail, tag/author pages, SEO meta tags, sitemap
- [ ] **Phase 6: Social Features** - Comments with moderation, like/bookmark reactions, user profile edit and public view
- [ ] **Phase 7: Admin Features + Media Upload** - User management, role assignment, ban, media upload to MinIO, blog-admin CMS pages
- [ ] **Phase 8: Search** - PostgreSQL FTS with Vietnamese unaccent config, search endpoint, blog-web search page
- [ ] **Phase 9: CI/CD + Kubernetes** - GitHub Actions CI/CD pipelines, Kubernetes Kustomize manifests, staging/prod deployment
- [ ] **Phase 10: Observability + Rate Limiting** - Prometheus + Grafana + Loki stack, Redis-backed rate limiting middleware

## Phase Details

### Phase 1: Monorepo Foundation + Domain Layer
**Goal**: The Nx monorepo is scaffolded with correct project graph boundaries, Blog.Domain is complete with all aggregates and value objects, PostgreSQL 18 is running with EF Core migrations applied, and architecture tests prevent layer pollution from day one.
**Depends on**: Nothing (first phase)
**Requirements**: INFR-01
**Success Criteria** (what must be TRUE):
  1. Developer can run `docker-compose up` and have PostgreSQL 18, Redis 8, and MinIO available locally
  2. `nx build blog-api` succeeds and `Blog.ArchTests` passes with zero violations
  3. EF Core migration runs cleanly against PostgreSQL 18 with the `unaccent` extension enabled
  4. `shared-contracts` is declared as an implicit dependency in both frontend `project.json` files and Nx graph reflects this
  5. All domain aggregates (Post, Comment, User), value objects (Slug, Email, ReadingTime, Tag), and domain events compile with no infrastructure references
**Plans**: 4 plans

Plans:
- [ ] 01-01-PLAN.md — Nx workspace scaffold, project graph registration, Docker Compose (PostgreSQL 18 + Redis 8 + MinIO)
- [ ] 01-02-PLAN.md — Blog.Domain: base classes, all 4 aggregates, value objects, domain events, repository interfaces
- [ ] 01-03-PLAN.md — Blog.Infrastructure (minimal): BlogDbContext, entity configs, EF Core migrations + bare Blog.API
- [ ] 01-04-PLAN.md — Blog.ArchTests: layer boundary tests + domain model integrity tests

### Phase 2: Infrastructure + Application Pipeline
**Goal**: Blog.Infrastructure implements all Domain interfaces, Blog.Application has the complete MediatR 4-behavior pipeline registered in the correct order, Redis cache-aside with pattern invalidation is operational, and the Testcontainers integration test scaffold is ready for feature tests.
**Depends on**: Phase 1
**Requirements**: (no standalone requirement IDs — enabling infrastructure for all feature phases)
**Success Criteria** (what must be TRUE):
  1. MediatR pipeline behaviors execute in order: ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior — verified by an architecture test
  2. `IUnitOfWork` cross-context transaction wrapper correctly rolls back both IdentityDbContext and BlogDbContext on failure — verified by an integration test
  3. A query implementing `ICacheableQuery` is served from Redis on the second call — verified by an integration test
  4. Redis pattern-based cache invalidation via Lua script clears the correct key patterns when a Domain Event fires
  5. Integration test suite runs against real PostgreSQL 18 + Redis 8 via Testcontainers with no environment-specific config
**Plans**: TBD

### Phase 3: Authentication + RBAC + Tags
**Goal**: Users can register, verify email, log in via email/password or OAuth, manage sessions, and reset passwords — and the 3-layer RBAC (API + MediatR + CASL) is enforced across all roles. Tags CRUD is included here because it is required before authors can create posts.
**Depends on**: Phase 2
**Requirements**: AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTH-07, AUTH-08, AUTH-09, RBAC-01, RBAC-02, RBAC-03, RBAC-04, TAG-01, TAG-02, TAG-03, TAG-04
**Success Criteria** (what must be TRUE):
  1. User can register with email/password, receive a confirmation email via Postal, verify their email, and log in — unverified users are blocked at login
  2. User can log in via Google OAuth or GitHub OAuth and have the account linked to their domain account by email
  3. User session persists across browser refresh via refresh token rotation; user can log out and have the refresh token immediately revoked
  4. User can request a password reset link via email and set a new password via that link
  5. Admin can assign any role to any user and ban a user — a banned user's existing JWT sessions are invalidated immediately
  6. An Editor or Admin can create, update, and delete tags; any public user can filter posts by tag slug via the API
**Plans**: TBD

### Phase 4: Post Backend API
**Goal**: The complete Post aggregate lifecycle — create, edit, publish, archive — is available via the API, with Tiptap JSON stored server-side as ProseMirror JSON plus pre-rendered sanitized HTML, post versioning captured on each save, autosave via debounced PUT, featured post toggle, and ISR revalidation triggered on every publish/update/archive event.
**Depends on**: Phase 3
**Requirements**: POST-01, POST-02, POST-03, POST-04, POST-05, POST-06, POST-07, POST-08, POST-09, POST-10
**Success Criteria** (what must be TRUE):
  1. Author can create a post in Draft status and edit it; Editor/Admin can publish it; owner or Admin can archive it
  2. Post body is stored as ProseMirror JSON and pre-rendered HTML is generated server-side from that JSON (never accepted from the client), then sanitized before storage
  3. Each post save creates a version snapshot in `post_versions` retrievable via the API
  4. CMS editor auto-saves the draft every 30 seconds via a debounced PUT request without user interaction
  5. On every publish, update, or archive event, the `RevalidationService` fires `POST /api/revalidate` to `blog-web` to invalidate ISR cache
**Plans**: TBD

### Phase 5: Public Blog Frontend
**Goal**: Readers can discover and read published Vietnamese content instantly via SSG/ISR pages on blog-web, with tag and author filtering, featured post sections, full SEO meta tags on every page, and an XML sitemap.
**Depends on**: Phase 4
**Requirements**: WEB-01, WEB-02, WEB-03, WEB-04, WEB-05, WEB-06
**Success Criteria** (what must be TRUE):
  1. Public user can view a paginated list of published posts at the blog-web homepage, newest first, rendered via SSG/ISR
  2. Public user can open any post detail page by slug and read the full body rendered from pre-rendered HTML via Tiptap EditorContent in read-only mode, sanitized with DOMPurify
  3. Public user can filter posts by tag (tag page) or by author (author profile page) — both pages are statically generated with ISR revalidation
  4. Homepage displays is_featured posts in a featured section separate from the chronological list
  5. Every blog-web page has correct title, description, og:image, and canonical URL meta tags; an XML sitemap at `/sitemap.xml` covers all published posts and tag pages
**Plans**: TBD

### Phase 6: Social Features
**Goal**: Authenticated users can comment on posts with 1-level nested replies, moderate comments (Editor/Admin), toggle likes and bookmarks per post, and manage their public profile — all from blog-web and blog-admin.
**Depends on**: Phase 5
**Requirements**: SOCL-01, SOCL-02, SOCL-03, SOCL-04, SOCL-05, SOCL-06, SOCL-07, SOCL-08
**Success Criteria** (what must be TRUE):
  1. Authenticated user can post a top-level comment on a published post and reply to an existing comment (1 level of nesting); pending comments are not visible to non-moderators
  2. Comment author, Editor, or Admin can delete a comment; Editor/Admin can approve or reject pending comments via a moderation queue in blog-admin
  3. Authenticated user can toggle like on a post and toggle bookmark on a post — both operations are idempotent and reflect the current state immediately
  4. Authenticated user can update their own profile (display name, bio, avatar URL, website, social links) and the changes appear on their public profile page immediately
  5. Public user can view any user's public profile page showing display name, bio, post count, and join date
**Plans**: TBD

### Phase 7: Admin Features + Media Upload
**Goal**: Admins can manage users (list, filter, assign roles, ban) through blog-admin, and authors can upload images to MinIO via the media upload endpoint for insertion into Tiptap editor content.
**Depends on**: Phase 6
**Requirements**: ADMN-01, ADMN-02, ADMN-03, ADMN-04
**Success Criteria** (what must be TRUE):
  1. Admin can view a paginated list of all users filtered by role and/or active status in blog-admin
  2. Editor/Admin can view the comment moderation queue (pending comments across all posts) and approve or reject individual comments from blog-admin
  3. Authenticated user with Author role or above can upload an image via POST /api/v1/media/upload and receive a MinIO-hosted URL in response
  4. The returned media URL can be inserted into a Tiptap editor post body via the image extension and renders correctly in both blog-admin and blog-web
**Plans**: TBD

### Phase 8: Search
**Goal**: Public users can search published posts by title and excerpt with correct Vietnamese diacritic support, receiving paginated results in the same shape as the post list.
**Depends on**: Phase 7
**Requirements**: SRCH-01, SRCH-02, SRCH-03
**Success Criteria** (what must be TRUE):
  1. Public user can search published posts via query parameter and receive a paginated list of matching posts
  2. Search query "viet" returns posts containing "việt", "viết", "Việt", and other diacritic variants — verified against all 6 Vietnamese tonal marks (sắc, huyền, hỏi, ngã, nặng, flat)
  3. Search results pagination uses the same `PaginatedPostList` response shape as the standard post list endpoint
  4. A GIN index on the FTS vector column is present and the query plan shows index scan (not sequential scan) for search queries
**Plans**: TBD

### Phase 9: CI/CD + Kubernetes
**Goal**: The platform deploys to Kubernetes with environment-specific overlays, GitHub Actions CI runs on every PR with type check / lint / test / gen-types freshness gates, and CD deploys to staging automatically and production with manual approval.
**Depends on**: Phase 8
**Requirements**: INFR-02, INFR-03, INFR-04
**Success Criteria** (what must be TRUE):
  1. Application deploys to Kubernetes using base + dev/staging/prod overlay manifests (Kustomize) and all pods reach Running state
  2. GitHub Actions CI pipeline runs on every PR and fails if: unit tests fail, integration tests fail, TypeScript type check fails, lint fails, or `gen-types.sh` produces a diff against committed `shared-contracts`
  3. Merge to main automatically deploys to staging; production deployment requires a manual approval gate in GitHub Actions before proceeding
**Plans**: TBD

### Phase 10: Observability + Rate Limiting
**Goal**: The platform has full production observability via Prometheus + Grafana + Loki (metrics, dashboards, logs) and all API endpoints are protected by Redis-backed distributed rate limiting.
**Depends on**: Phase 9
**Requirements**: INFR-05, INFR-06
**Success Criteria** (what must be TRUE):
  1. Prometheus scrapes API metrics, Grafana displays at least one dashboard covering request rate, error rate, and latency, and Loki collects application logs — all deployed alongside the application
  2. A request that exceeds the rate limit threshold receives a 429 Too Many Requests response; the rate limit counter is consistent across multiple API pod replicas (backed by Redis, not in-memory)
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Monorepo Foundation + Domain Layer | 4/4 | Complete   | 2026-03-15 |
| 2. Infrastructure + Application Pipeline | 0/TBD | Not started | - |
| 3. Authentication + RBAC + Tags | 0/TBD | Not started | - |
| 4. Post Backend API | 0/TBD | Not started | - |
| 5. Public Blog Frontend | 0/TBD | Not started | - |
| 6. Social Features | 0/TBD | Not started | - |
| 7. Admin Features + Media Upload | 0/TBD | Not started | - |
| 8. Search | 0/TBD | Not started | - |
| 9. CI/CD + Kubernetes | 0/TBD | Not started | - |
| 10. Observability + Rate Limiting | 0/TBD | Not started | - |
