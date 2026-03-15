# Phase 1: Monorepo Foundation + Domain Layer — Tổng quan

**Ngày hoàn thành**: 2026-03-15
**Trạng thái**: ✅ Hoàn thành
**Tổng thời gian thực thi**: ~24 phút
**Tổng số files**: 56 files created/modified

---

## Mục tiêu Phase 1

Dựng **nền móng kỹ thuật** cho toàn bộ dự án. Mọi phase sau đều phụ thuộc vào kết quả của phase này.

Cụ thể phải đạt được:

| # | Tiêu chí | Kết quả |
|---|---------|---------|
| 1 | `docker-compose up` khởi động PostgreSQL 18, Redis 8, MinIO | ✅ Stack defined, ready |
| 2 | `nx build blog-api` thành công, `Blog.ArchTests` pass 0 vi phạm | ✅ 9/9 arch tests pass |
| 3 | EF Core migrations chạy sạch trên PostgreSQL 18 với `unaccent` extension | ✅ 2 migrations generated |
| 4 | `shared-contracts` là `implicitDependencies` trong cả 2 frontend `project.json` | ✅ Configured |
| 5 | Domain aggregates/VOs/events compile không có reference đến Infrastructure | ✅ Zero infra references |

---

## 4 Plans đã thực hiện

```
Phase 1
├── Plan 01-01: Nx Workspace Scaffold + Docker Compose      (8 min, 19 files)
│   Kết quả: Nx 22 monorepo + 9 projects registered + Docker Compose stack
│
├── Plan 01-02: Blog.Domain Layer                           (6 min, 34 files)
│   Kết quả: 4 aggregates + 4 value objects + 12 domain events + 4 repo interfaces + 21 unit tests
│
├── Plan 01-03: Blog.Infrastructure + Blog.API              (7 min, 16 files)
│   Kết quả: BlogDbContext + 6 EF configs + 2 migrations + bare API + migration.sh
│
└── Plan 01-04: Architecture Tests                          (3 min, 2 files)
    Kết quả: 9 arch tests enforcing layer boundaries + domain model integrity
```

---

## Kiến trúc tổng thể sau Phase 1

```
blog-platform/ (Nx 22 workspace)
│
├── apps/
│   ├── blog-api/
│   │   └── src/
│   │       ├── Blog.Domain/           ← THUẦN C#, chỉ MediatR dependency
│   │       │   ├── Common/            ← AggregateRoot<T>, ValueObject, IDomainEvent
│   │       │   ├── Aggregates/        ← Post, Comment, User, Tag
│   │       │   ├── ValueObjects/      ← Slug, Email, ReadingTime, TagReference
│   │       │   ├── DomainEvents/      ← 12 event records
│   │       │   ├── Repositories/      ← 4 interface contracts
│   │       │   └── Exceptions/        ← DomainException
│   │       │
│   │       ├── Blog.Infrastructure/   ← EF Core, Npgsql, migrations
│   │       │   └── Persistence/
│   │       │       ├── BlogDbContext.cs
│   │       │       ├── Configurations/ ← 6 IEntityTypeConfiguration<T> files
│   │       │       └── Migrations/    ← 2 migrations
│   │       │
│   │       └── Blog.API/             ← Minimal startup (MigrateAsync + /healthz)
│   │
│   ├── blog-web/   (Next.js - chưa có app code)
│   └── blog-admin/ (Next.js - chưa có app code)
│
├── libs/
│   ├── shared-contracts/ (chưa có TS types)
│   └── shared-ui/        (chưa có components)
│
├── tests/
│   ├── Blog.ArchTests/   ← 9 architecture tests (ACTIVE)
│   └── Blog.UnitTests/   ← 21 domain unit tests (ACTIVE)
│
└── docker-compose.yml    ← PostgreSQL 18 + Redis 8 + MinIO
```

**Dependency direction** (Clean Architecture):

```
Blog.API
  └── depends on → Blog.Infrastructure
                     └── depends on → Blog.Domain
                                        └── depends on → (nothing external except MediatR)
```

---

## Layer Boundary Rules (enforced từ Phase 1)

| Quy tắc | Test | Trạng thái |
|---------|------|-----------|
| Domain không reference Infrastructure | `Domain_ShouldNot_ReferenceBlogInfrastructure` | ✅ PASS |
| Domain không reference API | `Domain_ShouldNot_ReferenceBlogAPI` | ✅ PASS |
| Infrastructure không reference API | `Infrastructure_ShouldNot_ReferenceBlogAPI` | ✅ PASS |
| Aggregates/VOs không implement MediatR trực tiếp | `Domain_AggregatiesAndValueObjects_ShouldNot_ReferenceMediatRDirectly` | ✅ PASS |
| Value Objects là immutable | `ValueObjects_ShouldBe_Immutable` | ✅ PASS |
| Value Objects kế thừa ValueObject base | `ValueObjects_ShouldInherit_ValueObjectBase` | ✅ PASS |
| Domain Events là `record` types | `DomainEvents_ShouldBe_RecordTypes` | ✅ PASS |
| Aggregate Roots kế thừa AggregateRoot<TId> | `AggregateRoots_ShouldInherit_AggregateRootBase` | ✅ PASS |
| Domain Events implement IDomainEvent | `DomainEvents_ShouldImplement_IDomainEvent` | ✅ PASS |

---

## Database Schema sau Phase 1

7 tables trong PostgreSQL, toàn bộ snake_case:

```
posts              ← Post aggregate root
  ↓ 1:1
post_contents      ← PostContent child entity (BodyJson JSONB + BodyHtml TEXT)

posts
  ↓ 1:many
post_versions      ← PostVersion child entity (append-only snapshots)

posts
  ↓ 1:many (owned)
post_tags          ← TagReference value object collection (composite PK: post_id + tag_id)

comments           ← Comment aggregate root (parent_id nullable = self-referencing)

users              ← User aggregate root (standalone, NO FK to AspNetUsers)

tags               ← Tag aggregate root
```

---

## Những quyết định thiết kế quan trọng nhất

1. **ADR-006: User tách biệt khỏi IdentityUser** — User domain aggregate dùng standalone GUID, không inheritance, không FK. `User.Create(Guid id, ...)` nhận ID từ IdentityUser.

2. **MediatR trong Domain** — Package duy nhất được phép. Cần thiết cho `IDomainEvent : INotification`. Architecture test cho phép chỉ `Blog.Domain.Common` và `Blog.Domain.DomainEvents`.

3. **PostgreSQL 18 volume path** — Mount tại `/var/lib/postgresql` (không phải `/data`) vì PG18 thay đổi PGDATA path.

4. **suppressTransaction cho unaccent** — `CREATE EXTENSION` không chạy được trong transaction block của PostgreSQL.

5. **OwnsMany với CLR property names** — `HasKey("PostId", nameof(TagReference.TagId))` dùng tên CLR, không phải tên column. Dùng tên column gây lỗi "no property type specified".

6. **ValueComparer cho SocialLinks JSONB** — Cần thiết để EF Core detect in-place dictionary mutations trong SaveChanges.

---

## Files chi tiết của từng Plan

| Plan | File tài liệu |
|------|--------------|
| Plan 01-01: Nx Scaffold + Docker | [`plan-01-nx-scaffold.md`](./plan-01-nx-scaffold.md) |
| Plan 01-02: Blog.Domain Layer | [`plan-02-domain-layer.md`](./plan-02-domain-layer.md) |
| Plan 01-03: Blog.Infrastructure + Blog.API | [`plan-03-infrastructure-layer.md`](./plan-03-infrastructure-layer.md) |
| Plan 01-04: Architecture Tests | [`plan-04-architecture-tests.md`](./plan-04-architecture-tests.md) |

---

## Bước tiếp theo: Phase 2

**Phase 2: Infrastructure + Application Pipeline**

Xây dựng trực tiếp trên nền móng Phase 1:

- **Repository implementations**: Implement 4 interfaces (IPostRepository, ICommentRepository, IUserRepository, ITagRepository) bằng EF Core
- **MediatR 4-behavior pipeline**: `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior` theo thứ tự cố định và bất biến
- **IUnitOfWork**: Cross-context transaction wrapper (IdentityDbContext + BlogDbContext dùng cùng 1 connection)
- **Redis ICacheableQuery**: Opt-in caching với Lua script pattern invalidation
- **Testcontainers scaffold**: Integration tests với real PostgreSQL 18 + Redis 8

> Phase 2 là tiền đề để Phase 3 (Authentication + RBAC) có thể implement user registration và login flow.
