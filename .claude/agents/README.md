# Project Agents Registry

Specialized sub-agents for the blog-platform monorepo. Each agent provides domain-specific judgment, analysis, and multi-step reasoning — complementing the 18 task-oriented [skills](./../skills/INDEX.md).

**Key distinction**: Skills = step-by-step task recipes (e.g., "scaffold this endpoint"). Agents = autonomous analysis personas (e.g., "review this PR for violations").

## Quick Reference

| Agent | Tier | Roles | Use When |
|---|---|---|---|
| [`review-architecture`](review-architecture.md) | 1 | Backend, Architect, Full-Stack | Validating Clean Architecture layers, DDD rules, ADR compliance |
| [`review-pull-request`](review-pull-request.md) | 1 | All Engineering | Comprehensive PR review before merge (architecture + RBAC + cache + tests) |
| [`plan-feature`](plan-feature.md) | 1 | Backend, Frontend, Full-Stack, Architect | Decomposing a feature into tasks across all layers and apps |
| [`debug-backend`](debug-backend.md) | 2 | Backend, Full-Stack | Systematic debugging of ASP.NET Core issues through the full request pipeline |
| [`review-migration`](review-migration.md) | 2 | Backend, DBA | Reviewing EF Core migrations for production safety and backward compatibility |
| [`audit-rbac`](audit-rbac.md) | 2 | Backend, Frontend, Architect | Verifying 3-layer RBAC consistency (API + MediatR + CASL) |
| [`optimize-database`](optimize-database.md) | 2 | Backend, DBA, SRE | Analyzing queries, recommending indexes, checking performance budgets |
| [`review-frontend`](review-frontend.md) | 3 | Frontend, Full-Stack | Reviewing Next.js patterns, Tailwind v4, Tiptap rendering, SEO |
| [`review-infrastructure`](review-infrastructure.md) | 3 | DevOps, SRE | Reviewing K8s manifests, Dockerfiles, CI/CD pipelines |
| [`plan-test-strategy`](plan-test-strategy.md) | 3 | QA, Backend, Frontend | Determining which tests a feature needs, identifying coverage gaps |

## By Role

### Backend Developer
- `review-architecture` — validate layer boundaries and DDD patterns
- `debug-backend` — trace issues through MediatR pipeline to EF Core/Redis
- `optimize-database` — analyze query performance and index strategy
- `review-migration` — check migration safety before staging/production
- `plan-feature` — break down features into backend tasks

### Frontend Developer
- `review-frontend` — validate Next.js patterns, Tailwind v4, Tiptap rendering
- `plan-feature` — understand frontend tasks in context of full feature
- `plan-test-strategy` — determine E2E test needs

### Architect / Tech Lead
- `review-architecture` — enforce Clean Architecture + DDD rules
- `review-pull-request` — comprehensive PR review
- `audit-rbac` — verify 3-layer permission consistency
- `plan-feature` — design feature decomposition across all layers

### DevOps / Platform Engineer
- `review-infrastructure` — review K8s, Docker, CI/CD configurations

### QA / Test Engineer
- `plan-test-strategy` — identify which tests to write per feature

### DBA / Database Engineer
- `review-migration` — migration safety review
- `optimize-database` — query and index optimization

### SRE
- `review-infrastructure` — infrastructure configuration review
- `optimize-database` — performance budget validation

## Agent Dependencies

```
plan-feature ──────► review-architecture (validate plan's design)
                 ├── audit-rbac (verify permission design)
                 └── plan-test-strategy (plan tests for the feature)

review-pull-request ► review-architecture (deeper arch review)
                    ├── audit-rbac (RBAC consistency)
                    ├── review-migration (if migrations included)
                    ├── review-frontend (deeper frontend review)
                    └── review-infrastructure (if infra changes)

debug-backend ─────► optimize-database (if issue is query perf)
                  └── review-architecture (if bug reveals arch violation)

review-migration ──► optimize-database (verify index recommendations)
```

## Setup
Agents are auto-loaded by Claude Code from `.claude/agents/`. No configuration needed.
