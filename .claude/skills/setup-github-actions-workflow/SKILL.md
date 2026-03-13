# setup-github-actions-workflow

Create a GitHub Actions CI/CD workflow following the project's conventions for PR checks, staging auto-deploy, and production manual approval.

## Arguments

- `type` (required) — Workflow type: `ci` (PR checks), `cd-staging` (staging deploy), `cd-production` (production deploy), `migration-check`, `load-test`, `scheduled`
- `name` (optional) — Workflow display name
- `triggers` (optional) — Custom trigger events (defaults to convention-based)

## Instructions

You are creating a GitHub Actions workflow for the blog-platform monorepo. The project uses Nx for build orchestration with `@nx-dotnet/core` for .NET integration.

### Workflow File Location

`.github/workflows/{workflow-name}.yml`

### CI Workflow (PR Checks)

```yaml
# .github/workflows/ci.yml
name: CI

on:
  pull_request:
    branches: [main, dev]
  push:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

env:
  DOTNET_VERSION: '10.0.x'
  NODE_VERSION: '24'
  NX_CLOUD_ACCESS_TOKEN: ${{ secrets.NX_CLOUD_ACCESS_TOKEN }}

jobs:
  lint-and-typecheck:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-node@v4
        with:
          node-version: ${{ env.NODE_VERSION }}
          cache: 'npm'

      - run: npm ci

      - name: Derive Nx affected SHAs
        uses: nrwl/nx-set-shas@v4

      - name: Lint affected projects
        run: npx nx affected -t lint

      - name: Type-check affected projects
        run: npx nx affected -t typecheck

  backend-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:18
        env:
          POSTGRES_DB: blog_test
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
        ports: ['5432:5432']
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
      redis:
        image: redis:8
        ports: ['6379:6379']
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore .NET packages
        run: dotnet restore apps/blog-api/Blog.sln

      - name: Build
        run: dotnet build apps/blog-api/Blog.sln --no-restore

      - name: Unit tests
        run: dotnet test apps/blog-api/tests/Blog.UnitTests --no-build --verbosity normal

      - name: Integration tests
        run: dotnet test apps/blog-api/tests/Blog.IntegrationTests --no-build --verbosity normal
        env:
          ConnectionStrings__BlogDb: "Host=localhost;Database=blog_test;Username=test;Password=test"
          ConnectionStrings__Redis: "localhost:6379"

      - name: Architecture tests
        run: dotnet test apps/blog-api/tests/Blog.ArchTests --no-build --verbosity normal

  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-node@v4
        with:
          node-version: ${{ env.NODE_VERSION }}
          cache: 'npm'

      - run: npm ci

      - uses: nrwl/nx-set-shas@v4

      - name: Build affected projects
        run: npx nx affected -t build

      - name: Test affected projects
        run: npx nx affected -t test

  gen-types-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: ${{ env.NODE_VERSION }}
          cache: 'npm'

      - run: npm ci

      - name: Regenerate types from OpenAPI
        run: scripts/gen-types.sh

      - name: Check for uncommitted type changes
        run: |
          if [ -n "$(git diff --name-only)" ]; then
            echo "::error::Generated types are out of date. Run scripts/gen-types.sh and commit."
            git diff
            exit 1
          fi

  migration-check:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:18
        env:
          POSTGRES_DB: blog_migration_test
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
        ports: ['5432:5432']
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Check for pending model changes
        run: |
          dotnet ef migrations has-pending-model-changes \
            --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
            --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj

      - name: Apply all migrations (Up)
        run: dotnet ef database update --project apps/blog-api/src/Blog.Infrastructure --startup-project apps/blog-api/src/Blog.API
        env:
          ConnectionStrings__BlogDb: "Host=localhost;Database=blog_migration_test;Username=test;Password=test"

      - name: Rollback all migrations (Down)
        run: dotnet ef database update 0 --project apps/blog-api/src/Blog.Infrastructure --startup-project apps/blog-api/src/Blog.API
        env:
          ConnectionStrings__BlogDb: "Host=localhost;Database=blog_migration_test;Username=test;Password=test"

      - name: Re-apply (idempotency check)
        run: dotnet ef database update --project apps/blog-api/src/Blog.Infrastructure --startup-project apps/blog-api/src/Blog.API
        env:
          ConnectionStrings__BlogDb: "Host=localhost;Database=blog_migration_test;Username=test;Password=test"

      - name: Generate SQL artifact
        run: |
          dotnet ef migrations script --idempotent \
            --project apps/blog-api/src/Blog.Infrastructure \
            --startup-project apps/blog-api/src/Blog.API \
            --output migrations.sql

      - uses: actions/upload-artifact@v4
        with:
          name: migration-sql
          path: migrations.sql
```

### CD Staging Workflow

```yaml
# .github/workflows/cd-staging.yml
name: Deploy to Staging

on:
  push:
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_PREFIX: ${{ github.repository }}

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    strategy:
      matrix:
        app: [blog-api, blog-web, blog-admin]
    steps:
      - uses: actions/checkout@v4

      - uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - uses: docker/build-push-action@v5
        with:
          context: .
          file: deploy/docker/${{ matrix.app }}.Dockerfile
          push: true
          tags: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/${{ matrix.app }}:staging-${{ github.sha }}

  deploy-staging:
    needs: build-and-push
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4

      - name: Deploy to staging K8s
        run: |
          kubectl apply -k deploy/k8s/overlays/staging
          kubectl set image deployment/blog-api blog-api=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-api:staging-${{ github.sha }} -n blog-staging
          kubectl set image deployment/blog-web blog-web=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-web:staging-${{ github.sha }} -n blog-staging
          kubectl set image deployment/blog-admin blog-admin=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-admin:staging-${{ github.sha }} -n blog-staging
          kubectl rollout status deployment/blog-api -n blog-staging --timeout=300s

      - name: Run smoke tests
        run: |
          npx playwright test --config=tests/e2e/playwright.staging.config.ts --grep @smoke
```

### CD Production Workflow

```yaml
# .github/workflows/cd-production.yml
name: Deploy to Production

on:
  workflow_dispatch:
    inputs:
      image_tag:
        description: 'Image tag to deploy (staging-{sha})'
        required: true

jobs:
  deploy-production:
    runs-on: ubuntu-latest
    environment: production  # Requires manual approval
    steps:
      - uses: actions/checkout@v4

      - name: Deploy to production K8s
        run: |
          kubectl apply -k deploy/k8s/overlays/prod
          kubectl set image deployment/blog-api blog-api=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-api:${{ inputs.image_tag }} -n blog-prod
          kubectl set image deployment/blog-web blog-web=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-web:${{ inputs.image_tag }} -n blog-prod
          kubectl set image deployment/blog-admin blog-admin=${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}/blog-admin:${{ inputs.image_tag }} -n blog-prod
          kubectl rollout status deployment/blog-api -n blog-prod --timeout=300s
```

### Deployment Flow

```
PR → CI (lint, test, typecheck, gen-types, migration-check)
  → Merge to main → CD Staging (auto-deploy + smoke test)
    → Manual dispatch → CD Production (manual approval required)
```

### Key Rules

1. **Staging auto-deploys** on merge to main — no manual intervention
2. **Production requires manual approval** — via GitHub Environment protection rules
3. **Migration SQL is a CI artifact** — reviewers must read it before approving destructive migrations
4. **Destructive migrations need 2 reviewers** — enforced via branch protection
5. **Nx affected** — Only build/test/lint projects affected by the PR changes
6. **Concurrency groups** — Cancel in-progress CI runs for the same PR
