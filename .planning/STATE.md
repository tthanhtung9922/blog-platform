---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: planning
stopped_at: Completed 02-infrastructure-application-pipeline/02-02-PLAN.md
last_updated: "2026-03-17T15:37:32.368Z"
last_activity: 2026-03-12 — Roadmap created, 10 phases derived from 54 v1 requirements
progress:
  total_phases: 10
  completed_phases: 1
  total_plans: 8
  completed_plans: 6
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-12)

**Core value:** Readers can discover and read high-quality Vietnamese content instantly; authors can write and publish rich content through a powerful CMS — all on a self-hosted, open-source stack with no vendor lock-in.
**Current focus:** Phase 1 — Monorepo Foundation + Domain Layer

## Current Position

Phase: 1 of 10 (Monorepo Foundation + Domain Layer)
Plan: 0 of TBD in current phase
Status: Ready to plan
Last activity: 2026-03-12 — Roadmap created, 10 phases derived from 54 v1 requirements

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: -
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**
- Last 5 plans: none yet
- Trend: -

*Updated after each plan completion*
| Phase 01-monorepo-foundation-domain-layer P01 | 8min | 3 tasks | 19 files |
| Phase 01-monorepo-foundation-domain-layer P02 | 6 | 2 tasks | 34 files |
| Phase 01-monorepo-foundation-domain-layer P03 | 7 | 2 tasks | 17 files |
| Phase 01-monorepo-foundation-domain-layer P04 | 3 | 1 tasks | 2 files |
| Phase 02-infrastructure-application-pipeline P01 | 4min | 2 tasks | 22 files |
| Phase 02-infrastructure-application-pipeline P02 | 4min | 2 tasks | 13 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: Use `@nx/dotnet` (official, Nx 22+) NOT `@nx-dotnet/core` (deprecated September 2025) — CLAUDE.md contains the wrong plugin name
- [Roadmap]: Phase 4 needs research-phase before planning — Tiptap v3 server-side HTML rendering has no official .NET implementation; two options (Node.js sidecar vs. server-action pre-rendering) need architectural decision documented as ADR
- [Roadmap]: Phase 8 needs research-phase before planning — Vietnamese `unaccent` rules file completeness must be validated against all 6 tonal marks before CI
- [Phase 01-01]: Use @nx/dotnet (official Nx 22 plugin) NOT @nx-dotnet/core (deprecated September 2025)
- [Phase 01-01]: BlogPlatform.slnx — dotnet new sln in .NET 10 creates .slnx (new XML format) not .sln
- [Phase 01-01]: PostgreSQL 18 Docker volume at /var/lib/postgresql not /var/lib/postgresql/data (PGDATA path changed in PG18)
- [Phase 01-01]: MediatR is the only external NuGet reference allowed in Blog.Domain (for IDomainEvent : INotification)
- [Phase 01-01]: MinIO bucket creation via mc init container, not API startup code
- [Phase 01-02]: User aggregate uses standalone GUID matching IdentityUser.Id (ADR-006) — no inheritance, no FK constraint
- [Phase 01-02]: Comment.AddReply() throws DomainException when called on reply — nesting limited to 1 level enforced in Domain
- [Phase 01-02]: TagReference is a value object on Post holding only TagId — Post does not reference Tag entity directly
- [Phase 01-03]: EF Core suppressTransaction uses Sql(sql, suppressTransaction: true) not a Migration property — SuppressTransaction is on SqlOperation not Migration base class in EF Core 10
- [Phase 01-03]: OwnsMany post_tags composite key uses CLR property names HasKey('PostId', nameof(TagReference.TagId)) not column names — column name strings cause 'no property type specified' EF design-time error
- [Phase 01-03]: SocialLinks ValueComparer added for JSONB Dictionary<string,string> to enable EF change tracking on in-place dictionary mutations
- [Phase 01-04]: MediatR allowed in Blog.Domain.Common AND Blog.Domain.DomainEvents — domain events implement IDomainEvent which extends MediatR.INotification; NetArchTest sees this as MediatR dependency on event types
- [Phase 02-01]: MediatR 14.1.0 used in both Blog.Domain and Blog.Application — version must be consistent to avoid assembly binding conflicts
- [Phase 02-01]: IUnitOfWork is BlogDbContext-only in Phase 2 — Phase 3 will add cross-context IdentityDbContext overload for Register/Ban operations
- [Phase 02-01]: CachingBehavior has zero-overhead early return for non-ICacheableQuery requests — no cache interaction for uncacheable queries
- [Phase 02-02]: UnitOfWork clears domain events BEFORE SaveChangesAsync to prevent double-dispatch; handler failures after commit are logged and swallowed
- [Phase 02-02]: RedisCacheService.RemoveByPatternAsync uses Lua SCAN+DEL with ScriptEvaluateAsync(values:) — never KEYS which blocks Redis event loop
- [Phase 02-02]: NoOp stubs (email/storage) log debug and return gracefully — not throw — so Phase 2 works without SMTP or MinIO infrastructure

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 4]: Tiptap v3 `generateHTML()` has no official .NET port. Must resolve (Node.js sidecar vs server-action pre-rendering) before Phase 4 planning begins. Research flag: NEEDS research-phase.
- [Phase 8]: Vietnamese PostgreSQL FTS `unaccent` rules file gaps. Default dictionary has missing tonal combinations. Must source and validate community rules file before Phase 8 planning begins. Research flag: NEEDS research-phase.
- [Phase 2]: No standalone requirement IDs map to this phase. It is enabling infrastructure (MediatR pipeline, IUnitOfWork, Redis, Testcontainers). Not a coverage gap — it is foundational work that unblocks Phase 3+.

## Session Continuity

Last session: 2026-03-17T15:37:32.364Z
Stopped at: Completed 02-infrastructure-application-pipeline/02-02-PLAN.md
Resume file: None
