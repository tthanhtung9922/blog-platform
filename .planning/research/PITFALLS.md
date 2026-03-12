# Pitfalls Research

**Domain:** Production blog platform — ASP.NET Core 10 + Next.js 16.1 + PostgreSQL 18 + Nx monorepo + Clean Architecture + DDD
**Researched:** 2026-03-12
**Confidence:** HIGH (stack-specific claims verified through official docs and community sources)

---

## Critical Pitfalls

### Pitfall 1: Orphaned IdentityUser When Domain User Creation Fails

**What goes wrong:**
`RegisterCommand` creates an `IdentityUser` in `AspNetUsers`, then creates a `User` domain aggregate in `users`. If the second step throws (validation error, DB constraint, etc.) after the first step committed, the system has an `IdentityUser` with no corresponding domain `User`. On next login the user can authenticate but hits null-reference or "user not found" errors throughout the application because no domain `User` record exists.

**Why it happens:**
Two separate `DbContext` instances (`IdentityDbContext` + `BlogDbContext`) default to independent connections and transactions. EF Core's `SaveChangesAsync()` per context auto-commits each step independently. A developer writing the handler naively calls `IdentityService.CreateAsync()` then `UserRepository.AddAsync()` without wrapping in a shared transaction.

**How to avoid:**
Implement the shared `DbConnection` strategy documented in ADR-007. Both `IdentityDbContext` and `BlogDbContext` must share one `NpgsqlConnection` and one `NpgsqlTransaction`. Wrap this in `IUnitOfWork` so all cross-context operations go through a single code path. Write an integration test that forces a failure at step 2 and asserts that step 1 was rolled back (no orphaned `AspNetUsers` row). Also add a database-level health check: a scheduled background job or startup check that queries for `AspNetUsers` rows without a corresponding `users` row and alerts.

**Warning signs:**
- Users reporting "account exists but can't log in" or login succeeds but profile page 404s
- `AspNetUsers` row count > `users` row count
- Application errors referencing `null` `User` after a successful `SignInManager` call
- Integration tests for registration pass in isolation but fail when DB is under constraint pressure

**Phase to address:** Phase 1 (Authentication foundation) — must be solved before any other feature is built on top of users.

---

### Pitfall 2: Domain Events Dispatched Before Transaction Commits (Cache Invalidation Fires on Rolled-Back Data)

**What goes wrong:**
Domain events (e.g., `PostPublishedEvent`) are dispatched inside the MediatR handler, which triggers `IRedisCacheService.RemoveByPatternAsync()`. If `SaveChangesAsync()` then fails (constraint violation, transient DB error), the cache has been invalidated but the database write was rolled back. The next read re-populates the cache from the database, which still has the old data — so the cache is "correct" by accident. But the inverse scenario is worse: if events are dispatched *after* `SaveChanges()` but the event handler itself throws, the DB is committed but cache is stale with no retry mechanism.

**Why it happens:**
The ADR-005 documentation correctly specifies that Domain Events are dispatched "after `SaveChanges()` succeeds via MediatR `INotificationHandler`", but the implementation detail of what happens when an event handler fails is left unaddressed. Without an outbox or retry mechanism, a transient Redis timeout will silently leave stale cache for up to the TTL duration.

**How to avoid:**
For Phase 1, accept eventual consistency: dispatch events post-commit, log failures, and rely on TTL expiration to self-heal. For Phase 2+, implement the Outbox pattern: persist domain events as rows in an `outbox_messages` table within the same transaction as the aggregate change, then process asynchronously via a background job. This guarantees at-least-once delivery even if Redis is temporarily unavailable.

Additionally, the ADR-005 pattern-based invalidation using `SCAN + DEL` for wildcards should use Lua scripts (as documented) — but the Lua script must be tested under concurrent load to avoid race conditions where a new cache entry is written between SCAN and DEL.

**Warning signs:**
- Cached post lists showing stale data minutes after a publish/archive action
- Redis errors appearing in logs at publish time with no corresponding cache invalidation
- `PostPublishedEvent` handler failures silently swallowed in fire-and-forget patterns

**Phase to address:** Phase 1 (implement events post-commit + TTL safety net); Phase 2 (add Outbox pattern for reliability).

---

### Pitfall 3: MediatR Pipeline Behavior Registration Order Breaks Authorization

**What goes wrong:**
The ADR-002 pipeline order is: `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior`. If behaviors are registered in the wrong order in `Program.cs` (DI container), a request may be cached *before* authorization runs, meaning the cached response is served to unauthorized users on subsequent requests. Alternatively, a validation error bypassing authorization means the pipeline can be probed for information.

**Why it happens:**
MediatR behaviors execute in the order they are registered with the DI container. This is the opposite of how many developers expect it (last-registered wraps outermost). A developer adding a new behavior or refactoring DI setup may unknowingly reorder registrations. There is no compile-time enforcement of behavior order.

**How to avoid:**
Register all behaviors in a single, clearly-commented block in a dedicated extension method (e.g., `services.AddMediatRBehaviors()`), not scattered across feature modules. Add an architecture test (using `NetArchTest` or custom reflection) that asserts the registration order matches the documented contract. Write a specific integration test: an unauthenticated request for a cached query should return 401, not cached data.

**Warning signs:**
- A previously-unauthorized user can access cached content after another user loaded it
- Validation errors from `ValidationBehavior` exposing information about what valid parameters look like before auth
- New developer adds a behavior by appending to the DI registrations without reading the existing order

**Phase to address:** Phase 1 (Authentication + RBAC) — document and enforce order before any handlers are written.

---

### Pitfall 4: CASL Frontend Authorization as the Only Guard (CVE-2025-29927 Pattern)

**What goes wrong:**
CASL enforces permissions in the React UI — hiding buttons, blocking navigation. But CASL runs entirely in the browser. If the API (`blog-api`) does not *also* enforce the same permissions independently, a user can craft direct HTTP requests to perform actions the UI hides (e.g., publish a post as an Author role, delete another user's comment).

A related, confirmed 2025 vulnerability (CVE-2025-29927) shows that Next.js middleware-based authorization can be bypassed by spoofing an internal header, underscoring that frontend auth cannot be the sole layer.

**Why it happens:**
Developers build the admin UI with CASL + PermissionGate and see it "working". They defer API-level enforcement as "we'll add it later". The project already has a 3-layer RBAC plan (ADR-004), but time pressure during implementation leads to skipping the `AuthorizationBehavior` or populating it with placeholder `return true`.

**How to avoid:**
Never ship a command handler without a passing authorization test. Write a test for every command that asserts a user with insufficient role receives `ForbiddenException`. Implement `AuthorizationBehavior` as a blocking behavior that throws if no `IAuthorizationRequirement` handler has approved the request. The `AuthorizationBehavior` must run in the MediatR pipeline, not just as a controller attribute.

For CASL, treat it as UX only — it improves the user experience by hiding inaccessible actions, but it is not a security control. Sync permission definitions between BE (policy names) and FE (`permissions.ts`) via a shared contract in `shared-contracts`.

**Warning signs:**
- Command handlers that lack unit tests asserting unauthorized access returns `ForbiddenException`
- CASL is the only place where a permission is enforced (no corresponding API policy)
- API integration tests checking authorization are absent or minimal

**Phase to address:** Phase 1 (Auth + RBAC) — must be established for every role before any content management features are added.

---

### Pitfall 5: ISR Cache Serving Stale Content After Post Publish (Two-Layer Cache Problem)

**What goes wrong:**
When an Editor publishes a post, the following must happen in order: (1) API invalidates Redis cache, (2) API or a webhook calls Next.js `revalidateTag()` or `revalidatePath()` to purge the ISR cache for `blog-web`. If step 2 is missing or fails, `blog-web` continues to serve the old SSG-generated page for up to the ISR revalidation interval. Readers see a post listed in tag pages but get a 404 on the detail page, or vice versa. This creates a two-layer stale cache problem: Redis (server) and Next.js ISR (CDN/edge).

**Why it happens:**
Backend developers implement Redis cache invalidation and consider caching "done". The frontend ISR layer is separate and requires an explicit call to `revalidatePath('/posts/[slug]')` or `revalidateTag('posts')` triggered by a webhook or API endpoint that Next.js exposes. This cross-system coordination is easy to forget, especially when `blog-api` and `blog-web` are deployed independently.

**How to avoid:**
Create a `RevalidationService` in `blog-api` that fires HTTP calls to `blog-web`'s `/api/revalidate` endpoint (protected by a shared secret) after any `PostPublishedEvent`, `PostUpdatedEvent`, or `PostArchivedEvent`. Add this as a named `INotificationHandler` for each domain event, executed after cache invalidation. Write an E2E test (Playwright) that publishes a post via the admin API and asserts the public blog page reflects the new content within the ISR revalidation window.

**Warning signs:**
- Blog-web ISR `revalidate` interval is long (e.g., 3600 seconds) with no on-demand invalidation
- Post published in admin but not visible on public site for minutes/hours
- No API endpoint in `blog-web` for receiving revalidation triggers

**Phase to address:** Phase 2 (Post lifecycle + public reader) — when SSG/ISR is first implemented.

---

### Pitfall 6: Tiptap JSON Stored But `body_html` Not Sanitized Before Storage

**What goes wrong:**
The schema stores both `body_json` (ProseMirror JSON) and `body_html` (pre-rendered HTML) in `post_contents`. If `body_html` is generated server-side by rendering the Tiptap JSON without sanitization, or if it is accepted directly from the client, a stored XSS payload can be injected. The Tiptap link extension has a documented XSS vulnerability where `javascript:` URIs in `href` attributes execute when rendered.

**Why it happens:**
The rendering pipeline runs Tiptap's server-side `generateHTML()` from JSON, which produces HTML that looks safe because it came from a "schema-validated" JSON document. Developers trust the JSON format and skip the sanitization step for `body_html`. On `blog-web`, the `body_html` is rendered using `dangerouslySetInnerHTML` or equivalent, executing the stored XSS.

**How to avoid:**
The `body_html` column must *only* be populated server-side on `blog-api` using Tiptap's static renderer, never accepted from the client. After server-side rendering, pass `body_html` through a sanitization step (e.g., `HtmlSanitizer` NuGet package, or `Ganss.Xss`) before storing. On `blog-web`, additionally run DOMPurify as a defense-in-depth measure when rendering `body_html`. The `body_json` column should be validated against the Tiptap document schema before any rendering.

**Warning signs:**
- `body_html` is populated from a field in the client's POST body
- The API contract accepts `body_html` as an input field rather than computing it
- No sanitization step between `generateHTML()` and database write
- `@tiptap/html` or static renderer output used directly in `dangerouslySetInnerHTML` without sanitization

**Phase to address:** Phase 2 (Rich text editing + post creation) — must be in place before any content can be saved.

---

### Pitfall 7: PostgreSQL FTS Index Built on Stale `body_html` Column Instead of Content

**What goes wrong:**
ADR-009 defines the FTS index as `to_tsvector('vietnamese', title || ' ' || COALESCE(excerpt, ''))` — indexed on `posts.title` and `posts.excerpt`, not on `post_contents.body_html` or `body_json`. This is correct. However, a common implementation mistake is building the search index on `post_contents.body_html`, which contains HTML tags in the indexed text (e.g., `<p>`, `<strong>`), polluting search results with HTML noise. Vietnamese `unaccent` configuration also needs a custom rules file because the default PostgreSQL unaccent dictionary does not cover all Vietnamese diacritics, and duplicate "TO" mapping warnings during migration indicate incomplete coverage.

**Why it happens:**
Developers assume the full post body should be searchable and add `body_html` or `body_json::text` to the FTS expression. HTML tags like `<p>` become searchable tokens. For Vietnamese diacritics, the developer uses `CREATE EXTENSION unaccent` without verifying the rules file covers all Vietnamese tonal marks.

**How to avoid:**
Keep the FTS index on `title || ' ' || excerpt` as specified. For full-body search (Phase 3), use Meilisearch (ADR-009) which handles Vietnamese tokenization natively. For Phase 1, add a test that searches for a term with and without diacritics (e.g., "phat trien" should match "phát triển") to validate the `unaccent` configuration. Source the Vietnamese unaccent rules file from a community-maintained resource (e.g., the `vinh0604/postgres-unaccent-vi` gist).

**Warning signs:**
- FTS returns results with literal `<p>` or `<strong>` in the matched fragment
- Searching "phat trien" returns no results when "phát triển" posts exist
- Migration logs show `WARNING: duplicate "TO" argument` when creating the `unaccent` dictionary
- Search results include Draft or Archived posts (FTS index not filtered by `status = 'Published'`)

**Phase to address:** Phase 2 or Phase 3 (Search feature) — verify configuration with integration tests before release.

---

### Pitfall 8: Nx Affected Builds Broken by Missing `.nx/cache` or Incorrect Project Boundaries

**What goes wrong:**
`nx affected` runs only what changed. If project dependency graph (`project.json` `implicitDependencies`) is not configured to declare that `blog-web` depends on `shared-contracts`, a change in `shared-contracts` will not trigger a rebuild of `blog-web`. The TypeScript types are stale, the build passes in CI, but production `blog-web` uses outdated types from a previous generate cycle. This manifests as runtime type mismatches between the API contract and the frontend.

**Why it happens:**
`@nx-dotnet/core` does not auto-detect dependencies between .NET projects and TypeScript projects. The `gen-types.sh` script generates types from the OpenAPI spec, but Nx has no knowledge of this dependency unless explicitly declared. Developers assume Nx will infer it from import statements, but the generated output is a separate file, not an ES module import chain.

**How to avoid:**
In `project.json` for `blog-web` and `blog-admin`, declare explicit `implicitDependencies: ["shared-contracts"]`. The `gen-types.sh` script should be a named Nx target (`nx run shared-contracts:generate`) with `dependsOn: ["blog-api:build"]`. Add a CI step that checks the generated types are not stale by running `gen-types.sh` and asserting `git diff --exit-code` on the generated files. This forces type regeneration to be committed whenever the OpenAPI spec changes.

**Warning signs:**
- `nx affected --target=build` in CI skips `blog-web` after an API contract change
- Frontend compiles but has TypeScript errors silenced with `// @ts-ignore` or `any`
- Runtime `400 Bad Request` errors caused by field name mismatches between FE and BE
- `shared-contracts` does not appear in `nx graph` as a dependency of `blog-web`

**Phase to address:** Phase 1 (Monorepo scaffold) — configure project graph correctly before any code is written.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip `AuthorizationBehavior` for now, use controller `[Authorize]` only | Faster handler development | Authorization logic scattered; no centralized audit; easy to miss a command | Never — the 3-layer RBAC is a stated requirement (ADR-004) |
| Cache everything with default TTL, no ICacheableQuery discipline | Queries are fast immediately | Sensitive queries (current user, admin lists) return stale data; hard to audit what is cached | Never — ADR-008 requires opt-in |
| Accept `body_html` from client to skip server-side rendering | Saves one server roundtrip | Stored XSS attack surface; sanitization burden shifts to all clients | Never |
| Use `KEYS *` instead of `SCAN` for Redis pattern invalidation | Simpler code | Blocks Redis event loop in production under load | Never in production; acceptable in local dev only |
| Skip `ISearchService` abstraction, call PostgreSQL FTS directly from handlers | Less abstraction | Cannot swap to Meilisearch (Phase 3) without rewriting all search handlers | MVP is acceptable if abstraction is added before Phase 3 |
| Hardcode cache TTL values inline in query records | Faster to write | TTL values scattered; changing strategy requires finding all call sites | Acceptable in Phase 1; centralize in `CacheKeys.cs` before Phase 2 |
| Commit `shared-contracts` generated types without CI freshness check | Simpler CI | API contract drift goes undetected; silent type mismatches in production | Never after Phase 1 scaffold |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| NextAuth v5 ↔ ASP.NET Core JWT | NextAuth generates its own JWT; backend cannot validate it because signing keys differ | Configure NextAuth Credentials provider to call ASP.NET Identity login endpoint; store the ASP.NET-issued JWT in the NextAuth session; forward it as `Authorization: Bearer` to `blog-api` |
| NextAuth v5 + `sameSite` cookie | Setting `sameSite: 'strict'` blocks cross-origin requests from admin subdomain to API | Use `sameSite: 'lax'` in production; `'none'` only if cross-site iframe is required |
| Redis `RemoveByPatternAsync` | Using `KEYS *` to find keys by pattern blocks the Redis event loop | Use `SCAN` cursor iteration + `DEL` in batches, wrapped in a Lua script for atomicity (as per ADR-005) |
| EF Core + `IdentityDbContext` shared transaction | Calling `context.Database.UseTransaction(tx)` after the context has already executed a query fails silently or throws | Open the `NpgsqlConnection` and begin the transaction *before* creating either DbContext; pass the connection into the `DbContextOptions` builder |
| Nx + `@nx-dotnet/core` + OpenAPI TypeScript gen | `nx-dotnet` TypeScript generation uses its own generator; custom `gen-types.sh` is invisible to Nx task graph | Register `gen-types.sh` as an Nx executor target; declare `dependsOn` so it runs after `blog-api:build` |
| Tiptap v3 static renderer in .NET backend | No official .NET Tiptap renderer exists; `generateHTML()` is Node.js only | Either (a) call Node.js renderer via a small sidecar process, or (b) render HTML in `blog-admin` on save (server action) and send pre-rendered HTML to API — ensure HTML is sanitized before storage |
| MinIO ↔ ASP.NET Core image upload | Presigned URLs with default 7-day expiry; cover images stored with public-read ACL but referenced in `body_html` via presigned URLs that expire | Use MinIO bucket policy for public read on the `covers/` prefix; reserve presigned URLs for private content only |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Loading full `post_versions` on every post edit | Admin editor slow to open; `post_versions` table grows unbounded | Load version list (IDs + timestamps only) lazily; paginate version history; never JOIN `post_versions.body_json` in the post list query | ~100 versions per active post |
| N+1 query in paginated post list (fetching tags per post) | Post list endpoint gets slower as tag assignments grow; DB query count scales with page size | Use a single query with LEFT JOIN on `post_tags` + `tags`, or use EF Core `Include()` correctly | Page size > 10 posts with many tags |
| ISR `revalidate: 3600` with no on-demand invalidation | Published content takes up to 1 hour to appear on public site | Implement on-demand revalidation via `revalidatePath`/`revalidateTag` webhook; set a reasonable ISR interval (60–300s) as fallback | First post publish after launch |
| FTS GIN index on full `body_html` column | GIN index size > table data size; index build on migration takes minutes | Keep FTS index on `title + excerpt` only (as designed); use Meilisearch for full-body search in Phase 3 | Table > 10,000 posts |
| `post_versions` unbounded growth | Storage cost escalates; backup/restore time grows | Add a soft limit (e.g., keep last 50 versions per post) enforced by a domain rule on `Post` aggregate | High-frequency editing (autosave) without a version cap |
| Slug lookup without covering index | `SELECT * FROM posts WHERE slug = ?` does table scan | The `CONSTRAINT uq_posts_slug UNIQUE (slug)` already creates an index — verify EF Core does not drop and recreate this constraint on each migration | First migration if constraint is dropped accidentally |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Trusting `body_html` from client | Stored XSS: malicious HTML saved to DB, served to all readers | `body_html` computed server-side only from `body_json`; sanitize with `Ganss.Xss` before storage; DOMPurify on `blog-web` as defense-in-depth |
| Using Next.js middleware as sole authorization gate | CVE-2025-29927: `x-middleware-subrequest` header spoof bypasses middleware auth | All authorization enforced by `blog-api`; Next.js middleware is a UX-only routing layer, not a security boundary |
| Hardcoded `NEXTAUTH_SECRET` or JWT signing key in `appsettings.json` | Key exposure in source control; token forgery | Use ASP.NET Data Protection for key management; secrets via environment variables or Kubernetes secrets; never commit secrets |
| User ban not invalidating active JWTs | Banned user's existing token remains valid until expiry | On ban, increment a `security_stamp` in `AspNetUsers` (ASP.NET Identity feature); validate `security_stamp` claim on each JWT validation to force re-authentication |
| Comments accepted without content length enforcement at API level | Comment body stored as `TEXT` but DB constraint `CHECK (length(content) <= 5000)` relies on DB enforcement only | Enforce max length in FluentValidation + API model binding, not only at DB level; defense-in-depth |
| Cover image URL not validated before storage | Open redirect or SSRF via crafted `cover_image_url` pointing to internal services | Validate that `cover_image_url` is a URL from the MinIO host (allowlist); reject external URLs in `cover_image_url` if not using a CDN proxy |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Tiptap editor state lost on browser refresh during draft editing | Authors lose unsaved work | Implement `localStorage` autosave in `blog-admin` with debounce (1–2s); save draft to API on a 30s interval; restore on page load |
| Slug auto-generated from title is not editable | Authors cannot control canonical URL; Vietnamese titles produce ugly slugs with encoded characters | Generate a Latin-ASCII slug from the Vietnamese title using `Unidecode` or equivalent; allow manual slug override in the editor with uniqueness validation |
| Post version history inaccessible from editor | Authors cannot review what changed between versions | Show version list in sidebar with diff view (or at minimum "restore" button); do not hide this behind admin-only access — Authors should see their own history |
| Search returning results without Vietnamese diacritic normalization | User searches "viet nam" and gets no results for "Việt Nam" posts | Validate `unaccent` configuration handles all Vietnamese tonal combinations before launch; add search integration test with Vietnamese fixtures |
| Pagination without cursor-based pagination for high-volume comment threads | Comments load slowly on popular posts; offset pagination degrades as post count grows | Use cursor-based pagination for comment lists (keyed on `created_at + id`); offset pagination acceptable for post lists in Phase 1 |

---

## "Looks Done But Isn't" Checklist

- [ ] **Authentication:** JWT refresh token rotation implemented — verify that using a refresh token invalidates the old token (prevent token replay attacks)
- [ ] **RBAC:** All 4 roles tested against all endpoints — verify an Author cannot call the publish endpoint; verify a Reader cannot post comments when not authenticated
- [ ] **Post Publish:** `blog-web` ISR cache invalidated — verify published post appears on public site within the revalidation window, not just in admin
- [ ] **Ban User:** Active JWT tokens invalidated — verify banned user's existing token is rejected on next request (not just blocked from new logins)
- [ ] **Comment Moderation:** Pending comments not visible to non-moderators — verify `is_approved = FALSE` comments are filtered from public API responses
- [ ] **Search:** Vietnamese diacritics normalized — verify "phat trien" query returns posts containing "phát triển"
- [ ] **Image Upload:** Pre-rendered `body_html` references stable MinIO URLs — verify cover image URL still works 30 days after upload (not an expiring presigned URL)
- [ ] **Post Versioning:** Version count bounded — verify that 100+ saves of a single post do not create 100+ `post_versions` rows without a cap
- [ ] **Nx Affected:** Type generation in CI detects API contract changes — verify that changing an API response field triggers a `shared-contracts` rebuild and downstream `blog-web`/`blog-admin` affected builds
- [ ] **Cross-context Transaction:** Register rollback tested — verify that a failure during domain `User` creation does not leave an orphaned `AspNetUsers` row

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Orphaned IdentityUser records discovered in production | MEDIUM | Write a one-time migration script: find `AspNetUsers.Id` values not in `users.id`; create corresponding `User` domain records with `role = 'Reader'`, `is_active = false`; notify affected users via email; investigate root cause via logs |
| Stale ISR cache after botched deploy | LOW | Call `revalidatePath('/')` and `revalidatePath('/posts/[slug]', 'page')` via the admin revalidation endpoint; or restart `blog-web` pods to clear in-memory ISR cache |
| Redis cache poisoned with HTML-injected content | HIGH | Flush the affected Redis key pattern; redeploy `blog-api` with sanitization fix; audit `post_contents.body_html` rows for XSS payloads; re-render all `body_html` from `body_json` server-side |
| FTS GIN index not covering Vietnamese diacritics | MEDIUM | Add correct unaccent rules file to PostgreSQL `tsearch_data` directory; `DROP INDEX idx_posts_fts; CREATE INDEX idx_posts_fts ...;` — rebuilds online in PostgreSQL 18 with `CONCURRENTLY` |
| MediatR behavior order wrong, unauthorized data in cache | HIGH | Flush Redis entirely; fix behavior registration order; add regression test; audit logs for unauthorized cache reads |
| `shared-contracts` types diverged from API in production | HIGH | Run `gen-types.sh` immediately; redeploy `blog-web` and `blog-admin`; add CI gate to prevent future divergence |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Orphaned IdentityUser (cross-context transaction) | Phase 1: Auth foundation | Integration test: force failure at step 2 of Register, assert no `AspNetUsers` orphan |
| Domain Events dispatched before commit | Phase 1: Event infrastructure | Unit test: mock SaveChanges failure, assert no event handler called |
| MediatR behavior registration order | Phase 1: CQRS pipeline scaffold | Architecture test asserting behavior registration order; integration test asserting 401 before cache |
| CASL-only authorization (no API enforcement) | Phase 1: RBAC | Integration test per role × endpoint matrix |
| ISR two-layer cache staleness | Phase 2: Post lifecycle + public reader | E2E test: publish post, assert visible on `blog-web` within revalidation window |
| Tiptap XSS via stored `body_html` | Phase 2: Rich text editing | Security test: submit `javascript:alert(1)` as link href in JSON, assert sanitized in stored HTML |
| FTS index on HTML content / missing Vietnamese unaccent | Phase 2 or 3: Search | Integration test: Vietnamese diacritic roundtrip search |
| Nx project graph missing `shared-contracts` dependency | Phase 1: Monorepo scaffold | CI: run `gen-types.sh`, assert `git diff --exit-code` |
| Unbounded `post_versions` growth | Phase 2: Post lifecycle | Domain rule unit test: save more than N versions, assert oldest are pruned or count is bounded |
| Banner user active JWT not invalidated | Phase 1 or 2: User management | Integration test: ban user, assert their valid JWT is rejected on next request |

---

## Sources

- [ADR-006, ADR-007 (project docs)](docs/blog-platform/03-architecture-decisions.md) — IdentityUser / Domain User split and shared connection transaction strategy
- [ADR-005, ADR-008 (project docs)](docs/blog-platform/03-architecture-decisions.md) — Cache-aside, opt-in ICacheableQuery, pattern invalidation
- [Microsoft: Domain Events Design and Implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) — dispatch timing, outbox pattern
- [Tiptap XSS in link extension — GitHub Issue #3673](https://github.com/ueberdosis/tiptap/issues/3673) — `javascript:` URI in href
- [ProseMirror DOMSerializer XSS — discuss.prosemirror.net](https://discuss.prosemirror.net/t/heads-up-xss-risk-in-domserializer/6572) — attribute value injection
- [CVE-2025-29927: Next.js Middleware Authorization Bypass](https://projectdiscovery.io/blog/nextjs-middleware-authorization-bypass) — middleware header spoof
- [Next.js ISR on-demand revalidation — official docs](https://nextjs.org/docs/app/guides/incremental-static-regeneration) — revalidateTag, revalidatePath
- [Redis cache invalidation race conditions — Medium, Dec 2025](https://medium.com/@rachoork/critical-caching-patterns-preventing-catastrophic-failures-at-scale-ddc75ac0e863) — SCAN vs KEYS, Lua atomic invalidation
- [Vietnamese FTS PostgreSQL — blog.tuando.me](https://blog.tuando.me/vietnamese-full-text-search-on-postgresql) — custom configuration, unaccent rules
- [NextAuth v5 session persistence issues — Clerk](https://clerk.com/articles/nextjs-session-management-solving-nextauth-persistence-issues) — NEXTAUTH_SECRET, cookie configuration
- [Nx + @nx-dotnet/core plugin](https://github.com/nx-dotnet/nx-dotnet) — project graph, TypeScript generation
- [CodeOpinion: Aggregates in DDD — Model Rules Not Relationships](https://codeopinion.com/aggregates-in-ddd-model-rules-not-relationships/) — aggregate boundary decisions
- [MediatR pipeline behavior ordering — codewithmukesh.com](https://codewithmukesh.com/blog/mediatr-pipeline-behaviour/) — registration order = execution order

---
*Pitfalls research for: ASP.NET Core 10 + Next.js 16.1 + PostgreSQL 18 + Nx monorepo + Clean Architecture + DDD blog platform*
*Researched: 2026-03-12*
