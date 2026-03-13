# Blog Platform — Claude Code Skills Index

Project-specific skills for the blog-platform monorepo (ASP.NET Core 10 + Next.js 16.1, Clean Architecture + DDD).

## Tier 1 — Critical (Multi-Role, High Frequency)

| # | Skill | Command | Roles | Description |
|---|-------|---------|-------|-------------|
| 1 | [add-api-endpoint](add-api-endpoint/SKILL.md) | `/add-api-endpoint` | Backend, Full-Stack | Create REST endpoint spanning all 4 Clean Architecture layers with CQRS, validation, auth, caching |
| 2 | [add-domain-entity](add-domain-entity/SKILL.md) | `/add-domain-entity` | Backend, Architect | Scaffold DDD aggregate/entity/value object with factory methods, domain events, repository interface |
| 3 | [enforce-rbac](enforce-rbac/SKILL.md) | `/enforce-rbac` | Backend, Frontend, Full-Stack | Implement 3-layer RBAC: API policies + MediatR AuthorizationBehavior + CASL frontend |
| 4 | [run-ef-migration](run-ef-migration/SKILL.md) | `/run-ef-migration` | Backend, DBA | Create/apply/rollback EF Core migrations with naming conventions and backward-compat rules |
| 5 | [add-cacheable-query](add-cacheable-query/SKILL.md) | `/add-cacheable-query` | Backend | Implement ICacheableQuery with cache key conventions and Domain Event invalidation |
| 6 | [add-frontend-page](add-frontend-page/SKILL.md) | `/add-frontend-page` | Frontend, Full-Stack | Create SSG/ISR page (blog-web) or dashboard page (blog-admin) with App Router patterns |

## Tier 2 — Important (Role-Specific, High Value)

| # | Skill | Command | Roles | Description |
|---|-------|---------|-------|-------------|
| 7 | [add-mediator-handler](add-mediator-handler/SKILL.md) | `/add-mediator-handler` | Backend | Create CQRS command/query handler with FluentValidation and pipeline behaviors |
| 8 | [add-domain-event](add-domain-event/SKILL.md) | `/add-domain-event` | Backend | Create domain event + handler for cache invalidation or cross-aggregate side effects |
| 9 | [add-integration-test](add-integration-test/SKILL.md) | `/add-integration-test` | QA, Backend | Write integration test with Testcontainers (PostgreSQL + Redis) |
| 10 | [render-tiptap-content](render-tiptap-content/SKILL.md) | `/render-tiptap-content` | Frontend | Render Tiptap v3 ProseMirror JSON in read-only mode with DOMPurify sanitization |
| 11 | [setup-github-actions-workflow](setup-github-actions-workflow/SKILL.md) | `/setup-github-actions-workflow` | DevOps | Create CI/CD pipeline with PR checks, staging auto-deploy, prod manual approval |
| 12 | [write-adr](write-adr/SKILL.md) | `/write-adr` | Architect | Create Architecture Decision Record following project ADR format and numbering |

## Tier 3 — Nice to Have (Infrequent or Lower Complexity)

| # | Skill | Command | Roles | Description |
|---|-------|---------|-------|-------------|
| 13 | [add-e2e-test](add-e2e-test/SKILL.md) | `/add-e2e-test` | QA, Frontend | Write Playwright E2E test for blog-web or blog-admin |
| 14 | [add-k8s-resource](add-k8s-resource/SKILL.md) | `/add-k8s-resource` | DevOps | Create Kubernetes manifest with Kustomize base + overlays (dev/staging/prod) |
| 15 | [cross-context-transaction](cross-context-transaction/SKILL.md) | `/cross-context-transaction` | Backend | Shared DbConnection pattern for IdentityDbContext + BlogDbContext operations (ADR-007) |
| 16 | [add-load-test-scenario](add-load-test-scenario/SKILL.md) | `/add-load-test-scenario` | QA | Write k6 load test with traffic distribution and pass criteria |
| 17 | [backup-restore](backup-restore/SKILL.md) | `/backup-restore` | SRE | Execute backup/restore for PostgreSQL (pgBackRest PITR), Redis, MinIO |
| 18 | [incident-response](incident-response/SKILL.md) | `/incident-response` | SRE | Follow SLI/SLO incident response workflow with severity-based escalation |

## Quick Reference by Role

| Role | Skills |
|------|--------|
| **Backend Developer** | add-api-endpoint, add-domain-entity, add-mediator-handler, add-cacheable-query, add-domain-event, run-ef-migration, cross-context-transaction |
| **Frontend Developer** | add-frontend-page, render-tiptap-content, enforce-rbac (CASL layer) |
| **Full-Stack Lead** | add-api-endpoint, add-frontend-page, enforce-rbac |
| **DevOps / Platform** | setup-github-actions-workflow, add-k8s-resource |
| **QA / Test Engineer** | add-integration-test, add-e2e-test, add-load-test-scenario |
| **DBA** | run-ef-migration |
| **Architect** | write-adr, add-domain-entity |
| **SRE / On-Call** | incident-response, backup-restore |
