# GitHub Configuration Design

**Date:** 2026-03-13
**Status:** Approved
**Scope:** `.github/` directory — CI/CD workflow stubs + community health files

---

## Context

The blog-platform repository is in the planning phase. No application code exists yet. The goal is to initialize a complete `.github/` configuration that:

1. Enforces contribution standards immediately (PR template, issue templates, CODEOWNERS)
2. Scaffolds CI/CD workflow stubs that match Phase 9 success criteria exactly, so wiring in real commands phase by phase requires no structural changes
3. Keeps dependencies up to date via Dependabot

**Branching strategy:** `main` + `dev`. Feature branches PR into `dev`. `dev` merges into `main` for releases.

---

## File Structure

```
.github/
├── workflows/
│   ├── ci.yml                  # PR gate: type-check, lint, test, gen-types freshness
│   ├── cd-staging.yml          # Auto-deploy to staging on push to dev
│   ├── cd-prod.yml             # Deploy to prod on push to main (manual approval gate)
│   └── gen-types-check.yml     # Reusable workflow: shared-contracts diff check
├── ISSUE_TEMPLATE/
│   ├── bug_report.md
│   ├── feature_request.md
│   └── config.yml              # Disables blank issues
├── pull_request_template.md
├── CODEOWNERS
└── dependabot.yml
```

---

## Workflows

### `ci.yml` — Continuous Integration

**Trigger:** `pull_request` targeting `dev` or `main`

**Jobs (in dependency order):**

| Job | Depends on | Purpose |
|-----|-----------|---------|
| `type-check` | — | TypeScript type check across blog-web and blog-admin |
| `lint` | — | ESLint (frontend) + dotnet format check (backend) |
| `test-unit` | — | dotnet test Blog.UnitTests |
| `test-integration` | `test-unit` | dotnet test Blog.IntegrationTests (Testcontainers) |
| `test-e2e` | `test-unit` | npx playwright test |
| `gen-types-check` | — | Calls reusable gen-types-check.yml; fails if shared-contracts diff detected |

All jobs are stubs (`run: echo "stub"`) with `# TODO (Phase N):` comments. The job graph and `needs:` wiring are real.

### `cd-staging.yml` — Staging Deployment

**Trigger:** `push` to `dev`
**Environment:** `staging`

**Jobs:**
- `build` — build all apps
- `deploy-staging` — deploy to Kubernetes staging overlay (needs `build`)

### `cd-prod.yml` — Production Deployment

**Trigger:** `push` to `main`
**Environment:** `production` (requires manual approval gate via GitHub Environments)

**Jobs:**
- `build` — build all apps
- `deploy-prod` — deploy to Kubernetes prod overlay (needs `build`, blocked on manual approval)

### `gen-types-check.yml` — Reusable Workflow

**Trigger:** `workflow_call`
Runs `scripts/gen-types.sh` and checks for a diff against committed `shared-contracts`. Fails CI if types are stale.

---

## Community Health Files

### `pull_request_template.md`

Sections:
- **Summary** — what this PR does
- **Type** — `feat / fix / refactor / chore / perf / ci / ops / build / docs / style / revert / test`
- **Linked issue** — `Closes #`
- **Test plan** — steps to verify
- **Checklist** — tests pass, lint clean, types regenerated if API changed, docs updated if needed

### Issue Templates

**`bug_report.md`:**
- Describe the bug
- Steps to reproduce
- Expected vs actual behavior
- Environment (OS, browser, app version)
- Logs / screenshots

**`feature_request.md`:**
- Problem statement
- Proposed solution
- Acceptance criteria
- Out of scope

**`config.yml`:** Disables blank issues, links to `docs/blog-platform/` for architecture questions.

### `CODEOWNERS`

```
# Default: core team owns everything
*                           @<org>/core-team

# Backend
apps/blog-api/              @<org>/backend-team

# Frontends
apps/blog-web/              @<org>/frontend-team
apps/blog-admin/            @<org>/frontend-team

# Shared contracts
libs/shared-contracts/      @<org>/core-team

# Infrastructure
deploy/                     @<org>/platform-team
.github/                    @<org>/platform-team
```

All team slugs are placeholders — replace with actual GitHub team names.

### `dependabot.yml`

- **npm** — weekly, directory `/` (monorepo root), label `dependencies`
- **nuget** — weekly, directory `apps/blog-api` (once scaffolded), label `dependencies`
- Both target `dev` branch (not `main`)

---

## Implementation Notes

- Workflow stubs use `run: echo "stub - not yet implemented"` to make them syntactically valid and pass YAML linting without executing anything
- Each stub step carries a `# TODO (Phase N): <actual command>` comment so the phase that implements it has clear instructions
- The `production` environment in `cd-prod.yml` must be created in GitHub repository settings with required reviewers to activate the manual approval gate
- Secrets referenced in workflow stubs (`KUBECONFIG`, `REGISTRY_TOKEN`, etc.) are placeholder names documented in comments — no real values needed until Phase 9

---

## Success Criteria

1. `.github/` directory exists with all files listed above
2. All workflow YAML files are syntactically valid (pass `actionlint` if available)
3. PR template appears automatically when opening a PR on GitHub
4. Issue templates appear as options when creating a new issue
5. CODEOWNERS assigns reviewers correctly for changed paths
6. Dependabot is enabled and targets `dev` branch
