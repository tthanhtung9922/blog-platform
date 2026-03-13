# Contributing to Blog Platform

Thank you for your interest in contributing. This document covers the branch strategy, commit conventions, and pull request process.

## Branching Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready code. Merging here auto-deploys to staging; production requires manual approval. |
| `dev` | Integration branch. All feature PRs target `dev`. |
| `feat/<name>` | New features |
| `fix/<name>` | Bug fixes |
| `chore/<name>` | Maintenance, dependency updates |
| `docs/<name>` | Documentation only |

**Flow:** `feat/your-feature` → PR → `dev` → PR → `main`

## Commit Convention

This project follows [Conventional Commits](docs/git-commit-message-best-practices.md).

```
<type>[optional scope]: <description>

[optional body]

[optional footer]
```

**Types:** `feat`, `fix`, `refactor`, `chore`, `perf`, `ci`, `ops`, `build`, `docs`, `style`, `revert`, `test`

**Examples:**

```
feat(auth): add Google OAuth login
fix(posts): correct slug generation for Vietnamese characters
docs(adr): add ADR-010 for rate limiting strategy
chore(deps): update Next.js to 16.1.2
```

## Pull Request Process

1. Branch off `dev`: `git checkout -b feat/your-feature dev`
2. Make your changes with atomic commits following the convention above
3. Ensure all CI checks pass locally before pushing:
   - `dotnet test` — backend unit + integration tests
   - `nx run-many --target=lint --all` — lint
   - `nx run-many --target=type-check --all` — TypeScript
   - `scripts/gen-types.sh` — regenerate types if you changed the API
4. Open a PR targeting `dev` and fill in the PR template
5. Request review from the relevant code owners (see [CODEOWNERS](.github/CODEOWNERS))
6. Squash-merge after approval

## Development Setup

See [README.md](README.md) for prerequisites and local setup instructions.

## Code Style

- **Backend (C#):** Follow the existing Clean Architecture layer boundaries. Domain must not reference Infrastructure. Run `dotnet format` before committing.
- **Frontend (TypeScript):** ESLint + Prettier configured at monorepo root. Run `nx run-many --target=lint --all --fix`.
- **Tailwind CSS v4:** CSS-first configuration — no `tailwind.config.ts`.

## Architecture Constraints

Before making structural changes, read the [Architecture Decision Records](docs/blog-platform/03-architecture-decisions.md). Key rules:

- `IdentityUser` and `User` domain aggregate are **separate models** — do not conflate them (ADR-006)
- Caching is **opt-in** via `ICacheableQuery` — do not cache queries implicitly (ADR-008)
- MediatR pipeline behavior order is fixed: `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior`

## Reporting Issues

Use the issue templates when opening a bug report or feature request.
