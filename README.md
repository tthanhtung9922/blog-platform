# Blog Platform

A production-grade blog platform for Vietnamese content creators and readers. Built as a self-hosted, open-source Nx monorepo with an ASP.NET Core 10 backend and two Next.js 16.1 frontends.

## Overview

| App | Purpose |
|-----|---------|
| `blog-web` | Public reader — SSG/ISR, SEO-optimized, Vietnamese FTS |
| `blog-admin` | CMS dashboard — Tiptap v3 editor, RBAC-gated |
| `blog-api` | ASP.NET Core 10 REST API — Clean Architecture + DDD |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 10 LTS, C# |
| Database | PostgreSQL 18 (FTS with Vietnamese unaccent) |
| Cache | Redis 8 (cache-aside via MediatR) |
| Object Storage | MinIO |
| Frontend | Next.js 16.1, TypeScript 6.0, Tailwind CSS v4 |
| Rich Text | Tiptap v3 (ProseMirror JSON) |
| UI Components | shadcn/ui |
| Auth | NextAuth v5 + ASP.NET Identity |
| Authorization | CASL >= 6.8.0 (frontend), Policy-based (backend) |
| Testing | xUnit 3.2 + Testcontainers, Playwright 1.58 |
| Monorepo | Nx with @nx-dotnet/core |
| CI/CD | GitHub Actions |
| Deploy | Docker + Kubernetes (Kustomize) |

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 24 LTS](https://nodejs.org/)
- [Nx CLI](https://nx.dev/): `npm install -g nx`

## Getting Started

```bash
# Clone the repo
git clone https://github.com/<org>/blog-platform.git
cd blog-platform

# Start infrastructure (PostgreSQL, Redis, MinIO)
docker-compose up -d

# Install Node dependencies
npm install

# Run EF Core migrations
dotnet ef database update --project apps/blog-api

# Start all apps
nx run-many --target=serve --all
```

> **Note:** The application is currently in the planning phase. No runnable code exists yet — see [docs/blog-platform/](docs/blog-platform/) for architecture documentation.

## Project Structure

```
blog-platform/
├── apps/
│   ├── blog-api/        # ASP.NET Core 10 backend
│   ├── blog-web/        # Next.js 16.1 public reader
│   └── blog-admin/      # Next.js 16.1 CMS dashboard
├── libs/
│   ├── shared-contracts/ # OpenAPI-generated TypeScript types
│   └── shared-ui/        # Shared React component library
├── deploy/
│   ├── docker/
│   └── k8s/             # Kustomize base + overlays
├── docs/
│   └── blog-platform/   # Architecture docs, ADRs, API spec
└── scripts/
    └── gen-types.sh     # Regenerate TypeScript types from OpenAPI spec
```

## Key Commands

```bash
# Backend tests
dotnet test Blog.UnitTests
dotnet test Blog.IntegrationTests
dotnet test Blog.ArchTests

# E2E tests
npx playwright test

# Regenerate TypeScript types from OpenAPI spec
scripts/gen-types.sh

# EF Core migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update

# Nx builds
nx build blog-api
nx build blog-web
nx build blog-admin
```

## Documentation

Full architecture documentation lives in [`docs/blog-platform/`](docs/blog-platform/):

- [Architecture Decisions (ADRs)](docs/blog-platform/03-architecture-decisions.md)
- [Database Schema](docs/blog-platform/06-database-schema.md)
- [API Contract (OpenAPI)](docs/blog-platform/09-api-contract--openapi-specification.md)
- [Folder Structure](docs/blog-platform/02-folder-structure.md)
- [Data Migration Runbook](docs/blog-platform/11-data-migration-runbook.md)
- [Disaster Recovery & Backup](docs/blog-platform/07-disaster-recovery--backup.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch strategy, commit conventions, and pull request guidelines.

## Security

See [SECURITY.md](SECURITY.md) for how to report vulnerabilities.

## License

[MIT](LICENSE)
