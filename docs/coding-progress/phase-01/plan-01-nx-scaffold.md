# Plan 01-01: Nx Workspace Scaffold + Docker Compose

**Thời gian**: ~8 phút (2026-03-15T05:41Z → 05:49Z)
**Trạng thái**: ✅ Hoàn thành
**Commits**: `d995a78` → `a74d858` → `d357359`
**Files tạo mới**: 19 files

---

## Mục tiêu Plan này

Tạo khung xương của toàn bộ monorepo — Nx workspace với tất cả 9 projects đã đăng ký, 5 .NET projects có NuGet packages cần thiết, và Docker Compose stack cho local development.

Khi plan này xong, developer có thể:
- Chạy `nx build blog-api` và Nx biết phải build project nào
- Chạy `docker-compose up -d` và có ngay PostgreSQL 18, Redis 8, MinIO

---

## Tại sao Nx? Tại sao không chỉ dùng folder thường?

Đây là câu hỏi hợp lý. Dự án này có 3 apps (blog-api, blog-web, blog-admin) + 2 libs (shared-contracts, shared-ui) + 4 test projects. Nếu không có Nx:

- Muốn build tất cả phải viết script thủ công
- Khi thay đổi `shared-contracts`, không biết app nào bị ảnh hưởng và cần rebuild
- CI chạy mọi test kể cả những gì không liên quan đến code đã thay đổi

Nx giải quyết 3 vấn đề đó:
- **Unified task runner**: `nx build blog-api`, `nx test blog-unit-tests`
- **Dependency graph**: Nx biết `blog-web` phụ thuộc `shared-contracts` → khi `shared-contracts` thay đổi, Nx tự rebuild `blog-web`
- **Affected detection**: `nx affected:test` chỉ test những gì thực sự bị ảnh hưởng bởi code change

---

## Task 1: Initialize Nx workspace và scaffold .NET projects

**Commit**: `d995a78`

### `package.json`

```json
{
  "name": "blog-platform",
  "version": "0.0.1",
  "devDependencies": {
    "nx": "22.5.4",
    "@nx/dotnet": "22.5.4",
    "@nx/next": "22.5.4"
  }
}
```

**Tại sao Node.js trong project .NET?** Nx là Node.js tool. Nó không compile .NET code — nó chỉ là task runner biết gọi `dotnet build`, `dotnet test` ở đúng thứ tự. `package.json` là điểm khởi đầu bắt buộc của bất kỳ Nx workspace nào.

**Deviation đã xảy ra**: `npx create-nx-workspace@latest .` từ chối vì `.` không phải tên workspace hợp lệ. Fix: `npm init -y && npx nx@latest init --preset=empty --nxCloud=skip`. Kết quả cuối cùng giống nhau.

---

### `nx.json`

```json
{
  "plugins": [
    "@nx/dotnet",
    "@nx/next"
  ]
}
```

**`@nx/dotnet` làm gì?** Plugin này scan tất cả `.csproj` files trong workspace và tự động tạo các Nx targets (build, test, watch) mà không cần config thủ công. Khi bạn chạy `nx build blog-domain`, Nx biết đây là .NET project và gọi `dotnet build`.

**Tại sao KHÔNG dùng `@nx-dotnet/core`?** Package đó đã bị deprecated từ tháng 9/2025. `@nx/dotnet` là official replacement từ Nx team. Dùng package cũ sẽ không nhận được updates và có thể break với Nx version mới.

---

### `BlogPlatform.slnx`

```xml
<Solution>
  <Folder Name="/apps/">
    <Project Path="apps/blog-api/src/Blog.Domain/Blog.Domain.csproj" />
    <Project Path="apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj" />
    <Project Path="apps/blog-api/src/Blog.API/Blog.API.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Blog.ArchTests/Blog.ArchTests.csproj" />
    <Project Path="tests/Blog.UnitTests/Blog.UnitTests.csproj" />
  </Folder>
</Solution>
```

**`.slnx` là gì?** Format XML mới của .NET 10 thay thế `.sln` cũ (text-based format dễ conflict khi merge). Cả hai đều được `dotnet build`, `dotnet test`, và Visual Studio hỗ trợ đầy đủ. Nếu bạn mở bằng VS Code hoặc Rider, nó cũng nhận diện được.

---

### 5 .csproj files

**`Blog.Domain.csproj`** — Chỉ có 1 NuGet dependency:
```xml
<ItemGroup>
  <PackageReference Include="MediatR" Version="12.5.0" />
</ItemGroup>
```

Đây là thiết kế có chủ đích. Domain layer = "trái tim" của ứng dụng. Nó phải không phụ thuộc vào bất kỳ framework nào. MediatR được phép duy nhất vì `IDomainEvent` cần implement `MediatR.INotification` để sau này có thể dùng `INotificationHandler`.

**`Blog.Infrastructure.csproj`** — Tất cả database dependencies:
```xml
<ItemGroup>
  <ProjectReference Include="../Blog.Domain/Blog.Domain.csproj" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.5" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.1" />
  <PackageReference Include="EFCore.NamingConventions" Version="10.0.1" />
</ItemGroup>
```

`EFCore.NamingConventions` cung cấp `.UseSnakeCaseNamingConvention()` — tự động chuyển `CoverImageUrl` thành `cover_image_url` trong database.

**`Blog.API.csproj`** — Layer ngoài cùng, reference tất cả:
```xml
<ItemGroup>
  <ProjectReference Include="../Blog.Infrastructure/Blog.Infrastructure.csproj" />
  <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.*" />
</ItemGroup>
```

**`Blog.ArchTests.csproj`** — Test project kiểm tra architectural rules:
```xml
<ItemGroup>
  <ProjectReference Include="../../apps/blog-api/src/Blog.Domain/Blog.Domain.csproj" />
  <ProjectReference Include="../../apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj" />
  <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
  <PackageReference Include="FluentAssertions" Version="6.*" />
  <PackageReference Include="xunit" Version="2.*" />
</ItemGroup>
```

---

## Task 2: Register all projects in Nx graph

**Commit**: `a74d858`

### `project.json` files — cách Nx nhận biết một project

Mỗi project trong monorepo cần 1 file `project.json` nằm cạnh `.csproj`. File này làm 2 việc:
1. Đăng ký project với tên và tags vào Nx graph
2. Khai báo dependencies mà Nx không thể tự detect

**`apps/blog-api/src/Blog.Domain/project.json`**:
```json
{
  "name": "blog-domain",
  "tags": ["scope:backend", "type:domain"],
  "targets": {}
}
```

> `targets: {}` là trống vì `@nx/dotnet` plugin tự động infer build/test targets từ `.csproj`. Không cần config thủ công.

Tags không bắt buộc nhưng hữu ích — bạn có thể dùng `nx run-many --projects=tag:scope:backend` để chạy tất cả backend projects.

**`apps/blog-web/project.json`** và **`apps/blog-admin/project.json`** — điểm quan trọng:
```json
{
  "name": "blog-web",
  "tags": ["scope:frontend", "type:app"],
  "implicitDependencies": ["shared-contracts"]
}
```

`implicitDependencies` nói với Nx: *"Dù không có import trực tiếp trong code, khi `shared-contracts` thay đổi, hãy coi `blog-web` như bị ảnh hưởng."*

Tại sao cần điều này? Vì TypeScript types trong `shared-contracts` được generate từ OpenAPI spec (script `gen-types.sh`). Khi API contract thay đổi, bạn regenerate types và Nx phải biết rebuild frontend apps. Nx không đọc được shell scripts để detect dependency — `implicitDependencies` là cách khai báo tường minh.

---

## Task 3: Docker Compose stack

**Commit**: `d357359`

### `docker-compose.yml` — phân tích từng service

**PostgreSQL 18:**
```yaml
postgres:
  image: postgres:18
  container_name: blog-postgres
  ports:
    - "5432:5432"
  environment:
    POSTGRES_USER: blog
    POSTGRES_PASSWORD: blog
    POSTGRES_DB: blog_db
  volumes:
    - postgres-data:/var/lib/postgresql          # ← QUAN TRỌNG: /var/lib/postgresql, không phải /data
    - ./docker/init.sql:/docker-entrypoint-initdb.d/init.sql:ro
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U blog -d blog_db"]
    interval: 10s
    timeout: 5s
    retries: 5
```

**Volume path `/var/lib/postgresql` — tại sao không phải `/var/lib/postgresql/data`?**

PostgreSQL 18 thay đổi `PGDATA` internal path. Nếu bạn mount `/var/lib/postgresql/data` (cách dùng cho PG17 trở về), Docker sẽ tạo volume đúng nhưng khi container restart, PostgreSQL 18 tìm data ở path khác và khởi động như fresh database — **mất toàn bộ data**. Mount ở `/var/lib/postgresql` (parent directory) để bao phủ cả path mới của PG18.

**`docker/init.sql` được mount vào `docker-entrypoint-initdb.d/`:**
```sql
CREATE EXTENSION IF NOT EXISTS unaccent;
```

PostgreSQL tự động chạy tất cả scripts trong thư mục đó khi khởi động lần đầu (khi volume còn trống). File này enable `unaccent` extension — cần thiết cho Phase 8 (full-text search tiếng Việt). Enable từ sớm để migration sau này có thể dùng mà không cần thêm step.

**Redis 8:**
```yaml
redis:
  image: redis:8
  container_name: blog-redis
  ports:
    - "6379:6379"
  healthcheck:
    test: ["CMD", "redis-cli", "ping"]
```

Đơn giản nhất trong stack. Không cần config đặc biệt cho Phase 1. Phase 2 sẽ dùng Redis cho cache-aside pattern.

**MinIO (object storage cho media files):**
```yaml
minio:
  image: quay.io/minio/minio:latest
  container_name: blog-minio
  ports:
    - "9000:9000"   # API port
    - "9001:9001"   # Console UI port
  command: ["server", "--console-address", ":9001", "/data"]
  environment:
    MINIO_ROOT_USER: minio
    MINIO_ROOT_PASSWORD: minio123
```

MinIO là S3-compatible object storage. Dự án dùng nó để store images và media files (Phase 7). Port 9000 là API (dùng trong code), port 9001 là web console (dùng để browse files thủ công qua browser).

**`minio-init` — one-shot init container:**
```yaml
minio-init:
  image: quay.io/minio/mc:latest
  container_name: blog-minio-init
  depends_on:
    minio:
      condition: service_started
  restart: on-failure
  entrypoint: >
    /bin/sh -c "
    sleep 5;
    /usr/bin/mc alias set local http://minio:9000 minio minio123;
    /usr/bin/mc mb local/blog-media --ignore-existing;
    echo 'MinIO bucket blog-media ready.';
    exit 0;
    "
```

Container này chạy `mc` (MinIO client), tạo bucket `blog-media`, rồi **exit**. Nó sẽ không ở trạng thái "running" sau khi xong — đó là hành vi đúng của init container. `restart: on-failure` đảm bảo nếu MinIO chưa ready khi `minio-init` bắt đầu, container sẽ thử lại.

**Tại sao tạo bucket bằng init container thay vì trong application startup?** 2 lý do:
1. Separation of concerns — infrastructure setup không nên nằm trong application code
2. Application chạy với ít quyền hơn admin user — tạo bucket trong app sẽ cần credentials có quyền cao hơn cần thiết

---

## Cách verify sau khi chạy

```bash
# Bước 1: Start stack
docker-compose up -d

# Bước 2: Kiểm tra tất cả services running
docker-compose ps
# Expected: blog-postgres (running), blog-redis (running), blog-minio (running)
# blog-minio-init sẽ ở trạng thái "Exited (0)" — đây là ĐÚNG

# Bước 3: Verify unaccent extension
docker exec blog-postgres psql -U blog -d blog_db \
  -c "SELECT extname FROM pg_extension WHERE extname='unaccent';"
# Expected: 1 row

# Bước 4: Verify Nx graph
npx nx show project blog-web --json | grep implicitDependencies
# Expected: ["shared-contracts"]

# Bước 5: Verify .NET build
dotnet build BlogPlatform.slnx
# Expected: Build succeeded. 0 Error(s)
```

---

## Tóm tắt: những thứ Plan 01-01 cung cấp cho các plans sau

| Plan phụ thuộc | Điều Plan 01-01 đã chuẩn bị |
|---------------|----------------------------|
| Plan 01-02 (Domain) | `.csproj` với MediatR package đã có |
| Plan 01-03 (Infrastructure) | `.csproj` với EF Core packages đã có; PostgreSQL đang chạy |
| Plan 01-04 (ArchTests) | NetArchTest package đã có; Nx test runner đã configured |
| Phase 2+ | Docker Compose stack sẵn sàng; Nx graph structure đã đúng |
