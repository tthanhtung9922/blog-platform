---
phase: 01-monorepo-foundation-domain-layer
plan: "01"
subsystem: infra
tags: [nx, dotnet, docker, postgresql, redis, minio, efcore, mediatr]

# Dependency graph
requires: []
provides:
  - Nx 22 workspace with @nx/dotnet and @nx/next plugins configured
  - .NET 10 solution with Blog.Domain, Blog.Infrastructure, Blog.API, Blog.ArchTests, Blog.UnitTests
  - Nx project graph with all 9 projects registered and correct implicit dependencies
  - Docker Compose local dev environment (PostgreSQL 18, Redis 8, MinIO)
  - PostgreSQL first-boot init.sql enabling unaccent extension
affects:
  - 01-02 (Domain layer builds on top of the project scaffold)
  - 01-03 (Infrastructure layer uses the NuGet packages added here)
  - 01-04 (Migrations use the EF Core setup established here)
  - all subsequent phases (depend on Nx graph structure)

# Tech tracking
tech-stack:
  added:
    - Nx 22.5.4 (monorepo task runner)
    - "@nx/dotnet plugin (official, infers build/test targets from .csproj)"
    - "@nx/next plugin (Next.js monorepo integration)"
    - MediatR 12.5.0 (IDomainEvent base in Domain layer)
    - Microsoft.EntityFrameworkCore 10.0.5
    - Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1
    - EFCore.NamingConventions 10.0.1 (UseSnakeCaseNamingConvention)
    - Microsoft.EntityFrameworkCore.Design 10.0.5
    - Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.x
    - NetArchTest.Rules 1.3.2
    - FluentAssertions 6.x
    - Docker Compose (PostgreSQL 18, Redis 8, MinIO)
  patterns:
    - project.json alongside .csproj registers project name and tags in Nx graph
    - implicitDependencies in project.json declares graph edges Nx cannot detect via static analysis
    - PostgreSQL 18 volume must target /var/lib/postgresql (not /var/lib/postgresql/data)
    - MinIO bucket initialization via one-shot mc init container
    - BlogPlatform.slnx (new XML solution format in .NET 10)

key-files:
  created:
    - nx.json
    - package.json
    - BlogPlatform.slnx
    - apps/blog-api/src/Blog.Domain/Blog.Domain.csproj
    - apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj
    - apps/blog-api/src/Blog.API/Blog.API.csproj
    - tests/Blog.ArchTests/Blog.ArchTests.csproj
    - tests/Blog.UnitTests/Blog.UnitTests.csproj
    - apps/blog-api/src/Blog.Domain/project.json
    - apps/blog-api/src/Blog.Infrastructure/project.json
    - apps/blog-api/src/Blog.API/project.json
    - tests/Blog.ArchTests/project.json
    - tests/Blog.UnitTests/project.json
    - apps/blog-web/project.json
    - apps/blog-admin/project.json
    - libs/shared-contracts/project.json
    - libs/shared-ui/project.json
    - docker-compose.yml
    - docker/init.sql
  modified:
    - .gitignore (updated for Nx, .NET, Next.js patterns)

key-decisions:
  - "Use @nx/dotnet (official Nx 22 plugin) NOT @nx-dotnet/core (deprecated September 2025)"
  - "BlogPlatform.slnx — dotnet new sln in .NET 10 creates .slnx (new XML format) not .sln; both supported by dotnet CLI"
  - "PostgreSQL 18 Docker volume at /var/lib/postgresql not /var/lib/postgresql/data (PGDATA path changed in PG18)"
  - "MediatR is the only external NuGet reference in Blog.Domain — needed for IDomainEvent : INotification"
  - "MinIO bucket creation via mc init container, not API startup code"

patterns-established:
  - "Nx project registration: place project.json alongside .csproj with name and tags; @nx/dotnet auto-infers build/test/watch targets"
  - "Frontend implicit dependencies: project.json implicitDependencies array declares shared-contracts dependency"
  - "Docker Compose pattern: one-shot init container for external service configuration (MinIO bucket creation)"
  - "PostgreSQL initialization: docker-entrypoint-initdb.d/init.sql runs on first boot only"

requirements-completed: [INFR-01]

# Metrics
duration: 8min
completed: 2026-03-15
---

# Phase 1 Plan 01: Nx Workspace Scaffold + Docker Compose Summary

**Nx 22 polyglot monorepo initialized with @nx/dotnet plugin, 5 .NET 10 projects registered in the project graph, and Docker Compose stack (PostgreSQL 18, Redis 8, MinIO) with automated bucket initialization**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-15T05:41:27Z
- **Completed:** 2026-03-15T05:49:44Z
- **Tasks:** 3
- **Files modified:** 19

## Accomplishments

- Nx 22 workspace initialized with `@nx/dotnet` (official) and `@nx/next` plugins; `nx.json` correctly references `@nx/dotnet` not `@nx-dotnet/core`
- 5 .NET 10 projects scaffolded via `dotnet new`, added to `BlogPlatform.slnx` solution, all NuGet packages installed; `dotnet build` exits 0 with 0 errors
- All 9 projects registered in the Nx graph via `project.json` files; `blog-web` and `blog-admin` declare `shared-contracts` as `implicitDependencies`; `blog-api` has build/test targets auto-inferred by `@nx/dotnet`
- Docker Compose stack defined with PostgreSQL 18, Redis 8, MinIO, and a one-shot `minio-init` container that creates the `blog-media` bucket

## Task Commits

Each task was committed atomically:

1. **Task 1: Initialize Nx workspace and create all project scaffolds** - `d995a78` (chore)
2. **Task 2: Register all projects in Nx graph and configure implicit dependencies** - `a74d858` (chore)
3. **Task 3: Create Docker Compose environment for local development** - `d357359` (chore)

## Files Created/Modified

- `nx.json` - Nx workspace config with @nx/dotnet and @nx/next plugins
- `package.json` - Node workspace manifest
- `BlogPlatform.slnx` - .NET 10 solution file (new XML format)
- `apps/blog-api/src/Blog.Domain/Blog.Domain.csproj` - Domain classlib, MediatR 12.x reference
- `apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj` - Infrastructure classlib, EF Core 10 + Npgsql 10.0.1
- `apps/blog-api/src/Blog.API/Blog.API.csproj` - Web API, references Infrastructure, health checks
- `tests/Blog.ArchTests/Blog.ArchTests.csproj` - xUnit + NetArchTest.Rules 1.3.2 + FluentAssertions
- `tests/Blog.UnitTests/Blog.UnitTests.csproj` - xUnit test project (empty in Phase 1)
- `apps/blog-api/src/Blog.Domain/project.json` - Registers blog-domain with tags in Nx graph
- `apps/blog-api/src/Blog.Infrastructure/project.json` - Registers blog-infrastructure in Nx graph
- `apps/blog-api/src/Blog.API/project.json` - Registers blog-api in Nx graph
- `tests/Blog.ArchTests/project.json` - Registers blog-arch-tests in Nx graph
- `tests/Blog.UnitTests/project.json` - Registers blog-unit-tests in Nx graph
- `apps/blog-web/project.json` - Registers blog-web with shared-contracts implicit dependency
- `apps/blog-admin/project.json` - Registers blog-admin with shared-contracts implicit dependency
- `libs/shared-contracts/project.json` - Registers shared-contracts lib
- `libs/shared-ui/project.json` - Registers shared-ui lib
- `docker-compose.yml` - PostgreSQL 18 + Redis 8 + MinIO + minio-init services
- `docker/init.sql` - CREATE EXTENSION IF NOT EXISTS unaccent
- `.gitignore` - Updated for Nx cache, .NET bin/obj, Next.js, env files

## Decisions Made

- **@nx/dotnet over @nx-dotnet/core**: The official plugin (Nx 22+) auto-infers all dotnet targets from `.csproj` files. No generators or explicit executor config needed in `project.json`.
- **BlogPlatform.slnx format**: `dotnet new sln` in .NET 10 produces `.slnx` (XML-based solution format) instead of the old `.sln` format. The dotnet CLI and MSBuild both support it — no action needed.
- **PostgreSQL 18 volume path**: Mounts at `/var/lib/postgresql` (not `/var/lib/postgresql/data`) because PostgreSQL 18 changed its internal PGDATA to `/var/lib/postgresql/18/docker`. The old path would silently lose data on container restart.
- **MediatR in Domain layer**: The only acceptable external reference in Blog.Domain. Required for `IDomainEvent : INotification` interface. Architecture tests will allow this one exception.
- **npx create-nx-workspace@latest . fails**: The `.` workspace name is invalid. Used `npm init -y` + `npx nx@latest init` instead — produces identical result.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Used `npm init -y && npx nx init` instead of `create-nx-workspace`**
- **Found during:** Task 1 (Initialize Nx workspace)
- **Issue:** `npx create-nx-workspace@latest .` rejects `.` as a workspace name; error: "Workspace names must start with a letter"
- **Fix:** Used `npm init -y` to create `package.json` first, then `npx nx@latest init --preset=empty --nxCloud=skip` to initialize Nx. Produces the same `nx.json` structure.
- **Files modified:** `package.json`, `nx.json`
- **Verification:** `nx.json` plugins array contains `@nx/dotnet` and `@nx/next`; `dotnet build` exits 0
- **Committed in:** `d995a78` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking issue)
**Impact on plan:** Blocking issue resolved with equivalent approach. Final workspace structure is identical to what `create-nx-workspace` would have produced.

## Issues Encountered

- Docker Desktop is not running on the execution machine. The `docker-compose.yml` and `docker/init.sql` files are created and correct. Runtime verification (`docker-compose up -d`) requires Docker Desktop to be started by the developer. All non-Docker verification steps passed: `dotnet build`, `nx show project blog-web`, `nx show project blog-api`.

## User Setup Required

To complete runtime verification of the Docker stack:

1. Start Docker Desktop
2. From the repo root: `docker compose up -d`
3. Wait ~15 seconds then verify: `docker compose ps`
   - Expected: `blog-postgres`, `blog-redis`, `blog-minio` are running; `blog-minio-init` shows Exit 0
4. Verify unaccent extension: `docker exec blog-postgres psql -U blog -d blog_db -c "SELECT extname FROM pg_extension WHERE extname='unaccent';"`
   - Expected: 1 row with `unaccent`

## Next Phase Readiness

- Nx workspace is fully configured — all subsequent plans can use `nx build`, `nx test`, and the project graph
- All .NET project scaffolds are in place with correct NuGet packages — Plan 01-02 can immediately start adding domain code to `Blog.Domain`
- Docker Compose stack is defined — developers can run `docker compose up -d` to get local PostgreSQL 18, Redis 8, and MinIO
- No blockers for Phase 1 Plan 01-02 (Domain Layer)

## Self-Check: PASSED

All created files verified to exist on disk. All 3 task commits verified in git log.

---
*Phase: 01-monorepo-foundation-domain-layer*
*Completed: 2026-03-15*
