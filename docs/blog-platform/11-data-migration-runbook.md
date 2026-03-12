# Data Migration Runbook

## 11.1 EF Core Migration Workflow

**Migration folder:** `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/`
**DbContext:** `BlogDbContext` (Blog.Infrastructure)
**Tool:** `dotnet ef` CLI (EF Core 10)

**Quy trình tạo migration mới:**

```bash
# 1. Chuyển đến thư mục API project
cd apps/blog-api/src/Blog.API

# 2. Tạo migration mới
#    --project: Infrastructure project chứa DbContext
#    --startup-project: API project (có DI container)
dotnet ef migrations add <MigrationName> \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --output-dir Persistence/Migrations

# 3. Review migration files TRƯỚC KHI apply
#    ⚠️ LUÔN review cả Up() và Down() methods
#    Kiểm tra: destructive changes, data loss, index creation

# 4. Apply migration vào local database
dotnet ef database update \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj

# 5. Generate SQL script (cho production deployment)
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project ../Blog.Infrastructure/Blog.Infrastructure.csproj \
  --startup-project Blog.API.csproj \
  --idempotent \
  --output migrations.sql
```

**Script tiện ích (`scripts/migration.sh`):**

```bash
#!/bin/bash
# Usage: ./scripts/migration.sh <command> [migration-name]
# Commands: add, apply, script, status, rollback

set -euo pipefail

API_PROJECT="apps/blog-api/src/Blog.API/Blog.API.csproj"
INFRA_PROJECT="apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj"
MIGRATIONS_DIR="Persistence/Migrations"

case "${1:-}" in
  add)
    [[ -z "${2:-}" ]] && echo "Usage: migration.sh add <MigrationName>" && exit 1
    dotnet ef migrations add "$2" \
      --project "$INFRA_PROJECT" \
      --startup-project "$API_PROJECT" \
      --output-dir "$MIGRATIONS_DIR"
    echo "✓ Migration '$2' created. Review before applying."
    ;;

  apply)
    echo "Applying pending migrations..."
    dotnet ef database update \
      --project "$INFRA_PROJECT" \
      --startup-project "$API_PROJECT"
    echo "✓ Database updated."
    ;;

  script)
    FROM="${2:-0}"
    TO="${3:-}"
    dotnet ef migrations script "$FROM" "$TO" \
      --project "$INFRA_PROJECT" \
      --startup-project "$API_PROJECT" \
      --idempotent \
      --output migrations.sql
    echo "✓ SQL script generated: migrations.sql"
    ;;

  status)
    dotnet ef migrations list \
      --project "$INFRA_PROJECT" \
      --startup-project "$API_PROJECT"
    ;;

  rollback)
    [[ -z "${2:-}" ]] && echo "Usage: migration.sh rollback <TargetMigration>" && exit 1
    echo "⚠️  Rolling back to migration: $2"
    dotnet ef database update "$2" \
      --project "$INFRA_PROJECT" \
      --startup-project "$API_PROJECT"
    echo "✓ Rolled back to '$2'."
    ;;

  *)
    echo "Usage: migration.sh {add|apply|script|status|rollback} [args]"
    exit 1
    ;;
esac
```

---

## 11.2 Migration Naming Convention

**Format:** `YYYYMMDDHHMMSS_<DescriptiveName>`

EF Core tự thêm timestamp prefix. Tên migration phải mô tả rõ thay đổi:

| Pattern | Ví dụ | Khi nào dùng |
|---|---|---|
| `Add<Entity>` | `AddPostVersionsTable` | Tạo bảng mới |
| `Add<Column>To<Table>` | `AddCoverImageUrlToPosts` | Thêm cột |
| `Remove<Column>From<Table>` | `RemoveIsDeletedFromPosts` | Xóa cột |
| `Rename<Entity>` | `RenameTagsSlugColumn` | Đổi tên |
| `AddIndex<Name>` | `AddIndexPostsPublishedAt` | Thêm index |
| `Alter<Column>In<Table>` | `AlterExcerptLengthInPosts` | Thay đổi kiểu/constraint |
| `Seed<Data>` | `SeedDefaultRoles` | Data seeding |
| `Create<Extension>` | `CreateUnaccentExtension` | PostgreSQL extension |

**Ví dụ migration history:**

```
20260115080000_InitialCreate
20260120100000_AddPostContentTable
20260125140000_AddPostVersionsTable
20260201090000_CreateUnaccentExtension
20260201090100_AddFtsIndexToPosts
20260210110000_AddIsApprovedToComments
20260215150000_AddBookmarksTable
20260301080000_SeedDefaultAdminUser
```

---

## 11.3 Pre-deployment Checklist

Trước khi apply migration lên **Staging** hoặc **Production**, kiểm tra:

**1. Review SQL Script:**

```bash
# Generate idempotent SQL script
./scripts/migration.sh script <last-applied> <new-migration>

# Review script — kiểm tra:
# ✅ Không có DROP TABLE / DROP COLUMN chứa data quan trọng
# ✅ Không có ALTER COLUMN làm mất data (ví dụ: VARCHAR(256) → VARCHAR(64))
# ✅ Index creation dùng CONCURRENTLY (nếu bảng lớn)
# ✅ Down() method hoạt động đúng (rollback path)
```

**2. Backward Compatibility:**

| Thay đổi | Backward compatible? | Cách xử lý |
|---|---|---|
| Thêm cột nullable | ✅ Yes | Deploy migration trước, code sau |
| Thêm cột NOT NULL + default | ✅ Yes | Đảm bảo default value hợp lý |
| Xóa cột | ❌ No | 2-phase: (1) code ngừng dùng cột → deploy, (2) migration xóa cột |
| Rename cột | ❌ No | 2-phase: (1) thêm cột mới + copy data, (2) xóa cột cũ |
| Thay đổi kiểu cột | ⚠️ Depends | Test kỹ data conversion, có thể cần data migration |
| Thêm index | ✅ Yes | Dùng `CREATE INDEX CONCURRENTLY` cho bảng lớn |
| Xóa index | ✅ Yes | Đảm bảo không có query phụ thuộc vào index |

**3. Large Table Migration (> 1M rows):**

```sql
-- ⚠️ EF Core KHÔNG hỗ trợ CONCURRENTLY trong migration
-- Phải viết raw SQL cho bảng lớn:

-- Trong migration Up() method:
migrationBuilder.Sql(@"
    CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_posts_new_column
    ON posts (new_column);
");

-- Lưu ý: CONCURRENTLY không thể chạy trong transaction
-- → Cần set Migration.SuppressTransaction = true cho migration này
```

**4. Data Migration (khi cần transform data):**

```csharp
// Trong migration Up() — SAU KHI schema change
migrationBuilder.Sql(@"
    UPDATE posts
    SET reading_time_minutes = GREATEST(1, CEIL(
        (SELECT length(body_html) FROM post_contents WHERE post_id = posts.id) / 1500.0
    ))
    WHERE reading_time_minutes IS NULL;
");
```

---

## 11.4 Rollback Procedures

**Nguyên tắc:** Mọi migration PHẢI có `Down()` method hoạt động đúng.

**Rollback trên Local/Staging:**

```bash
# Rollback về migration cụ thể
./scripts/migration.sh rollback <TargetMigrationName>

# Ví dụ: rollback AddBookmarksTable
./scripts/migration.sh rollback AddPostVersionsTable
# → Sẽ rollback tất cả migrations SAU AddPostVersionsTable
```

**Rollback trên Production (manual SQL):**

```bash
# 1. Generate rollback SQL script
dotnet ef migrations script <CurrentMigration> <TargetMigration> \
  --project "$INFRA_PROJECT" \
  --startup-project "$API_PROJECT" \
  --idempotent \
  --output rollback.sql

# 2. Review rollback script cẩn thận
# ⚠️ Kiểm tra có mất data không (DROP COLUMN, DROP TABLE)

# 3. Backup database TRƯỚC KHI rollback
pgbackrest --stanza=blog-db --type=incr backup

# 4. Apply rollback script
psql -h $DB_HOST -U $DB_USER -d blog_db -f rollback.sql

# 5. Verify
psql -c "SELECT * FROM __EFMigrationsHistory ORDER BY migration_id DESC LIMIT 5;"
```

**Emergency Rollback — PITR (khi migration gây data corruption):**

```bash
# Sử dụng Point-in-Time Recovery (Section 7.4)
# Restore database đến thời điểm TRƯỚC migration

pgbackrest --stanza=blog-db \
  --type=time \
  --target="<timestamp-before-migration>" \
  restore

# ⚠️ Cần redeploy API code tương ứng với schema cũ
```

---

## 11.5 CI/CD Integration

**Migration validation trong CI pipeline (`ci.yml`):**

```yaml
# Thêm vào .github/workflows/ci.yml
migration-check:
  name: Validate EF Core Migrations
  runs-on: ubuntu-latest
  services:
    postgres:
      image: postgres:18
      env:
        POSTGRES_DB: blog_db_test
        POSTGRES_USER: test
        POSTGRES_PASSWORD: test
      ports:
        - 5432:5432
      options: >-
        --health-cmd pg_isready
        --health-interval 10s
        --health-timeout 5s
        --health-retries 5

  steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Install EF Core tools
      run: dotnet tool install --global dotnet-ef

    - name: Check for pending model changes
      run: |
        dotnet ef migrations has-pending-model-changes \
          --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
          --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj
      env:
        ConnectionStrings__DefaultConnection: "Host=localhost;Database=blog_db_test;Username=test;Password=test"

    - name: Apply all migrations (validate Up)
      run: |
        dotnet ef database update \
          --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
          --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj
      env:
        ConnectionStrings__DefaultConnection: "Host=localhost;Database=blog_db_test;Username=test;Password=test"

    - name: Rollback all migrations (validate Down)
      run: |
        dotnet ef database update 0 \
          --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
          --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj
      env:
        ConnectionStrings__DefaultConnection: "Host=localhost;Database=blog_db_test;Username=test;Password=test"

    - name: Re-apply all migrations (idempotency check)
      run: |
        dotnet ef database update \
          --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
          --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj
      env:
        ConnectionStrings__DefaultConnection: "Host=localhost;Database=blog_db_test;Username=test;Password=test"

    - name: Generate SQL script artifact
      run: |
        dotnet ef migrations script \
          --project apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj \
          --startup-project apps/blog-api/src/Blog.API/Blog.API.csproj \
          --idempotent \
          --output migrations.sql

    - name: Upload migration SQL
      uses: actions/upload-artifact@v4
      with:
        name: migration-sql
        path: migrations.sql
```

**Production Deployment Flow:**

```
┌──────────────┐     ┌──────────────────┐     ┌───────────────┐     ┌──────────────┐
│  PR Merged   │────►│  CI: Migration   │────►│  Staging:     │────►│  Production: │
│  to main     │     │  Validation      │     │  Auto-apply   │     │  Manual      │
│              │     │                  │     │  + Smoke test │     │  Approval    │
│              │     │  • has-pending   │     │               │     │              │
│              │     │  • apply (Up)    │     │  cd-staging   │     │  cd-prod     │
│              │     │  • rollback (Down)│    │  workflow     │     │  workflow    │
│              │     │  • re-apply      │     │               │     │              │
└──────────────┘     └──────────────────┘     └───────────────┘     └──────────────┘
```

**Lưu ý quan trọng:**

- **Staging** luôn apply migration tự động khi deploy — đây là môi trường validate
- **Production** yêu cầu **manual approval** trước khi apply migration
- Migration SQL script được attach vào PR dưới dạng artifact — reviewer phải đọc trước khi approve
- Mọi migration destructive (DROP, ALTER data type) cần **2 reviewers** approve

---
