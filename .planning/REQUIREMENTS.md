# Requirements: Blog Platform

**Defined:** 2026-03-12
**Core Value:** Readers can discover and read high-quality Vietnamese content instantly; authors can write and publish rich content through a powerful CMS — all on a self-hosted, open-source stack with no vendor lock-in.

## v1 Requirements

### Authentication

- [ ] **AUTH-01**: User can register with email and password and receive a confirmation email
- [ ] **AUTH-02**: User can verify email via link sent at registration; unverified users cannot log in
- [ ] **AUTH-03**: User can log in with email and password and receive a JWT access token + refresh token
- [ ] **AUTH-04**: User session persists across browser refresh via refresh token rotation
- [ ] **AUTH-05**: User can log out and have their refresh token revoked
- [ ] **AUTH-06**: User can log in with Google OAuth (linked to domain account by email)
- [ ] **AUTH-07**: User can log in with GitHub OAuth (linked to domain account by email)
- [ ] **AUTH-08**: User can request a password reset and receive an email link (via Postal)
- [ ] **AUTH-09**: User can resend email verification link if the original expires

### RBAC

- [ ] **RBAC-01**: System enforces 4 roles: Admin, Editor, Author, Reader — with least-privilege defaults
- [ ] **RBAC-02**: RBAC is enforced at 3 layers: ASP.NET API authorization policies, MediatR AuthorizationBehavior, and CASL frontend permission gates
- [ ] **RBAC-03**: Admin can assign any role to any user
- [ ] **RBAC-04**: Admin can ban (deactivate) a user account, invalidating existing sessions

### Posts

- [ ] **POST-01**: Author/Editor/Admin can create a new post in Draft status
- [ ] **POST-02**: Author can edit their own post; Editor/Admin can edit any post
- [ ] **POST-03**: Editor/Admin can publish a post (Draft → Published)
- [ ] **POST-04**: Post owner or Admin can archive a post (Published → Archived, soft delete)
- [ ] **POST-05**: Author/Editor/Admin can set post title, excerpt, cover image URL, and tag assignments
- [ ] **POST-06**: Post body is stored as Tiptap v3 ProseMirror JSON with server-side pre-rendered HTML
- [ ] **POST-07**: Each post save creates a version snapshot in post_versions (content history)
- [ ] **POST-08**: CMS editor auto-saves draft every 30 seconds via debounced PUT request
- [ ] **POST-09**: Admin/Editor can toggle the is_featured flag on a published post
- [ ] **POST-10**: Post slug is auto-generated from title on create; author can customize slug before publish

### Tags

- [ ] **TAG-01**: Admin/Editor can create, update, and delete tags (with name + auto-generated slug)
- [ ] **TAG-02**: Author/Editor/Admin can assign tags to a post during create or edit
- [ ] **TAG-03**: Public users can filter published posts by tag slug via the API
- [ ] **TAG-04**: Tag pages are statically generated on blog-web with ISR revalidation

### Public Blog (blog-web)

- [ ] **WEB-01**: Public user can view a paginated list of published posts (SSG/ISR, newest first)
- [ ] **WEB-02**: Public user can view full post detail page by slug (SSG/ISR, includes body HTML)
- [ ] **WEB-03**: Public user can view posts filtered by author (author profile page with post list)
- [ ] **WEB-04**: Public user can view homepage featuring is_featured = true posts in a featured section
- [ ] **WEB-05**: Each blog-web page has per-page SEO meta tags (title, description, og:image, canonical URL)
- [ ] **WEB-06**: blog-web exposes an XML sitemap covering all published posts and tag pages

### Social

- [ ] **SOCL-01**: Authenticated user can add a top-level comment to a published post (max 5000 chars)
- [ ] **SOCL-02**: Authenticated user can reply to an existing top-level comment (1 level of nesting)
- [ ] **SOCL-03**: Comment author, Editor, or Admin can delete a comment
- [ ] **SOCL-04**: Editor/Admin can approve or reject pending comments via moderation queue
- [ ] **SOCL-05**: Authenticated user can toggle like on a published post (idempotent)
- [ ] **SOCL-06**: Authenticated user can toggle bookmark on a published post (idempotent)
- [ ] **SOCL-07**: Authenticated user can update their own profile (display name, bio, avatar URL, website, social links)
- [ ] **SOCL-08**: Public user can view any user's public profile page (display name, bio, post count, join date)

### Admin & CMS

- [ ] **ADMN-01**: Admin can view paginated list of all users with filters by role and active status
- [ ] **ADMN-02**: Editor/Admin can view and action the comment moderation queue (pending comments)
- [ ] **ADMN-03**: Authenticated user with Author role or above can upload images/media to MinIO via POST /api/v1/media/upload
- [ ] **ADMN-04**: Uploaded media URLs are insertable into Tiptap editor content (image extension)

### Search

- [ ] **SRCH-01**: Public user can search published posts by title and excerpt via query parameter
- [ ] **SRCH-02**: Search supports Vietnamese diacritics (unaccent extension + custom `vietnamese` PostgreSQL FTS config)
- [ ] **SRCH-03**: Search results are paginated with the same PaginatedPostList response shape as the post list

### Infrastructure

- [ ] **INFR-01**: All services (API, PostgreSQL, Redis, MinIO) run locally via Docker Compose
- [ ] **INFR-02**: Application deploys to Kubernetes with base + dev/staging/prod overlay manifests (Kustomize)
- [ ] **INFR-03**: GitHub Actions CI pipeline runs on all PRs: unit tests, integration tests, type check, lint, and gen-types freshness gate
- [ ] **INFR-04**: GitHub Actions CD deploys to staging automatically on merge to main; production deployment requires manual approval
- [ ] **INFR-05**: Prometheus + Grafana + Loki monitoring stack is deployed and collects API metrics, dashboards, and application logs
- [ ] **INFR-06**: Redis-backed distributed rate limiting middleware protects all API endpoints

---

## v2 Requirements

### Notifications

- **NOTF-01**: User receives email notification when someone comments on their post
- **NOTF-02**: User receives email notification when a reply is posted on their comment
- **NOTF-03**: User can subscribe to newsletter and receive new post digests
- **NOTF-04**: User can configure notification preferences (opt-in/out per type)

### Reader Experience

- **READ-01**: User can view their bookmarked posts at GET /users/me/bookmarks
- **READ-02**: Application supports dark mode (Tailwind v4 CSS variables)
- **READ-03**: Application supports i18n (Vietnamese / English toggle)
- **READ-04**: blog-web exposes an RSS feed for all published posts

### Author Analytics

- **ANLT-01**: Author can view post-level analytics (views, read time, engagement rate)
- **ANLT-02**: Author can see top-performing posts by engagement
- **ANLT-03**: Admin can view platform-wide content metrics dashboard

### Social Sharing

- **SHRG-01**: Post pages include social sharing buttons (link copy, Twitter/X, Facebook)
- **SHRG-02**: OpenGraph and Twitter Card metadata is complete for social sharing previews

---

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real-time comments / websockets | High infrastructure complexity; async moderation is sufficient for Phase 1 |
| Scheduled publishing | Requires background job system; deferred to v1.x after core workflow validated |
| Image upload via drag-drop/paste in Tiptap | Workaround: external URL input at launch; drag-drop requires MinIO presigned URL flow (v1.x) |
| Version restore UI in CMS | Data is captured (post_versions); restore UI adds complexity for low Phase 1 priority |
| My bookmarks list page | Toggle works at launch; dedicated list view adds polish; deferred to v2 |
| Multi-author publications / organizations | Phase 3 scope — requires significant data model changes |
| Paid membership / paywall | Phase 3 scope — Lago integration |
| AI-powered features (auto-tag, recommendations) | Phase 3 scope |
| Advanced media management (video embedding) | Phase 3+ scope |
| Meilisearch migration | Phase 3 — upgrade from PostgreSQL FTS when search volume demands |
| Multi-tenant / SaaS | Phase 4 |
| SSO / SAML | Phase 4 |
| Mobile app | Not planned |
| Email newsletter (beyond transactional) | v2 — requires user base to justify infrastructure |

---

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| INFR-01 | Phase 1 — Monorepo Foundation + Domain Layer | Pending |
| AUTH-01 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-02 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-03 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-04 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-05 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-06 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-07 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-08 | Phase 3 — Authentication + RBAC + Tags | Pending |
| AUTH-09 | Phase 3 — Authentication + RBAC + Tags | Pending |
| RBAC-01 | Phase 3 — Authentication + RBAC + Tags | Pending |
| RBAC-02 | Phase 3 — Authentication + RBAC + Tags | Pending |
| RBAC-03 | Phase 3 — Authentication + RBAC + Tags | Pending |
| RBAC-04 | Phase 3 — Authentication + RBAC + Tags | Pending |
| TAG-01 | Phase 3 — Authentication + RBAC + Tags | Pending |
| TAG-02 | Phase 3 — Authentication + RBAC + Tags | Pending |
| TAG-03 | Phase 3 — Authentication + RBAC + Tags | Pending |
| TAG-04 | Phase 3 — Authentication + RBAC + Tags | Pending |
| POST-01 | Phase 4 — Post Backend API | Pending |
| POST-02 | Phase 4 — Post Backend API | Pending |
| POST-03 | Phase 4 — Post Backend API | Pending |
| POST-04 | Phase 4 — Post Backend API | Pending |
| POST-05 | Phase 4 — Post Backend API | Pending |
| POST-06 | Phase 4 — Post Backend API | Pending |
| POST-07 | Phase 4 — Post Backend API | Pending |
| POST-08 | Phase 4 — Post Backend API | Pending |
| POST-09 | Phase 4 — Post Backend API | Pending |
| POST-10 | Phase 4 — Post Backend API | Pending |
| WEB-01 | Phase 5 — Public Blog Frontend | Pending |
| WEB-02 | Phase 5 — Public Blog Frontend | Pending |
| WEB-03 | Phase 5 — Public Blog Frontend | Pending |
| WEB-04 | Phase 5 — Public Blog Frontend | Pending |
| WEB-05 | Phase 5 — Public Blog Frontend | Pending |
| WEB-06 | Phase 5 — Public Blog Frontend | Pending |
| SOCL-01 | Phase 6 — Social Features | Pending |
| SOCL-02 | Phase 6 — Social Features | Pending |
| SOCL-03 | Phase 6 — Social Features | Pending |
| SOCL-04 | Phase 6 — Social Features | Pending |
| SOCL-05 | Phase 6 — Social Features | Pending |
| SOCL-06 | Phase 6 — Social Features | Pending |
| SOCL-07 | Phase 6 — Social Features | Pending |
| SOCL-08 | Phase 6 — Social Features | Pending |
| ADMN-01 | Phase 7 — Admin Features + Media Upload | Pending |
| ADMN-02 | Phase 7 — Admin Features + Media Upload | Pending |
| ADMN-03 | Phase 7 — Admin Features + Media Upload | Pending |
| ADMN-04 | Phase 7 — Admin Features + Media Upload | Pending |
| SRCH-01 | Phase 8 — Search | Pending |
| SRCH-02 | Phase 8 — Search | Pending |
| SRCH-03 | Phase 8 — Search | Pending |
| INFR-02 | Phase 9 — CI/CD + Kubernetes | Pending |
| INFR-03 | Phase 9 — CI/CD + Kubernetes | Pending |
| INFR-04 | Phase 9 — CI/CD + Kubernetes | Pending |
| INFR-05 | Phase 10 — Observability + Rate Limiting | Pending |
| INFR-06 | Phase 10 — Observability + Rate Limiting | Pending |

**Coverage:**
- v1 requirements: 54 total
- Mapped to phases: 53 (INFR-01 to Phase 1; AUTH/RBAC/TAG to Phase 3; POST to Phase 4; WEB to Phase 5; SOCL to Phase 6; ADMN to Phase 7; SRCH to Phase 8; INFR-02..04 to Phase 9; INFR-05..06 to Phase 10)
- Phase 2 (Infrastructure + Application Pipeline): enabling phase with no standalone requirement IDs — foundational work that unblocks Phase 3+
- Unmapped: 0 — all 54 requirements assigned ✓

---
*Requirements defined: 2026-03-12*
*Last updated: 2026-03-12 — Traceability updated after roadmap creation (10 phases)*
