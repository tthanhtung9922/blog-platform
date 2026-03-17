---
phase: 2
slug: infrastructure-application-pipeline
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-16
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none — conventions only |
| **Quick run command** | `dotnet test tests/Blog.ArchTests` |
| **Full suite command** | `dotnet test` (all test projects) |
| **Estimated runtime** | ~60 seconds (ArchTests fast; IntegrationTests ~45s with containers) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Blog.ArchTests`
- **After every plan wave:** Run `dotnet test` (all test projects)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| SC1 | TBD | TBD | MediatR pipeline order | Arch (DI reflection) | `dotnet test tests/Blog.ArchTests` | ❌ W0 | ⬜ pending |
| SC2 | TBD | TBD | IUnitOfWork rollback | Integration | `dotnet test tests/Blog.IntegrationTests --filter "UnitOfWork"` | ❌ W0 | ⬜ pending |
| SC3 | TBD | TBD | Redis cache on 2nd call | Integration | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` | ❌ W0 | ⬜ pending |
| SC4 | TBD | TBD | Domain Event → cache invalidation | Integration | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` | ❌ W0 | ⬜ pending |
| SC5 | TBD | TBD | Lua SCAN+DEL clears patterns | Integration | `dotnet test tests/Blog.IntegrationTests --filter "Caching"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Blog.IntegrationTests/Blog.IntegrationTests.csproj` — new project, does not exist yet
- [ ] `tests/Blog.IntegrationTests/Fixtures/ApiFactory.cs` — WebApplicationFactory subclass
- [ ] `tests/Blog.IntegrationTests/Fixtures/IntegrationTestFixture.cs` — Testcontainers + Respawn
- [ ] `tests/Blog.IntegrationTests/Fixtures/IntegrationTestCollection.cs` — collection definition
- [ ] `tests/Blog.IntegrationTests/Helpers/JwtTokenHelper.cs` — GenerateJwt(role)
- [ ] `apps/blog-api/src/Blog.Application/Blog.Application.csproj` — new project, does not exist yet
- [ ] `apps/blog-api/src/Blog.API/Program.cs` — needs `public partial class Program {}` added

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Redis Lua script produces correct side effects in staging | SC4/SC5 | Staging Redis not accessible from CI | Run `dotnet test tests/Blog.IntegrationTests --filter "Caching"` against staging env manually |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
