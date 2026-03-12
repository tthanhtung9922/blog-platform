# Folder Structure

## 2.1 Monorepo Root

```
blog-platform/                          # Monorepo root (Nx workspace)
│
├── apps/                               # Runnable applications
│   ├── blog-api/                       # ASP.NET Core 10 — REST API
│   ├── blog-web/                       # Next.js 16.1 — Public reader
│   └── blog-admin/                     # Next.js 16.1 — CMS dashboard
│
├── libs/                               # Shared libraries
│   ├── shared-contracts/               # OpenAPI-generated TypeScript types
│   └── shared-ui/                      # Reusable React component library
│
├── deploy/                             # Infrastructure & orchestration
│   ├── docker/                         # Dockerfiles per app
│   └── k8s/                            # Kubernetes manifests
│
├── docs/                               # Architecture Decision Records (ADR)
│   ├── adr/
│   │   ├── 001-monorepo-nx.md
│   │   ├── 002-clean-architecture.md
│   │   ├── 003-two-frontend-apps.md    # [FIX] Đổi tên từ 003-rbac-strategy.md
│   │   ├── 004-rbac-strategy.md        # [FIX] Đổi tên từ 004-caching-strategy.md
│   │   ├── 005-caching-strategy.md     # [FIX] Thêm mới — tương ứng ADR-005
│   │   ├── 006-identity-vs-domain-user.md  # [FIX] Thêm mới — tương ứng ADR-006
│   │   ├── 007-transaction-strategy.md     # Thêm mới — tương ứng ADR-007
│   │   ├── 008-cache-opt-in.md             # Thêm mới — tương ứng ADR-008
│   │   └── 009-postgresql-fts-vietnamese.md # Thêm mới — tương ứng ADR-009
│   └── diagrams/                       # C4 diagrams, sequence diagrams
│
├── scripts/                            # Developer utility scripts
│   ├── seed-db.sh
│   ├── gen-types.sh                    # OpenAPI → TypeScript codegen
│   └── migration.sh
│
├── .github/
│   └── workflows/                      # CI/CD pipelines
│
├── nx.json                             # Nx workspace config
├── package.json                        # Root package (FE tooling)
├── docker-compose.yml                  # Local development (all services)
├── docker-compose.emergency-only.yml   # ⚠️ Emergency / local-staging fallback ONLY (NOT for production K8s)
│                                       # Đổi tên từ docker-compose.prod.yml để tránh nhầm lẫn
├── .env.example                        # Environment variables template
└── README.md
```

---

## 2.2 Backend — ASP.NET Core 10 (Clean Arch + DDD)

```
apps/blog-api/
│
├── src/
│   │
│   ├── Blog.Domain/                    # 🔴 Domain Layer — core business logic
│   │   ├── Aggregates/
│   │   │   ├── Posts/
│   │   │   │   ├── Post.cs             # Aggregate Root
│   │   │   │   ├── PostContent.cs      # Entity
│   │   │   │   ├── PostStatus.cs       # Enum (Draft, Published, Archived)
│   │   │   │   └── PostVersion.cs      # Entity — content versioning
│   │   │   ├── Comments/
│   │   │   │   ├── Comment.cs          # Aggregate Root
│   │   │   │   └── Reply.cs            # Entity
│   │   │   └── Users/
│   │   │       ├── User.cs             # Aggregate Root
│   │   │       └── UserProfile.cs      # Entity
│   │   │
│   │   ├── ValueObjects/
│   │   │   ├── Slug.cs                 # Immutable, validated slug
│   │   │   ├── Tag.cs
│   │   │   ├── Email.cs
│   │   │   └── ReadingTime.cs
│   │   │
│   │   ├── DomainEvents/
│   │   │   ├── PostPublishedEvent.cs
│   │   │   ├── PostArchivedEvent.cs
│   │   │   ├── CommentAddedEvent.cs
│   │   │   └── UserRegisteredEvent.cs
│   │   │
│   │   ├── Repositories/               # Interfaces only — no implementation here
│   │   │   ├── IPostRepository.cs
│   │   │   ├── ICommentRepository.cs
│   │   │   └── IUserRepository.cs
│   │   │
│   │   ├── Services/                   # Domain services (stateless)
│   │   │   └── SlugGeneratorService.cs
│   │   │
│   │   └── Exceptions/
│   │       ├── PostNotFoundException.cs
│   │       └── UnauthorizedActionException.cs
│   │
│   ├── Blog.Application/               # 🟠 Application Layer — use cases
│   │   ├── Features/
│   │   │   ├── Posts/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreatePost/
│   │   │   │   │   │   ├── CreatePostCommand.cs
│   │   │   │   │   │   ├── CreatePostCommandHandler.cs
│   │   │   │   │   │   └── CreatePostCommandValidator.cs
│   │   │   │   │   ├── UpdatePost/
│   │   │   │   │   ├── PublishPost/
│   │   │   │   │   ├── ArchivePost/
│   │   │   │   │   └── DeletePost/
│   │   │   │   └── Queries/
│   │   │   │       ├── GetPostBySlug/
│   │   │   │       ├── GetPostList/
│   │   │   │       ├── GetPostsByTag/
│   │   │   │       └── GetPostsByAuthor/
│   │   │   │
│   │   │   ├── Comments/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── AddComment/
│   │   │   │   │   ├── DeleteComment/
│   │   │   │   │   └── ModerateComment/    # Editor/Admin only
│   │   │   │   └── Queries/
│   │   │   │       └── GetCommentsByPost/
│   │   │   │
│   │   │   ├── Reactions/
│   │   │   │   └── Commands/
│   │   │   │       ├── ToggleLike/
│   │   │   │       └── ToggleBookmark/
│   │   │   │
│   │   │   ├── Users/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── UpdateProfile/
│   │   │   │   │   └── AssignRole/         # Admin only
│   │   │   │   └── Queries/
│   │   │   │       ├── GetUserProfile/
│   │   │   │       └── GetUserList/        # Admin only
│   │   │   │
│   │   │   └── Auth/
│   │   │       ├── Commands/
│   │   │       │   ├── Login/
│   │   │       │   ├── Register/
│   │   │       │   ├── RefreshToken/
│   │   │       │   └── RevokeToken/
│   │   │       └── Queries/
│   │   │           └── GetCurrentUser/
│   │   │
│   │   ├── Behaviors/                  # MediatR pipeline behaviors
│   │   │   ├── ValidationBehavior.cs   # FluentValidation
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── AuthorizationBehavior.cs
│   │   │   └── CachingBehavior.cs      # Redis cache-aside
│   │   │
│   │   ├── Abstractions/
│   │   │   ├── ICurrentUserService.cs
│   │   │   ├── IDateTimeService.cs
│   │   │   ├── IEmailService.cs
│   │   │   └── IStorageService.cs
│   │   │
│   │   └── DTOs/
│   │       ├── PostDto.cs
│   │       ├── CommentDto.cs
│   │       └── UserDto.cs
│   │
│   ├── Blog.Infrastructure/            # 🔵 Infrastructure Layer — external concerns
│   │   ├── Persistence/
│   │   │   ├── Configurations/         # IEntityTypeConfiguration<T>
│   │   │   │   ├── PostConfiguration.cs
│   │   │   │   ├── CommentConfiguration.cs
│   │   │   │   └── UserConfiguration.cs
│   │   │   ├── Migrations/             # EF Core 10 migrations
│   │   │   ├── Repositories/           # Concrete repository implementations
│   │   │   │   ├── PostRepository.cs
│   │   │   │   ├── CommentRepository.cs
│   │   │   │   └── UserRepository.cs
│   │   │   └── BlogDbContext.cs
│   │   │
│   │   ├── Identity/
│   │   │   ├── IdentityService.cs      # ASP.NET Identity wrapper
│   │   │   ├── JwtTokenService.cs      # JWT generation & validation
│   │   │   └── CurrentUserService.cs   # ICurrentUserService impl
│   │   │
│   │   ├── Authorization/
│   │   │   ├── Policies/
│   │   │   │   ├── CanPublishPostPolicy.cs
│   │   │   │   ├── CanModerateCommentPolicy.cs
│   │   │   │   └── IsAdminPolicy.cs
│   │   │   └── Requirements/
│   │   │
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs    # Redis 8
│   │   │   └── CacheKeys.cs
│   │   │
│   │   ├── Storage/
│   │   │   └── MinioStorageService.cs  # Image/media upload
│   │   │
│   │   ├── Email/
│   │   │   ├── IEmailSender.cs           # Abstraction — swap provider without changing handlers
│   │   │   ├── PostalEmailService.cs     # Postal (open source, self-hosted) — primary
│   │   │   └── SendGridEmailService.cs   # SendGrid (paid SaaS) — fallback / alternative
│   │   │
│   │   └── Search/
│   │       └── PostgresFullTextSearch.cs  # Phase 1; migrate to Meilisearch (open source) in Phase 3
│   │
│   └── Blog.API/                       # 🟢 Presentation Layer
│       ├── Controllers/
│       │   ├── PostsController.cs
│       │   ├── CommentsController.cs
│       │   ├── ReactionsController.cs
│       │   ├── UsersController.cs
│       │   └── AuthController.cs
│       │
│       ├── Middleware/
│       │   ├── ExceptionHandlingMiddleware.cs
│       │   ├── RateLimitingMiddleware.cs   # Redis-backed per-user rate limit
│       │   │                               # Note: dùng custom thay vì built-in
│       │   │                               # Microsoft.AspNetCore.RateLimiting vì
│       │   │                               # cần distributed rate limit qua Redis
│       │   └── RequestLoggingMiddleware.cs
│       │
│       ├── Extensions/
│       │   ├── ServiceCollectionExtensions.cs
│       │   └── WebApplicationExtensions.cs
│       │
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── appsettings.Production.json

└── tests/
    ├── Blog.UnitTests/                 # Domain + Application unit tests
    │   ├── Domain/
    │   └── Application/
    ├── Blog.IntegrationTests/          # EF Core + Redis + API tests
    │   └── (uses Testcontainers)
    └── Blog.ArchTests/                 # Architecture rule enforcement
        └── (uses NetArchTest)
```

---

## 2.3 Frontend — Public Blog (blog-web)

```
apps/blog-web/                          # Public reader — Next.js 16.1 (SSG/ISR)
│
├── src/
│   ├── app/                            # App Router
│   │   ├── (public)/                   # Route group — no auth required
│   │   │   ├── page.tsx                # Homepage — featured posts
│   │   │   ├── blog/
│   │   │   │   ├── page.tsx            # Post list (ISR, paginated)
│   │   │   │   └── [slug]/
│   │   │   │       ├── page.tsx        # Post detail (SSG + ISR)
│   │   │   │       └── opengraph-image.tsx
│   │   │   ├── tags/
│   │   │   │   └── [tag]/page.tsx
│   │   │   ├── authors/
│   │   │   │   └── [username]/page.tsx
│   │   │   └── search/page.tsx
│   │   │
│   │   ├── (auth)/                     # Route group — login for Reader (comment)
│   │   │   ├── login/page.tsx
│   │   │   └── register/page.tsx
│   │   │
│   │   ├── api/                        # Route handlers
│   │   │   ├── auth/[...nextauth]/route.ts
│   │   │   └── revalidate/route.ts     # On-demand ISR revalidation
│   │   │
│   │   ├── sitemap.ts                  # Dynamic sitemap
│   │   ├── robots.ts
│   │   ├── layout.tsx
│   │   └── not-found.tsx
│   │
│   ├── components/
│   │   ├── ui/                         # shadcn/ui base components
│   │   ├── layout/
│   │   │   ├── Header.tsx
│   │   │   ├── Footer.tsx
│   │   │   └── ThemeToggle.tsx         # Dark/light mode
│   │   ├── post/
│   │   │   ├── PostCard.tsx
│   │   │   ├── PostContent.tsx         # [FIX] Tiptap HTML/JSON renderer — KHÔNG phải MDX
│   │   │   │                           # Tiptap lưu content dạng HTML hoặc ProseMirror JSON,
│   │   │   │                           # KHÔNG phải MDX format. Chọn một trong hai approach:
│   │   │   │                           # Option A — HTML output (đơn giản hơn):
│   │   │   │                           #   sanitize bằng DOMPurify, render qua
│   │   │   │                           #   dangerouslySetInnerHTML={{ __html: sanitized }}
│   │   │   │                           # Option B — JSON output (khuyến nghị):
│   │   │   │                           #   dùng @tiptap/react <EditorContent editor={editor} />
│   │   │   │                           #   với useEditor({ editable: false, content: jsonContent })
│   │   │   │                           # Lý do ưu tiên Option B: portable, tránh XSS,
│   │   │   │                           # dễ extend (highlight, mention, custom nodes)
│   │   │   ├── TableOfContents.tsx
│   │   │   ├── ReadingProgress.tsx
│   │   │   └── RelatedPosts.tsx
│   │   └── comment/
│   │       ├── CommentList.tsx
│   │       ├── CommentForm.tsx
│   │       └── ReactionBar.tsx
│   │
│   ├── lib/
│   │   ├── api/
│   │   │   ├── client.ts               # Typed fetch wrapper
│   │   │   ├── posts.ts
│   │   │   ├── comments.ts
│   │   │   └── reactions.ts
│   │   ├── hooks/
│   │   │   ├── usePost.ts
│   │   │   ├── useComments.ts
│   │   │   └── useReaction.ts
│   │   ├── seo/
│   │   │   ├── metadata.ts             # generateMetadata helpers
│   │   │   └── structured-data.ts      # JSON-LD schemas
│   │   └── utils/
│   │       ├── date.ts
│   │       └── reading-time.ts
│   │
│   ├── types/                          # Generated from OpenAPI (shared-contracts)
│   └── styles/
│       └── globals.css                 # Tailwind v4 — CSS-first config (@theme, @plugin)
│
├── next.config.ts
└── tsconfig.json
# [FIX] tailwind.config.ts đã bị xóa — Tailwind v4 dùng CSS-first config trong globals.css
```

---

## 2.4 Frontend — Admin CMS (blog-admin)

```
apps/blog-admin/                        # CMS Dashboard — Next.js 16.1
│
├── src/
│   ├── app/
│   │   ├── (auth)/
│   │   │   ├── login/page.tsx
│   │   │   └── forgot-password/page.tsx
│   │   │
│   │   ├── (dashboard)/                # Protected — requires authentication
│   │   │   ├── layout.tsx              # Sidebar + top nav layout
│   │   │   ├── page.tsx                # Overview dashboard
│   │   │   │
│   │   │   ├── posts/
│   │   │   │   ├── page.tsx            # Post list — filter by status/author
│   │   │   │   ├── new/page.tsx        # Create post — rich editor
│   │   │   │   └── [id]/
│   │   │   │       ├── edit/page.tsx   # Edit post
│   │   │   │       └── preview/page.tsx
│   │   │   │
│   │   │   ├── comments/
│   │   │   │   └── page.tsx            # Moderation queue — Editor/Admin
│   │   │   │
│   │   │   ├── users/
│   │   │   │   ├── page.tsx            # User list — Admin only
│   │   │   │   └── [id]/page.tsx       # User detail + role assignment
│   │   │   │
│   │   │   ├── tags/page.tsx
│   │   │   ├── analytics/page.tsx      # Author/Admin analytics
│   │   │   └── settings/page.tsx       # Admin only
│   │   │
│   │   └── api/
│   │       └── auth/[...nextauth]/route.ts
│   │
│   ├── components/
│   │   ├── ui/                         # shadcn/ui base
│   │   ├── layout/
│   │   │   ├── Sidebar.tsx
│   │   │   ├── TopNav.tsx
│   │   │   └── Breadcrumb.tsx
│   │   ├── editor/
│   │   │   ├── RichTextEditor.tsx      # Tiptap v3 (stable since 01/2026)
│   │   │   ├── MediaUploader.tsx
│   │   │   ├── TagInput.tsx
│   │   │   └── PublishPanel.tsx        # Status, schedule, meta
│   │   ├── rbac/
│   │   │   ├── ProtectedRoute.tsx      # Redirect if not authenticated
│   │   │   ├── PermissionGate.tsx      # Show/hide by role
│   │   │   └── RoleBadge.tsx
│   │   └── analytics/
│   │       ├── StatsCard.tsx
│   │       ├── ViewsChart.tsx
│   │       └── TopPostsTable.tsx
│   │
│   ├── lib/
│   │   ├── auth/
│   │   │   ├── nextauth.config.ts      # NextAuth v5 config
│   │   │   └── session.ts
│   │   ├── permissions/
│   │   │   ├── ability.ts              # CASL permission definitions (>= 6.8.0 — CVE-2026-1774 fix)
│   │   │   ├── roles.ts                # Role → Permission mapping
│   │   │   └── usePermission.ts        # React hook
│   │   ├── api/
│   │   │   └── client.ts               # Authenticated API client
│   │   └── hooks/
│   │
│   └── types/
│
├── next.config.ts
└── tsconfig.json
# [FIX] tailwind.config.ts đã bị xóa — Tailwind v4 dùng CSS-first config trong globals.css
```

**RBAC Matrix:**

| Permission | Admin | Editor | Author | Reader |
|---|:---:|:---:|:---:|:---:|
| Đọc bài đã publish | ✅ | ✅ | ✅ | ✅ |
| Tạo / sửa bài của mình | ✅ | ✅ | ✅ | ❌ |
| Publish bài | ✅ | ✅ | ❌ | ❌ |
| Xóa bài của người khác | ✅ | ❌ | ❌ | ❌ |
| Duyệt / xóa comment | ✅ | ✅ | ❌ | ❌ |
| Quản lý users & roles | ✅ | ❌ | ❌ | ❌ |
| Xem Analytics | ✅ | ✅ | Own only | ❌ |
| System Settings | ✅ | ❌ | ❌ | ❌ |

---

## 2.5 Shared Libraries

```
libs/
│
├── shared-contracts/                   # TypeScript types, shared across apps
│   ├── src/
│   │   ├── api.types.ts                # Auto-generated from OpenAPI 3.1
│   │   ├── roles.ts                    # Enum: Admin, Editor, Author, Reader
│   │   ├── permissions.ts              # Permission matrix constants
│   │   └── index.ts
│   ├── package.json
│   └── tsconfig.json
│
└── shared-ui/                          # Reusable React component library
    ├── src/
    │   ├── components/
    │   │   ├── Avatar.tsx
    │   │   ├── Badge.tsx
    │   │   ├── Button.tsx
    │   │   └── index.ts
    │   └── index.ts
    ├── package.json
    └── tsconfig.json
```

---

## 2.6 Deploy — Docker & Kubernetes

```
deploy/
│
├── docker/
│   ├── Dockerfile.api                  # Multi-stage: build → runtime (.NET 10)
│   ├── Dockerfile.web                  # Multi-stage: build → runner (Node 24 LTS)
│   ├── Dockerfile.admin                # Multi-stage: build → runner (Node 24 LTS)
│   └── .dockerignore
│
└── k8s/
    ├── base/                           # Base Kubernetes manifests
    │   ├── api-deployment.yaml
    │   ├── api-service.yaml
    │   ├── api-hpa.yaml                # Horizontal Pod Autoscaler — API
    │   ├── web-deployment.yaml
    │   ├── web-service.yaml
    │   ├── web-hpa.yaml                # [FIX] Thêm mới — HPA cho blog-web
    │   ├── admin-deployment.yaml
    │   ├── admin-service.yaml
    │   ├── admin-hpa.yaml              # [FIX] Thêm mới — HPA cho blog-admin
    │   ├── postgres-statefulset.yaml
    │   ├── postgres-pvc.yaml
    │   ├── redis-statefulset.yaml      # [FIX] Đổi từ redis-deployment.yaml → StatefulSet
    │   ├── redis-pvc.yaml              # [FIX] Thêm mới — PVC cho Redis persistence
    │   ├── minio-deployment.yaml       # Object storage (image upload)
    │   ├── ingress.yaml                # Nginx Ingress Controller
    │   ├── cert-manager.yaml           # TLS via Let's Encrypt
    │   └── kustomization.yaml
    │
    └── overlays/
        ├── dev/                        # Local / development cluster
        │   └── kustomization.yaml
        ├── staging/                    # Pre-production environment
        │   ├── kustomization.yaml
        │   └── patch-replicas.yaml
        └── prod/                       # Production environment
            ├── kustomization.yaml
            ├── patch-replicas.yaml     # Min 2 replicas per service
            └── patch-resources.yaml   # CPU/memory limits
```

---

## 2.7 CI/CD — GitHub Actions

```
.github/
└── workflows/
    ├── ci.yml                          # PR checks: lint, test, build
    │   # Triggers: pull_request → main, develop
    │   # Steps: install → lint → unit tests → integration tests → build Docker
    │
    ├── cd-staging.yml                  # Auto-deploy to staging
    │   # Triggers: push → develop
    │   # Steps: build → push GHCR → kubectl apply overlays/staging
    │
    ├── cd-prod.yml                     # Deploy to production (manual approval)
    │   # Triggers: push → main (requires approval)
    │   # Steps: build → push GHCR → kubectl apply overlays/prod → smoke test
    │
    ├── security-scan.yml               # Weekly security audit
    │   # Steps: Trivy image scan + OWASP Dependency Check
    │
    └── gen-types.yml                   # Regenerate TypeScript types from OpenAPI
        # Triggers: blog-api changes merged → auto-PR to update shared-contracts
```

---
