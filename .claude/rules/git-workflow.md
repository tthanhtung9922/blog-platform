---
description: >
  Apply these rules whenever creating commits, branches, pull requests, or
  interacting with the CI/CD pipeline. Triggers on: git operations, commit
  messages, branch naming, PR creation, merge strategy, type regeneration.
---

# Git & CI Workflow Rules

## MUST

- **Conventional Commits format:**
  ```
  <type>[optional scope]: <description>

  [optional body]

  [optional footer]
  ```
  **Types:** `feat`, `fix`, `refactor`, `chore`, `perf`, `ci`, `ops`, `build`, `docs`, `style`, `revert`, `test`
  ```
  feat(auth): add Google OAuth login
  fix(posts): correct slug generation for Vietnamese characters
  docs(adr): add ADR-010 for rate limiting strategy
  chore(deps): update Next.js to 16.1.2
  ```
- **Branch naming prefixes:** `feat/<name>`, `fix/<name>`, `chore/<name>`, `docs/<name>`.
- **PRs target `dev` branch.** Flow: `feat/your-feature` → PR → `dev` → PR → `main`.
- **Regenerate TypeScript types** (`scripts/gen-types.sh`) whenever the API contract (OpenAPI spec or controller signatures) changes. Commit the regenerated types in the same PR.
- **CI must pass before merge:** lint, unit tests, integration tests, build Docker images, migration validation (apply → rollback → re-apply).

## SHOULD

- Use squash-merge for PRs to keep a clean commit history on `dev` and `main`.
- Run these checks locally before pushing:
  ```bash
  dotnet test                              # backend tests
  nx run-many --target=lint --all          # lint
  nx run-many --target=type-check --all    # TypeScript
  ```
- Write atomic commits — each commit should represent one logical change.

## NEVER

- Never push directly to `main` — always go through a PR.
- Never merge a PR with failing CI checks.
- Never commit secrets, `.env` files, or credentials. Use `.env.example` as a template.
