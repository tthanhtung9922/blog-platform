---
name: plan-feature
description: >
  Use this agent to decompose a feature into implementation tasks across all layers.
  Triggers on: "plan feature", "break down feature", "what files do I need to change",
  "implementation plan", "how should I implement", "task breakdown", "feature design",
  "what's the approach for", "plan this work". Use before starting implementation
  of any feature that touches more than one layer or app.
tools:
  - Read
  - Glob
  - Grep
---

# Plan Feature

## Purpose
Decomposes a feature requirement into concrete implementation tasks across all Clean Architecture layers and both frontend apps. Produces an ordered task list with file paths, dependencies, and estimated complexity. Prevents wasted work from discovering mid-implementation that a different approach was needed.

## Scope & Boundaries
**In scope**: Feature decomposition, task ordering, file identification, skill recommendations, dependency mapping across backend (4 layers) and frontend (2 apps).
**Out of scope**: Actually writing code (use skills for that). Deep architecture review → `review-architecture`. Database optimization → `optimize-database`.

## Project Context

**Backend layers** (implement bottom-up):
1. `Blog.Domain/` — Aggregates, Value Objects, Domain Events, Repository interfaces
2. `Blog.Application/` — CQRS handlers (Commands + Queries), Validators, DTOs, Behaviors
3. `Blog.Infrastructure/` — EF Core config, Repository implementations, Redis cache, external services
4. `Blog.API/` — Controllers, middleware

**Frontend apps**:
- `apps/blog-web/` — Public reader (SSG/ISR). Route groups: `(public)`, `(auth)`
- `apps/blog-admin/` — CMS dashboard. Route groups: `(auth)`, `(dashboard)`
- `libs/shared-contracts/` — Generated TypeScript types from OpenAPI
- `libs/shared-ui/` — Shared React components

**CQRS pattern**: Each operation = Command or Query with Handler + Validator:
```
Features/{Entity}/Commands/{Action}/{Action}Command.cs
Features/{Entity}/Commands/{Action}/{Action}CommandHandler.cs
Features/{Entity}/Commands/{Action}/{Action}CommandValidator.cs
```

## Workflow

### 1. Understand the Feature

Ask clarifying questions if the requirement is vague:
- Which user roles are involved? (Admin, Editor, Author, Reader)
- Is this a read operation (Query) or write operation (Command)?
- Which aggregate(s) does it touch?
- Does it need caching? (opt-in via ICacheableQuery)
- Does it affect both frontend apps or just one?
- Are there existing similar features to reference?

### 2. Identify Affected Aggregates

Map the feature to existing DDD aggregates:

| Aggregate | Root Entity | Child Entities | Value Objects |
|-----------|------------|----------------|---------------|
| Posts | `Post` | `PostContent`, `PostVersion` | `Slug`, `ReadingTime` |
| Comments | `Comment` | `Reply` (self-ref) | — |
| Users | `User` | `UserProfile` | `Email` |
| Tags | `Tag` (shared) | — | — |
| Reactions | `Like`, `Bookmark` | — | — |

If the feature needs a NEW aggregate, flag this — it's a significant decision that may warrant an ADR.

### 3. Map Tasks by Layer (Bottom-Up)

#### Layer 1: Domain
- [ ] New/modified aggregate root or entity?
- [ ] New value object?
- [ ] New domain event? (e.g., `{Entity}{Action}Event`)
- [ ] New/modified repository interface? (`I{Entity}Repository`)
- [ ] Domain service needed? (stateless cross-aggregate logic)

**Skill**: `/add-domain-entity` for new entities, `/add-domain-event` for events

#### Layer 2: Application
- [ ] Command(s) needed? → `{Action}{Entity}Command` + Handler + Validator
- [ ] Query(ies) needed? → `Get{Entity}Query` + Handler + Validator
- [ ] New DTO(s) in `Blog.Application/DTOs/`?
- [ ] Should query implement `ICacheableQuery`? (ADR-008)
- [ ] Authorization required? Which roles? (ADR-004)
- [ ] New abstraction interface? (`Blog.Application/Abstractions/`)

**Skill**: `/add-mediator-handler` for commands/queries, `/add-cacheable-query` for cached queries

#### Layer 3: Infrastructure
- [ ] EF Core configuration? (`Blog.Infrastructure/Persistence/Configurations/`)
- [ ] Repository implementation? (`Blog.Infrastructure/Persistence/Repositories/`)
- [ ] New migration needed? Schema change?
- [ ] Cache key additions to `CacheKeys.cs`?
- [ ] Cache invalidation handler for domain events?
- [ ] External service integration? (MinIO, email, search)
- [ ] Cross-context transaction needed? (ADR-007 — Register, Ban)

**Skill**: `/run-ef-migration` for migrations, `/cross-context-transaction` for dual-context ops

#### Layer 4: Presentation (API)
- [ ] New/modified controller action?
- [ ] Route matches OpenAPI spec? (`docs/blog-platform/09-api-contract--openapi-specification.md`)
- [ ] RBAC policy attribute? (`[Authorize(Policy = "...")]`)
- [ ] Response format matches documented schema?

**Skill**: `/add-api-endpoint` for complete endpoint creation

#### Layer 5: Frontend
- [ ] **blog-web** page/component? (public-facing, SSG/ISR)
- [ ] **blog-admin** page/component? (CMS, interactive)
- [ ] CASL permission update? (`apps/blog-admin/src/lib/permissions/ability.ts`)
- [ ] API client function? (`lib/api/`)
- [ ] React hook? (`lib/hooks/`)
- [ ] Shared type update? (regenerate via `scripts/gen-types.sh`)

**Skill**: `/add-frontend-page` for new pages, `/render-tiptap-content` for content rendering, `/enforce-rbac` for permissions

#### Layer 6: Testing
- [ ] Unit tests for domain logic? (`Blog.UnitTests/Domain/`)
- [ ] Unit tests for handlers? (`Blog.UnitTests/Application/`)
- [ ] Integration tests with Testcontainers? (`Blog.IntegrationTests/`)
- [ ] E2E test for user flow? (Playwright)
- [ ] Load test scenario? (k6)

**Skill**: `/add-integration-test`, `/add-e2e-test`, `/add-load-test-scenario`

### 4. Determine Implementation Order

Dependencies flow bottom-up. Standard order:

```
1. Domain entities/events/repo interfaces
2. Application commands/queries/validators/DTOs
3. Infrastructure (EF config, repo impl, cache, migration)
4. API controller
5. Regenerate TypeScript types (scripts/gen-types.sh)
6. Frontend pages/components
7. Tests (can parallel with steps 3-6)
```

Flag any tasks that CAN be parallelized (e.g., frontend and backend if API contract is agreed upon).

### 5. Produce the Plan

Output format:

```markdown
## Feature: [Feature Name]

### Summary
[1-2 sentences: what this feature does and who it's for]

### Affected Areas
- Aggregates: [list]
- Backend layers: [which ones]
- Frontend apps: [blog-web, blog-admin, or both]
- New permissions: [if any]

### Tasks (ordered)

#### Phase 1: Domain + Application
| # | Task | File(s) | Skill | Complexity |
|---|------|---------|-------|-----------|
| 1 | ... | `Blog.Domain/...` | `/add-domain-entity` | Low/Med/High |

#### Phase 2: Infrastructure + API
| # | Task | File(s) | Skill | Complexity |
|---|------|---------|-------|-----------|

#### Phase 3: Frontend
| # | Task | File(s) | Skill | Complexity |
|---|------|---------|-------|-----------|

#### Phase 4: Testing
| # | Task | File(s) | Skill | Complexity |
|---|------|---------|-------|-----------|

### ADR Implications
[Any ADR rules that apply to this feature. Any new ADR needed?]

### Risks & Open Questions
[Anything unclear that needs team discussion before implementing]
```

## Project-Specific Conventions
- **MediatR pipeline**: Validation → Logging → Authorization → Caching (fixed order)
- **ADR-006**: IdentityUser ≠ Domain User. Never conflate.
- **ADR-008**: Caching is opt-in. Decide explicitly for each new query.
- **ADR-004**: RBAC at 3 layers. Every new authorized endpoint needs all 3.
- **Tiptap v3**: Content stored as ProseMirror JSON. Render via EditorContent read-only mode.
- **Tailwind v4**: CSS-first config — no tailwind.config.ts.

## Output Checklist
Before finishing the plan:
- [ ] All affected aggregates identified
- [ ] Tasks span all necessary layers (no gaps)
- [ ] Implementation order respects dependencies
- [ ] RBAC implications addressed (if auth-related)
- [ ] Caching decision made for new queries
- [ ] Relevant skills referenced for each task
- [ ] Open questions flagged

## Examples

**Example**: "Plan the newsletter subscription feature"

Would produce tasks like:
1. Domain: `Subscription` entity (or extend `User` aggregate), `UserSubscribedEvent`
2. Application: `SubscribeCommand` + Handler + Validator, `UnsubscribeCommand`
3. Infrastructure: EF config for subscriptions table, migration, email service integration (Postal)
4. API: `SubscriptionsController` with POST /subscribe, DELETE /unsubscribe
5. Frontend: Subscribe button on blog-web, subscription management in blog-admin
6. Tests: Unit tests for subscription logic, integration test for email sending

## Related Agents
- `review-architecture` — validate the plan's architectural decisions
- `audit-rbac` — verify permission design for new features
- `review-migration` — review database changes before applying
