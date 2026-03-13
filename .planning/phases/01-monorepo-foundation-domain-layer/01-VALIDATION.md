---
phase: 1
slug: monorepo-foundation-domain-layer
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-13
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x + NetArchTest.Rules 1.3.2 |
| **Config file** | `tests/Blog.ArchTests/Blog.ArchTests.csproj` (none — Wave 0 creates it) |
| **Quick run command** | `dotnet test tests/Blog.ArchTests/ --no-build` |
| **Full suite command** | `dotnet test tests/Blog.ArchTests/ && dotnet test tests/Blog.UnitTests/ && nx build blog-api` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Blog.ArchTests/ --no-build`
- **After every plan wave:** Run `dotnet test tests/Blog.ArchTests/ && nx build blog-api`
- **Before `/gsd:verify-work`:** Full suite must be green + EF Core migration clean against docker-compose PostgreSQL 18
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-W0-arch-setup | 01 | 0 | INFR-01 | arch | `dotnet test tests/Blog.ArchTests/` | ❌ W0 | ⬜ pending |
| 1-W0-unit-setup | 01 | 0 | INFR-01 | unit | `dotnet test tests/Blog.UnitTests/` | ❌ W0 | ⬜ pending |
| 1-W0-docker | 01 | 0 | INFR-01 | smoke | `docker-compose up -d && docker-compose ps` | ❌ W0 | ⬜ pending |
| 1-01-nx-scaffold | 01 | 1 | INFR-01 | build | `nx build blog-api` | ❌ W0 | ⬜ pending |
| 1-01-domain-layer | 01 | 1 | INFR-01 | arch | `dotnet test tests/Blog.ArchTests/` | ❌ W0 | ⬜ pending |
| 1-01-value-objects | 01 | 1 | INFR-01 | arch | `dotnet test tests/Blog.ArchTests/` | ❌ W0 | ⬜ pending |
| 1-01-domain-events | 01 | 1 | INFR-01 | arch | `dotnet test tests/Blog.ArchTests/` | ❌ W0 | ⬜ pending |
| 1-01-ef-migration | 01 | 2 | INFR-01 | integration | `dotnet ef database update` against docker-compose | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Blog.ArchTests/Blog.ArchTests.csproj` — create project, add NetArchTest.Rules 1.3.2 + xUnit 2.9.x
- [ ] `tests/Blog.ArchTests/LayerBoundaryTests.cs` — Domain-does-not-reference-Infrastructure, Application-does-not-reference-API stubs
- [ ] `tests/Blog.ArchTests/DomainModelIntegrityTests.cs` — value object immutability, domain events are records, aggregates inherit AggregateRoot<TId>
- [ ] `tests/Blog.UnitTests/Blog.UnitTests.csproj` — create empty project (populated in Phase 2+)
- [ ] `docker-compose.yml` — PostgreSQL 18 + Redis 8 + MinIO + mc init container
- [ ] `docker/init.sql` — `CREATE EXTENSION IF NOT EXISTS unaccent;`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| PostgreSQL 18, Redis 8, MinIO all healthy after `docker-compose up` | INFR-01 | Container health depends on host Docker daemon | `docker-compose up -d && docker-compose ps` — all containers show "healthy" or "running" |
| EF Core migration applies cleanly with unaccent extension | INFR-01 | Requires running containers (no Testcontainers in Phase 1 — added Phase 2) | `docker-compose up -d && dotnet ef database update` — zero errors, migration table shows entry |
| `shared-contracts` implicit dependency in both frontend project.json | INFR-01 | Nx graph verification is visual/CLI | `nx graph` — blog-web and blog-admin depend on shared-contracts |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
