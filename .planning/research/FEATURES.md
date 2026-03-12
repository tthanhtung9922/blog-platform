# Feature Research

**Domain:** Production blog platform (Vietnamese content creators, self-hosted)
**Researched:** 2026-03-12
**Confidence:** HIGH

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete or broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Email/password registration + login | Every platform has it; users have no other way to authenticate | LOW | Already specified in API (ADR-006). Email verification is mandatory — unverified accounts can't log in |
| OAuth login (Google, GitHub) | Readers and tech-focused Vietnamese creators strongly prefer social login to avoid password creation | MEDIUM | NextAuth v5 providers; backend must map OAuth identity to IdentityUser on first sign-in |
| Password reset via email | Industry default; any platform without it creates immediate trust loss | MEDIUM | Requires transactional email (Postal primary, SendGrid fallback). Spec does not define this endpoint yet — gap identified |
| JWT + refresh token session | Standard secure session model; users expect persistent login across browser restarts | MEDIUM | `/auth/refresh` and `/auth/revoke` already defined in OpenAPI spec |
| Public post list with pagination | Readers expect to browse posts; no list = no discoverability | LOW | Defined in `/posts` GET with pagination, tag/author filter |
| Individual post page with readable content | Core reader experience; rich HTML output must be clean and safe | LOW | SSG/ISR via blog-web. HTML sanitization with DOMPurify is required |
| Tag-based filtering | Standard navigation; readers browse by topic | LOW | Defined in API. Tags need CRUD management endpoint (gap — no tags API in spec) |
| Author profile page | Readers want to know who wrote content; public credibility signal | LOW | `/users/{username}` defined. Public profile with post count and bio |
| Author filtering on post list | Common on content-heavy platforms; lets readers follow specific authors | LOW | Already defined via `author` query param on `/posts` |
| Featured posts on homepage | Every blog has editorial curation; "featured" is a core display pattern | LOW | `is_featured` field on posts. No admin API to set `is_featured` defined — gap identified |
| Reading time display | Users scan lists with reading time to decide what to read; expected since Medium popularized it | LOW | `reading_time_minutes` computed as Value Object in domain, stored in DB |
| Search (basic) | Users expect to find content; any platform without search feels incomplete above ~50 posts | MEDIUM | PostgreSQL FTS with Vietnamese `unaccent` config (ADR-009). Already in API via `search` param |
| Rich text editing in CMS | Authors expect a WYSIWYG editor; raw HTML editing is unacceptable for non-dev users | HIGH | Tiptap v3 (ProseMirror JSON). More complex than Markdown editors but specified |
| Draft / publish workflow | Authors need to save work-in-progress; accidental publishing is catastrophic | LOW | Draft → Published → Archived lifecycle defined. Publish requires Editor/Admin role |
| Cover image per post | Visual content is the norm; text-only posts appear unfinished | LOW | `cover_image_url` in schema. Upload endpoint not defined in spec — gap identified |
| Comment system | Reader engagement mechanism; absence signals the platform is not a community | MEDIUM | Nested (1 level), moderation queue, approve/reject defined |
| Comment moderation | Without moderation, spam dominates and readers leave; this is standard infrastructure | MEDIUM | `/comments/{id}/moderate` defined. Needs a moderation queue view in admin |
| Like/react on posts | Lightweight engagement signal; users expect at least a like button | LOW | Toggle like defined in Reactions API |
| Bookmark / save for later | Readers browsing long-form content expect to save posts; absence is noticeable | LOW | Toggle bookmark defined. No "my bookmarks" list endpoint — gap identified |
| User profile editing | Authors and readers need to set display name, avatar, bio | LOW | `/users/me/profile` PUT defined |
| SEO meta tags (title, description, OG, canonical) | Blog content is worthless without Google indexability; og:image drives social sharing | MEDIUM | Not in API spec — this is entirely frontend responsibility in blog-web (Next.js metadata API). Must be implemented per-page |
| XML sitemap | Google and Bing require it for indexing; absence hurts organic discovery significantly | MEDIUM | Frontend-only concern (Next.js sitemap generation). Not defined anywhere — gap identified |
| Responsive mobile layout | Over 70% of Vietnamese internet users access on mobile; non-responsive = unusable | MEDIUM | Tailwind CSS v4 + shadcn/ui — responsive by default, but must be designed intentionally |
| HTTPS + secure auth | Browser security warnings on plain HTTP cause immediate abandonment | LOW | Infrastructure/deployment concern; K8s ingress must terminate TLS |
| Admin: user list, role assignment, ban | Platform cannot operate without admin tools to manage users and escalate permissions | MEDIUM | Defined in Users API (Admin only) |
| RBAC enforced at all layers | Without authorization, content integrity fails; authors can publish, vandalize others' posts | HIGH | 3-layer RBAC (API + MediatR + CASL). Fully specified in ADR-004 |
| Post versioning (content history) | Authors expect to recover from accidental overwrites; editors need audit trail | MEDIUM | `post_versions` table defined. No API endpoint to list/restore versions — gap identified |
| Autosave drafts | Every modern CMS autosaves; losing work to a closed tab causes authors to abandon the platform | MEDIUM | Not defined in spec or API. Must be implemented client-side (periodic PUT to update draft) — gap identified |
| Image upload within editor | Authors expect to paste/drop images directly into the editor; external URL-only is a friction point | HIGH | Not in spec. Requires upload endpoint (multipart POST → MinIO), Tiptap image extension. Critical gap |
| Slug customization | Authors want human-readable URLs; auto-generated slugs from titles are expected but manual override is necessary | LOW | Slug is in schema. No explicit field for author-customizable slug in CreatePostRequest — gap identified |

---

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable — especially for the Vietnamese creator market.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Vietnamese-first full-text search | Most platforms use `english` FTS config; searching "phat trien" and finding "phát triển" is a core quality differentiator for Vietnamese content | MEDIUM | PostgreSQL FTS with custom `vietnamese` config + `unaccent`. Defined in ADR-009. Competitors on generic platforms don't do this |
| Post content versioning with restore | Ghost and Hashnode have this; most self-hosted platforms don't. Builds author trust | MEDIUM | Data model exists (`post_versions`). Needs API endpoints and admin UI to view diffs and restore |
| Self-hosted, no vendor lock-in | Vietnamese content creators increasingly wary of platform dependence (Medium paywalls, Substack fees). Full data ownership is a meaningful selling point | HIGH | Stack decision: MinIO not S3, Postal not SendGrid, Meilisearch (Phase 3) not Algolia |
| Editor/Author separation (editorial workflow) | Hobbyist platforms let anyone publish. This platform enforces Editor approval before publish — quality signal for Vietnamese professional content | LOW | Already designed in RBAC. Editor/Admin-only publish permission is differentiating vs self-publishing platforms |
| Tiptap v3 rich editor (code blocks, tables, embeds) | Markdown-only editors (DEV.to, Hashnode basic) frustrate non-developer Vietnamese writers. Rich JSON-based editor with proper formatting is a significant UX advantage | HIGH | Tiptap v3 specified. Requires careful extension selection — code highlighting, tables, images, embeds |
| Featured posts curation | Editorial curation of "featured" content distinguishes platform-quality from dump-everything blogs | LOW | `is_featured` field already exists. Low lift if admin API to set it is added |
| Bookmark list / personal reading list | Readers engaging with a content library want to curate their own reading queue | MEDIUM | Bookmark toggle exists; needs "my bookmarks" list endpoint and a UI view |
| SSG/ISR public site (sub-second loads) | Vietnamese mobile networks are often congested; sub-second page loads on mobile are a real differentiator vs dynamic-render competitor platforms | HIGH | Next.js 15+ SSG/ISR. Pre-rendered HTML stored in `body_html` column avoids runtime rendering cost |
| Prometheus + Grafana observability | Self-hosters need operational visibility; most simple blog platforms have no observability story | MEDIUM | Already specified in infrastructure. Differentiates this platform from basic self-hosted blogs |

---

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems — deliberately exclude from Phase 1.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Real-time comments (WebSocket) | "Live" feeling for readers | Requires WebSocket infrastructure (SignalR or SSE), complicates deployment, cache invalidation breaks, K8s scaling becomes stateful. Zero proven user value vs polling at Phase 1 scale | Poll-based comment loading with short cache TTL (2 min) — already specified in ADR-005 |
| Email notifications / newsletter | Authors want to reach readers; readers want alerts | Requires transactional email + unsubscribe management + bounce handling + GDPR compliance. Full product in itself. Postal integration adds significant complexity | Defer to Phase 2. Use RSS feed as Phase 1 substitute (below) |
| Analytics dashboard for authors | Authors want to see view counts, engagement | Requires event pipeline (page views, scroll depth), storage, aggregation queries. PostgreSQL cannot handle high-volume analytics writes at scale efficiently | Defer to Phase 2. Show comment count + like count per post as proxy engagement signals |
| AI auto-tagging / recommendations | Modern AI-powered feature, marketable | Depends on a large content corpus to be useful. Adds external API dependency or significant compute. Premature at Phase 1 scale | Defer to Phase 3. Manual tagging works well at Phase 1 content volumes |
| Scheduled publishing (future publish date) | Authors want to queue posts | Adds background job complexity (Hangfire/Quartz), race conditions in caching, confusing RBAC implications (who can schedule?) | Author submits for review; Editor publishes immediately. Simplest workflow for Phase 1 |
| Multi-author publications / organizations | Groups of authors collaborating under one brand | Requires tenant-aware data model changes, complex RBAC expansion, significant schema migration. | Defer to Phase 3. Single-author-per-post with Editor role provides enough collaboration for Phase 1 |
| Paid membership / paywall | Monetization for content creators | Billing, subscription management, payment gateway integration (Stripe/Lago), access control per post. Full product. Premature for a platform with no user base yet | Defer to Phase 3 |
| Custom domain per author | Power users want their own domain pointing to their author page | Requires wildcard TLS, dynamic routing config, DNS verification flow. K8s ingress complexity | Defer to Phase 4. Single platform domain serves Phase 1 |
| Comment voting / upvoting (Reddit-style) | Engagement signal | Adds ranking algorithm complexity, gaming/abuse patterns, display logic changes. Distracts from quality writing community tone | Simple likes on posts; flat comment ordering with `created_at` sort |
| WYSIWYG email digest / newsletter builder | Authors want to compose newsletters visually | Completely separate product domain. Postal integration for transactional email ≠ newsletter builder | Defer entirely. Out of scope |

---

## Feature Dependencies

```
[Auth: Register + Login]
    └──required by──> [All authenticated features]
                          ├──> [Post: Create Draft]
                          │       └──required by──> [Post: Publish (Editor/Admin)]
                          │                              └──required by──> [Public: Post List / Post Detail]
                          ├──> [Comment: Post a comment]
                          │       └──required by──> [Comment: Moderate (Editor/Admin)]
                          ├──> [Reactions: Like / Bookmark]
                          └──> [Profile: Edit own profile]

[Post: Create Draft]
    └──required by──> [Post Versioning]
    └──required by──> [Image Upload in Editor]  <-- needs MinIO upload endpoint first

[Tags: CRUD management]
    └──required by──> [Post: tag assignment on create/update]
    └──required by──> [Public: tag filtering on post list]

[RBAC: Role definitions]
    └──required by──> [Admin: user management, role assignment, ban]
    └──required by──> [Post: publish gate (Editor/Admin only)]
    └──required by──> [Comment: moderation (Editor/Admin only)]

[SEO: meta tags + OG image]
    └──enhances──> [Public: Post Detail page]
    └──enhances──> [Author Profile page]

[XML Sitemap]
    └──requires──> [Public: Post List] (to enumerate published posts)

[Bookmark: toggle]
    └──enhances by──> [My Bookmarks: list view]  (not yet defined — Phase 1 gap)

[Post Versioning: save on update]
    └──enhances by──> [Version restore API]  (deferred — data exists, endpoint missing)
```

### Dependency Notes

- **Auth is the root dependency:** Every authenticated feature (posting, commenting, reacting, admin) requires the full auth flow including email verification to be working first.
- **Tags API must exist before post creation is complete:** The `CreatePostRequest` accepts `tagIds`, but there is no Tags CRUD API defined in the spec. Tags must be pre-seeded or creatable before authors can categorize posts.
- **Image upload requires MinIO before Tiptap image extension:** The editor can accept image URLs already, but in-editor image upload (drag-drop/paste) requires a working upload endpoint (`POST /api/v1/media/upload` or similar) backed by MinIO. This is a hard dependency for a smooth author experience.
- **`is_featured` toggle requires an admin endpoint:** The field exists in the schema, but no API endpoint to set it is defined. Homepage featured posts require this.
- **Autosave requires no new API endpoint:** Autosave is a client-side behavior — the Tiptap editor periodically calls the existing `PUT /posts/{id}` to persist the draft. Implementation is in blog-admin only.

---

## MVP Definition

### Launch With (v1 — Phase 1 Production Launch)

Minimum viable for production — what's needed for real readers and authors to use the platform.

- [ ] Full auth flow: register, email verification, login, OAuth (Google/GitHub), password reset, refresh token, logout
- [ ] Post CRUD: create draft, edit, publish (Editor/Admin gate), archive, delete
- [ ] Rich text editor (Tiptap v3) with heading, bold, italic, lists, code blocks, links, images (URL-based minimum)
- [ ] Tags: create tags, assign to posts, filter post list by tag
- [ ] Public blog-web: post list (SSG), post detail (ISR), tag filter, author filter, featured posts
- [ ] SEO: per-page title/description/og:image meta tags, canonical URLs, XML sitemap
- [ ] Comment system: post comment, nested replies, moderation (approve/reject), delete
- [ ] Reactions: like toggle, bookmark toggle
- [ ] User profiles: public profile page, edit own profile (display name, avatar URL, bio, social links)
- [ ] RBAC: 4 roles, enforced at API + MediatR + CASL
- [ ] Admin: user list, role assignment, user ban
- [ ] Post versioning: save content snapshot on each update (data only — restore UI deferred)
- [ ] Client-side autosave in blog-admin editor
- [ ] Password reset email (requires Postal transactional email setup)
- [ ] Admin: set/unset `is_featured` on posts (requires new API endpoint)
- [ ] Vietnamese FTS search (PostgreSQL `unaccent` + custom config)
- [ ] Infrastructure: Docker + K8s + CI/CD + Prometheus/Grafana/Loki

### Add After Validation (v1.x — Post-launch fixes)

Features to add once core is working and real-user feedback surfaces gaps.

- [ ] In-editor image upload (drag-drop/paste → MinIO) — initially authors use external URLs; upgrade once usage proves need
- [ ] My bookmarks list — toggle works at launch; dedicated list view adds polish
- [ ] Post version restore UI — data is being captured; expose it when authors ask

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] Email notifications / newsletter (Phase 2) — significant infrastructure, needs user base to justify
- [ ] Author analytics dashboard (Phase 2) — requires event pipeline
- [ ] Dark mode (Phase 2) — Tailwind CSS v4 supports it; design work deferred
- [ ] Meilisearch migration (Phase 3) — upgrade from PostgreSQL FTS when search quality or scale demands it
- [ ] Multi-author publications (Phase 3) — schema changes, RBAC expansion
- [ ] Paid membership / paywall (Phase 3) — billing product
- [ ] AI features: auto-tag, recommendations (Phase 3) — needs content corpus

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Auth (register/login/OAuth/password-reset) | HIGH | MEDIUM | P1 |
| Post CRUD + publish workflow | HIGH | MEDIUM | P1 |
| Tiptap v3 rich editor | HIGH | HIGH | P1 |
| Tags API + tag filtering | HIGH | LOW | P1 |
| Public blog-web (SSG/ISR) | HIGH | HIGH | P1 |
| SEO meta tags + XML sitemap | HIGH | MEDIUM | P1 |
| Comment system + moderation | HIGH | MEDIUM | P1 |
| RBAC (3-layer) | HIGH | HIGH | P1 |
| Admin user management | HIGH | MEDIUM | P1 |
| Vietnamese FTS search | HIGH | MEDIUM | P1 |
| Like / bookmark reactions | MEDIUM | LOW | P1 |
| User profile (edit + public page) | MEDIUM | LOW | P1 |
| Post versioning (data capture) | MEDIUM | LOW | P1 |
| Autosave drafts (client-side) | MEDIUM | LOW | P1 |
| `is_featured` admin toggle | MEDIUM | LOW | P1 |
| Password reset email | HIGH | MEDIUM | P1 |
| In-editor image upload (MinIO) | HIGH | HIGH | P2 |
| My bookmarks list | MEDIUM | LOW | P2 |
| Post version restore UI | LOW | MEDIUM | P2 |
| Dark mode | LOW | MEDIUM | P3 |
| RSS feed | MEDIUM | LOW | P2 |

---

## Gaps Identified in Current Specification

These features are expected by users or required for completeness but are **not yet defined** in the API contract or requirements:

| Gap | Category | Severity | Notes |
|-----|----------|----------|-------|
| Tags CRUD API (`GET/POST/DELETE /tags`) | Table Stakes | HIGH | `tagIds` used in post create/update but no tag management endpoint exists. Authors cannot tag posts without seeded data or admin tooling |
| `is_featured` toggle endpoint (`PATCH /posts/{id}/feature`) | Table Stakes | MEDIUM | Field exists in schema; no API to set it. Needed for homepage featured section |
| Image/media upload endpoint (`POST /media/upload`) | Table Stakes | HIGH | No upload API defined. Tiptap editor can use external URLs as a workaround initially, but in-editor upload is expected by non-technical authors |
| Password reset flow (`POST /auth/forgot-password`, `POST /auth/reset-password`) | Table Stakes | HIGH | Email/password auth without password reset is incomplete. Not in OpenAPI spec |
| Email confirmation resend (`POST /auth/resend-confirmation`) | Table Stakes | MEDIUM | Users who miss the confirmation email need a resend path |
| My bookmarks list (`GET /users/me/bookmarks`) | Table Stakes | MEDIUM | Bookmark toggle exists; no list endpoint to retrieve saved posts |
| Post versions list (`GET /posts/{id}/versions`) | Differentiator | LOW | Data captured; no read endpoint. Add when restore UI is built |
| Tags list endpoint for admin/editor (`GET /admin/tags`) | Table Stakes | LOW | For managing/creating tags in the CMS |
| XML sitemap generation | Table Stakes | MEDIUM | Frontend-only (Next.js), but no spec or plan for it. Required for SEO |
| Slug override field in `CreatePostRequest` | Table Stakes | LOW | Auto-generated from title but no way for author to customize before creation |
| RSS feed (`GET /feed.xml` or `/rss`) | Differentiator | LOW | Expected by readers who use feed readers; Vietnamese tech community uses RSS |

---

## Competitor Feature Analysis

| Feature | Medium | Ghost (self-hosted) | Hashnode | DEV.to | This Platform (Phase 1) |
|---------|--------|---------------------|---------|--------|-------------------------|
| Rich text editor | Block editor | Ghost editor (Markdown+) | Block editor | Markdown only | Tiptap v3 (ProseMirror JSON) — most flexible |
| Vietnamese search | No custom config | No custom config | No custom config | No custom config | PostgreSQL FTS + `unaccent` — native advantage |
| Self-hosted | No | Yes | No | No (Forem is open-source) | Yes — full stack self-hostable |
| Custom domain | No (paid) | Yes | Yes | No | Single domain Phase 1; custom domain Phase 4 |
| Editorial workflow (draft approval) | No | Basic | No | No | Yes — Editor/Admin publish gate |
| Post versioning | No | Yes | No | No | Yes (data capture Phase 1, restore UI Phase 2) |
| Comment moderation | No | Yes | No | Yes | Yes — approve/reject queue |
| In-editor image upload | Yes | Yes | Yes | Yes | Phase 1 gap; Phase 2 delivery |
| Autosave | Yes | Yes | Yes | Yes | Client-side implementation (Phase 1) |
| Email newsletter | Yes (paid) | Yes (built-in) | No | No | Phase 2 |
| Analytics for authors | Yes | Yes | Yes | Yes | Phase 2 |
| Dark mode | Yes | Yes | Yes | Yes | Phase 2 |
| Bookmark / reading list | Yes | No | No | Yes (reading list) | Toggle Phase 1; list view Phase 2 |

---

## Sources

- Project specification: `.planning/PROJECT.md`, `docs/blog-platform/04-long-term-roadmap.md`, `docs/blog-platform/09-api-contract--openapi-specification.md`
- Architecture decisions: `docs/blog-platform/03-architecture-decisions.md` (ADR-004, ADR-005, ADR-008, ADR-009)
- Database schema: `docs/blog-platform/06-database-schema.md`
- Competitor research: [Hashnode vs DEV.to comparison](https://www.blogbowl.io/blog/posts/hashnode-vs-dev-to-which-platform-is-best-for-developers-in-2025) (MEDIUM confidence — verified against known platform capabilities)
- Ghost alternatives analysis: [10 Best Ghost Alternatives 2025](https://hyvor.com/blog/ghost-alternatives) (MEDIUM confidence)
- CMS draft/autosave patterns: [Payload CMS versioning docs](https://payloadcms.com/docs/versions/drafts) (HIGH confidence — official docs)
- SEO requirements: [Next.js metadata and OG images](https://nextjs.org/docs/app/getting-started/metadata-and-og-images) (HIGH confidence — official docs)
- Comment moderation necessity: [WordPress comment moderation guide](https://www.cloudways.com/blog/moderate-comments-in-wordpress/) (MEDIUM confidence)
- MinIO self-hosted media: [MinIO best practices](https://blog.min.io/tag/best-practices/) (HIGH confidence — official source)
- Vietnamese creator market context: [Vietnam digital platform opportunities](https://mmcommunications.vn/en/make-money-online-2025-vietnam-digital-platform-opportunities-n464) (LOW confidence — single source)

---
*Feature research for: Vietnamese content creator blog platform*
*Researched: 2026-03-12*
