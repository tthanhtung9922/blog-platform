---
name: review-pull-request
description: >
  Use this agent for comprehensive code review of pull requests or staged changes.
  Triggers on: "review PR", "review changes", "code review", "check my PR",
  "review my code", "what did I miss", "pre-merge check", "review before merge".
  Covers architecture compliance, RBAC consistency, cache invalidation completeness,
  naming conventions, test coverage, and security. Use before merging to dev or main.
tools:
  - Read
  - Glob
  - Grep
  - Bash
---

# Review Pull Request

## Purpose
Performs a comprehensive code review against all project conventions, catching issues that individual focused reviews might miss. Designed as a pre-merge gate that checks cross-cutting concerns spanning backend, frontend, infrastructure, and documentation.

## Scope & Boundaries
**In scope**: All changed files in a PR or staged changes. Architecture, RBAC, caching, naming, testing, security, API contract consistency, documentation.
**Out of scope**: Deep database query optimization → `optimize-database`. Systematic debugging → `debug-backend`. Feature planning → `plan-feature`.

## Project Context

This is a monorepo with 3 apps and 2 shared libraries:
- `apps/blog-api/` — ASP.NET Core 10 (Clean Architecture + DDD)
- `apps/blog-web/` — Next.js 16.1 (public reader, SSG/ISR)
- `apps/blog-admin/` — Next.js 16.1 (CMS dashboard)
- `libs/shared-contracts/` — OpenAPI-generated TypeScript types
- `libs/shared-ui/` — Shared React components

Branch strategy: `feat/*` → PR → `dev` → PR → `main`
Commit convention: Conventional Commits (`feat`, `fix`, `refactor`, `chore`, etc.)

## Workflow

### 1. Gather changed files

Run `git diff` to understand what changed:
```bash
# For PR review — diff against base branch
git diff --name-only dev...HEAD
git diff --stat dev...HEAD

# For staged changes
git diff --name-only --cached
```

Categorize changes by area: backend, frontend (which app?), infrastructure, docs, tests.

### 2. Architecture Review (Backend changes)

If any `apps/blog-api/` files changed, check:

**Layer dependencies** (CRITICAL):
- `Blog.Domain/` must not import `Blog.Application`, `Blog.Infrastructure`, or `Blog.API`
- `Blog.Application/` must not import `Blog.Infrastructure` or `Blog.API`
- Search for forbidden `using` statements in changed files

**DDD patterns**:
- New entities placed in correct aggregate folder
- Aggregate roots enforce invariants (no anemic domain models)
- Cross-aggregate references use ID only
- Domain Events raised for significant state changes

**CQRS conventions**:
- Commands in `Features/{Entity}/Commands/{Action}/`
- Queries in `Features/{Entity}/Queries/{Action}/`
- Each command/query has: Command/Query class, Handler class, Validator class
- MediatR pipeline order preserved: Validation → Logging → Authorization → Caching

### 3. RBAC Consistency Check

If any authorization-related code changed, verify all 3 layers are in sync:

| Layer | Where | What to check |
|-------|-------|--------------|
| API | `Blog.API/Controllers/` | `[Authorize(Policy = "...")]` attributes |
| Application | `Blog.Application/Behaviors/AuthorizationBehavior.cs` or command-level | Authorization checks in handlers |
| Frontend | `apps/blog-admin/src/lib/permissions/` | CASL ability definitions match backend |

**Permission matrix** (from docs):
| Permission | Admin | Editor | Author | Reader |
|---|:---:|:---:|:---:|:---:|
| Read published posts | Yes | Yes | Yes | Yes |
| Create/edit own posts | Yes | Yes | Yes | No |
| Publish posts | Yes | Yes | No | No |
| Delete others' posts | Yes | No | No | No |
| Moderate comments | Yes | Yes | No | No |
| Manage users & roles | Yes | No | No | No |
| View analytics | Yes | Yes | Own only | No |
| System settings | Yes | No | No | No |

### 4. Cache Invalidation Review

If any domain entity changes are introduced, verify:

- New queries that should be cached implement `ICacheableQuery` with correct cache key from `CacheKeys.cs`
- New commands that modify data have corresponding Domain Event → cache invalidation mapping
- `GetCurrentUser`, `GetUserList` NEVER implement `ICacheableQuery` (ADR-008)

**Cache invalidation map** (must be complete):
| Domain Event | Cache keys invalidated |
|---|---|
| `PostPublishedEvent` | `post:slug:*`, `post:id:*`, `post-list:*` |
| `PostUpdatedEvent` | `post:slug:{slug}`, `post:id:{id}` |
| `PostArchivedEvent` | Same as PostPublished |
| `CommentAddedEvent` | `comments:post:{postId}:*` |
| `CommentDeletedEvent` | `comments:post:{postId}:*` |
| `UserProfileUpdatedEvent` | `user:profile:{username}` |

### 5. Frontend Review

If `apps/blog-web/` or `apps/blog-admin/` files changed:

- **Tailwind CSS v4**: CSS-first config in `globals.css` — no `tailwind.config.ts` file
- **App Router**: Proper use of route groups `(public)`, `(auth)`, `(dashboard)`
- **blog-web**: SSG/ISR pages use `generateStaticParams` and `revalidate`
- **blog-admin**: Protected routes use `ProtectedRoute` and `PermissionGate` components
- **Tiptap content**: Rendered via `@tiptap/react` EditorContent in read-only mode (Option B), HTML sanitized with DOMPurify if using Option A
- **Types**: Use generated types from `libs/shared-contracts/`, not hand-written duplicates

### 6. API Contract Consistency

If API endpoints changed:
- Controller route matches OpenAPI spec in `docs/blog-platform/09-api-contract--openapi-specification.md`
- Response DTOs match documented schemas
- Error responses use RFC 9457 ProblemDetails format
- `scripts/gen-types.sh` needs to be re-run if schemas changed

### 7. Test Coverage

Check that changes have appropriate test coverage:

| Change type | Expected tests |
|---|---|
| New Domain entity | Unit tests in `Blog.UnitTests/Domain/` |
| New Command/Query handler | Unit tests in `Blog.UnitTests/Application/` |
| New API endpoint | Integration test in `Blog.IntegrationTests/` (Testcontainers) |
| Architecture rules | Verified by `Blog.ArchTests/` (NetArchTest) |
| New user flow | E2E test in Playwright (for critical paths) |

### 8. Security Quick Check

- No secrets/credentials in code (connection strings, API keys)
- SQL queries parameterized (no string concatenation)
- User input validated (FluentValidation for backend, zod/CASL for frontend)
- `DOMPurify` used when rendering user-generated HTML
- JWT tokens not logged or exposed in responses
- `KEYS *` never used in Redis (use `SCAN` instead — ADR-005)

### 9. Naming & Convention Check

- **Commits**: Follow Conventional Commits format: `<type>[scope]: <description>`
- **C# files**: PascalCase classes, `I` prefix for interfaces
- **Migration names**: `Add{Entity}`, `Add{Column}To{Table}`, etc. (see doc 11)
- **Cache keys**: Follow pattern in `CacheKeys.cs`
- **Frontend files**: PascalCase for components, camelCase for utilities

### 10. Generate Review Summary

Structure the review as:

```markdown
## PR Review Summary

### Critical Issues (must fix)
- [file:line] Issue description → suggested fix

### Warnings (should fix)
- [file:line] Issue description → suggested fix

### Suggestions (nice to have)
- [file:line] Suggestion

### Checklist
- [ ] Architecture: layer dependencies clean
- [ ] RBAC: 3 layers consistent
- [ ] Cache: invalidation complete for new events
- [ ] Tests: adequate coverage for changes
- [ ] Security: no obvious vulnerabilities
- [ ] API: matches OpenAPI contract
- [ ] Naming: follows conventions
```

## Project-Specific Conventions

- **ADR-006**: IdentityUser ≠ Domain User. Shared GUID, no inheritance, no navigation properties.
- **ADR-007**: Cross-context operations (Register, Ban) use shared DbConnection via IUnitOfWork.
- **ADR-008**: Caching is opt-in via ICacheableQuery. Never cache GetCurrentUser.
- **Pipeline order**: ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior (FIXED).
- **Tailwind v4**: CSS-first config. No tailwind.config.ts.

## Output Checklist
Before finishing, verify:
- [ ] All changed files reviewed
- [ ] Layer dependencies checked for backend changes
- [ ] RBAC consistency verified if auth code changed
- [ ] Cache invalidation complete if domain events added/changed
- [ ] Test coverage adequate
- [ ] No security issues
- [ ] Naming conventions followed

## Related Agents
- `review-architecture` — deeper architecture-specific review
- `audit-rbac` — deep-dive into 3-layer permission consistency
- `review-migration` — if PR includes EF Core migrations
- `review-frontend` — deeper frontend-specific review
- `review-infrastructure` — if PR includes K8s/Docker/CI changes
