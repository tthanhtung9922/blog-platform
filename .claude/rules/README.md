# Project Rules Index

## File Map

| File | Concerns Covered | Key Rules |
|---|---|---|
| `CLAUDE.md` | Architecture, Security, Domain Language, Layout | Layer direction, Identity/User separation, RBAC 3-layer, cache opt-in, pipeline order |
| `.claude/rules/backend-architecture.md` | Clean Arch, DDD, CQRS, MediatR | Aggregate placement, CQRS folders, command/query naming, domain events, cross-context transactions |
| `.claude/rules/caching.md` | Redis cache-aside, opt-in, invalidation | ICacheableQuery, cache key convention, event-driven invalidation, no KEYS command |
| `.claude/rules/security-auth.md` | RBAC, JWT, roles, CASL | 3-layer enforcement, role hierarchy, permission sync, CASL >= 6.8.0 |
| `.claude/rules/database.md` | Schema, migrations, PostgreSQL | snake_case, UUID PKs, TIMESTAMPTZ, migration naming, backward compatibility |
| `.claude/rules/frontend.md` | Next.js, Tailwind v4, Tiptap, CASL | Two separate apps, CSS-first Tailwind, ProseMirror JSON, DOMPurify, OpenAPI types |
| `.claude/rules/api-design.md` | REST, OpenAPI, errors, pagination | /api/v1/ prefix, ProblemDetails, 422 validation, consistent pagination envelope |
| `.claude/rules/git-workflow.md` | Commits, branches, PRs, CI | Conventional Commits, branch prefixes, PRs target dev, type regeneration |
| `.claude/rules/testing.md` | xUnit, Testcontainers, Playwright, ArchTests | Real DB in integration tests, NetArchTest layer enforcement, k6 load tests |

## Severity Summary

### NEVER (Hard Prohibitions)
- Domain layer references Infrastructure — `backend-architecture.md`
- User extends IdentityUser — `backend-architecture.md`
- Redis KEYS * in production — `caching.md`
- Cache queries without ICacheableQuery — `caching.md`
- Invalidate cache in command handlers — `caching.md`
- Cache GetCurrentUser or GetUserList — `caching.md`
- Hardcode secrets in source code — `security-auth.md`
- Trust frontend CASL alone for security — `security-auth.md`
- tailwind.config.ts file exists — `frontend.md`
- Render Tiptap as Markdown/MDX — `frontend.md`
- Unsanitized dangerouslySetInnerHTML — `frontend.md`
- Apply migrations directly to production — `database.md`
- Skip migration Down() method — `database.md`
- Use TIMESTAMP without time zone — `database.md`
- Expose stack traces in API responses — `api-design.md`
- Business logic in controllers — `api-design.md`
- Push directly to main — `git-workflow.md`
- Mock database in integration tests — `testing.md`

### Critical MUSTs
- Layer dependency: Domain → Application → Infrastructure → Presentation — `CLAUDE.md`
- IdentityUser and User are separate models — `CLAUDE.md`
- RBAC enforced at 3 layers — `CLAUDE.md`
- Cache opt-in via ICacheableQuery — `CLAUDE.md`
- MediatR pipeline order is fixed — `CLAUDE.md`
- Cross-context ops use shared DbConnection — `CLAUDE.md`
- Tiptap content is ProseMirror JSON — `CLAUDE.md`
- API uses ProblemDetails for errors — `api-design.md`
- Migrations have working Down() — `database.md`
- Destructive migrations need 2 reviewers — `database.md`
- Architecture tests enforce layer boundaries — `testing.md`

## How Rules Differ from Agents and Skills
- **Rules** are always-on constraints — they apply automatically in every interaction.
- **Agents** are invoked for specific review/analysis tasks (e.g., `audit-rbac`, `review-migration`).
- **Skills** are invoked for specific workflows (e.g., `/add-domain-entity`, `/run-ef-migration`).
