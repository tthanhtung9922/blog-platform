# Plan 01-03: Blog.Infrastructure + Blog.API

**Thời gian**: ~7 phút (2026-03-15T06:00Z → 06:07Z)
**Trạng thái**: ✅ Hoàn thành
**Commits**: `cf96e1d` (BlogDbContext + configs) → `195db19` (migrations + API + migration.sh)
**Files tạo mới**: 16 files | sửa: 2 files

---

## Mục tiêu Plan này

Kết nối Domain layer với PostgreSQL database. Sau plan này:
- `BlogDbContext` biết cách map tất cả Domain entities sang PostgreSQL tables
- 2 EF Core migrations đã được generated (unaccent extension + full schema)
- `Blog.API` có thể khởi động, tự động apply migrations, và trả về health check

---

## Tại sao cần "Infrastructure" layer riêng?

Hãy nghĩ thế này: Domain layer đã định nghĩa `IPostRepository` với method `GetBySlugAsync(string slug)`. Nhưng ai sẽ thực sự viết SQL để tìm post theo slug?

Infrastructure layer là câu trả lời. Nó chứa:
- **EF Core**: ORM — viết C# code thay vì raw SQL
- **Configurations**: dạy EF Core cách map C# objects ↔ PostgreSQL tables
- **Migrations**: version-controlled database schema changes

Domain không biết EF Core tồn tại. Infrastructure biết cả Domain (để implement interfaces) và EF Core (để thực thi queries).

---

## Task 1: BlogDbContext và 6 Entity Configurations

**Commit**: `cf96e1d`

### `Blog.Infrastructure/Persistence/BlogDbContext.cs`

```csharp
public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostContent> PostContents => Set<PostContent>();
    public DbSet<PostVersion> PostVersions => Set<PostVersion>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlogDbContext).Assembly);
    }
}
```

**`DbSet<T>` là gì?** Mỗi `DbSet` đại diện cho một table trong database. `context.Posts` = table `posts`. Bạn dùng LINQ trên DbSet để query: `context.Posts.Where(p => p.Status == PostStatus.Published)`.

**Tại sao dùng `Set<Post>()` thay vì `DbSet<Post> Posts { get; set; }`?**

Cả 2 đều hoạt động, nhưng `Set<T>()` pattern với expression body property (`=> Set<Post>()`) đảm bảo property luôn trả về cùng 1 DbSet instance từ EF Core's internal state. Pattern cũ với auto-property có thể cause NullReferenceException trong một số edge cases với constructors.

**`ApplyConfigurationsFromAssembly(...)`**

Thay vì đăng ký thủ công từng configuration:
```csharp
// Cách thủ công — phải nhớ thêm mỗi khi tạo entity mới
modelBuilder.ApplyConfiguration(new PostConfiguration());
modelBuilder.ApplyConfiguration(new CommentConfiguration());
// ...
```

`ApplyConfigurationsFromAssembly` scan assembly, tìm tất cả classes implement `IEntityTypeConfiguration<T>`, và apply tất cả tự động. Khi thêm entity mới trong Phase 2+, chỉ cần tạo config file — không cần chạm vào `OnModelCreating`.

---

### `Configurations/PostConfiguration.cs` — Phức tạp nhất

```csharp
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).HasMaxLength(256).IsRequired();

        // Value object conversion: Slug stored as VARCHAR(256)
        builder.Property(p => p.Slug)
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(s => s.Value, v => Slug.FromExisting(v));

        builder.HasIndex(p => p.Slug).IsUnique();

        // Enum stored as string for readability in DB
        builder.Property(p => p.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion<string>();

        // Partial index: only index published posts (performance optimization)
        builder.HasIndex(p => p.Status)
            .HasFilter("status = 'Published'");

        // Value object conversion: ReadingTime stored as int (minutes)
        builder.Property(p => p.ReadingTime)
            .HasConversion(
                r => r == null ? (int?)null : r.Minutes,
                v => v == null ? null : ReadingTime.FromWordCount(v.Value * 250));

        // 1-to-1 relationship with PostContent
        builder.HasOne(p => p.Content)
            .WithOne()
            .HasForeignKey<PostContent>(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-many relationship with PostVersion (append-only snapshots)
        builder.HasMany(p => p.Versions)
            .WithOne()
            .HasForeignKey(v => v.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tag collection: map List<TagReference> VOs to post_tags join table
        builder.OwnsMany(p => p.Tags, tagBuilder =>
        {
            tagBuilder.ToTable("post_tags");
            tagBuilder.WithOwner().HasForeignKey("PostId");
            tagBuilder.Property(t => t.TagId).HasColumnName("tag_id").IsRequired();
            tagBuilder.HasKey("PostId", nameof(TagReference.TagId));
        });
    }
}
```

**Pattern 1: Value Object Conversion (Slug)**

```csharp
builder.Property(p => p.Slug)
    .HasConversion(
        s => s.Value,              // C# → DB: lấy string value từ Slug object
        v => Slug.FromExisting(v)  // DB → C#: tạo Slug từ string trong DB
    );
```

EF Core không biết `Slug` là gì — nó chỉ biết lưu/đọc primitive types. `HasConversion` là "dịch thuật" hai chiều:
- Khi **write**: EF lấy `slug.Value` (string) và lưu vào column
- Khi **read**: EF đọc string từ column và gọi `Slug.FromExisting(v)` để tạo Slug object

Tại sao `FromExisting` thay vì `Create`? Vì `Create` chạy thuật toán normalize (loại dấu, lowercase...). Data trong DB đã được normalize rồi — chạy lại sẽ là wasted work và có thể fail nếu format không match.

**Pattern 2: Enum as String**

```csharp
builder.Property(p => p.Status)
    .HasConversion<string>();
```

Mặc định, EF Core lưu enum là integer (0, 1, 2...). `Draft=0`, `Published=1`, `Archived=2` trong database là khó đọc khi debug SQL.

Với `HasConversion<string>()`, database lưu `"Draft"`, `"Published"`, `"Archived"`. Con người đọc được. SQL query dễ viết: `WHERE status = 'Published'`.

**Pattern 3: Partial Index**

```csharp
builder.HasIndex(p => p.Status)
    .HasFilter("status = 'Published'");
```

Index thông thường (`HasIndex(p => p.Status)`) tạo index trên mọi giá trị của `status` — Draft, Published, Archived.

Partial index với `HasFilter` chỉ index rows có `status = 'Published'`. Vì 99% queries cho end user chỉ cần Published posts, partial index nhỏ hơn (ít rows), nhanh hơn, và tốn ít disk space hơn.

**Pattern 4: OwnsMany cho TagReference collection**

```csharp
builder.OwnsMany(p => p.Tags, tagBuilder =>
{
    tagBuilder.ToTable("post_tags");
    tagBuilder.WithOwner().HasForeignKey("PostId");
    tagBuilder.Property(t => t.TagId).HasColumnName("tag_id").IsRequired();
    tagBuilder.HasKey("PostId", nameof(TagReference.TagId));
    //                ^^^^^^^
    //                CLR property name, không phải column name!
});
```

`OwnsMany` dùng để map một collection of Value Objects sang một separate table. `TagReference` không có own table `tag_references` — nó được "owned" bởi Post và mapped vào table `post_tags`.

**Bug đã gặp và fix**: `HasKey("post_id", "tag_id")` (dùng column names) gây lỗi `"The property 'tag_id' cannot be added to type 'TagReference'... no property type was specified"`. EF Core cần CLR property names trong `HasKey`, không phải column names. Fix: `HasKey("PostId", nameof(TagReference.TagId))`.

Kết quả:

```sql
CREATE TABLE post_tags (
    tag_id UUID NOT NULL,
    post_id UUID NOT NULL,  -- shadow FK property tạo bởi EF
    CONSTRAINT pk_post_tags PRIMARY KEY (post_id, tag_id),
    CONSTRAINT fk_post_tags_posts_post_id FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE
);
```

---

### `Configurations/PostContentConfiguration.cs`

```csharp
public class PostContentConfiguration : IEntityTypeConfiguration<PostContent>
{
    public void Configure(EntityTypeBuilder<PostContent> builder)
    {
        builder.ToTable("post_contents");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BodyJson)
            .HasColumnType("jsonb")    // ← JSONB, không phải json hay text
            .IsRequired();

        builder.Property(c => c.BodyHtml)
            .HasColumnType("text")
            .IsRequired();
    }
}
```

**`jsonb` vs `json` vs `text`:**

| Type | Storage | Indexable | Query | Parse on read |
|------|---------|-----------|-------|--------------|
| `text` | Plain text | No | No JSON ops | No |
| `json` | Plain text | No | Yes (slow) | Every read |
| `jsonb` | Binary format | **Yes** | Yes (fast) | Once on write |

Dùng `jsonb` để sau này có thể tạo GIN index trên nội dung JSON nếu cần. `bodyJson` là ProseMirror JSON — có thể lớn, cần efficient storage.

`bodyHtml` là `text` (không phải `jsonb`) vì nó là HTML string, không cần JSON operations.

---

### `Configurations/PostVersionConfiguration.cs`

```csharp
builder.ToTable("post_versions");
builder.HasKey(v => v.Id);
builder.Property(v => v.BodyJson).HasColumnType("jsonb").IsRequired();
builder.Property(v => v.VersionNumber).IsRequired();

// Covering index for paginated version retrieval by post
builder.HasIndex(v => new { v.PostId, v.CreatedAt });
```

**Composite index `(post_id, created_at)` — tại sao?**

Query phổ biến nhất cho PostVersion: "Lấy tất cả versions của post X, sắp xếp theo thời gian."

```sql
SELECT * FROM post_versions WHERE post_id = $1 ORDER BY created_at DESC;
```

Composite index `(post_id, created_at)` cho phép PostgreSQL dùng Index Scan thay vì Seq Scan — đặc biệt quan trọng khi bài viết có nhiều versions.

---

### `Configurations/CommentConfiguration.cs`

```csharp
builder.ToTable("comments");
builder.HasKey(c => c.Id);
builder.Property(c => c.Body).HasMaxLength(5000).IsRequired();
builder.Property(c => c.Status).HasMaxLength(20).IsRequired().HasConversion<string>();
builder.Property(c => c.ParentId).IsRequired(false);  // nullable
builder.HasIndex(c => new { c.PostId, c.CreatedAt });
```

`IsRequired(false)` cho `ParentId` — explicit nullable declaration. Top-level comments có `ParentId = null`.

Không có FK từ `parent_id` → `id` vì self-referencing FK với ON DELETE CASCADE có thể gây circular dependency issues trong PostgreSQL. Comment deletion được handle ở application level thay vì DB cascade.

---

### `Configurations/UserConfiguration.cs` — Phức tạp nhất vì SocialLinks

```csharp
// SocialLinks stored as JSONB (Dictionary<string,string>)
// ValueComparer là REQUIRED để EF Core detect in-place dictionary mutations
var socialLinksComparer = new ValueComparer<Dictionary<string, string>>(
    // Equality: so sánh JSON serialization của 2 dictionaries
    (a, b) => JsonSerializer.Serialize(a, ...) == JsonSerializer.Serialize(b, ...),
    // HashCode: hash của JSON string
    v => JsonSerializer.Serialize(v, ...).GetHashCode(),
    // Snapshot (deep copy): deserialize → serialize để tạo independent copy
    v => JsonSerializer.Deserialize<Dictionary<string, string>>(
             JsonSerializer.Serialize(v, ...), ...) ?? new Dictionary<string, string>()
);

builder.Property(u => u.SocialLinks)
    .HasColumnType("jsonb")
    .HasConversion(
        d => JsonSerializer.Serialize(d, ...),    // C# Dict → JSON string → JSONB
        v => JsonSerializer.Deserialize<...>(v, ...) ?? new Dictionary<string, string>(),
        socialLinksComparer                        // ← PHẢI có, nếu không mutations bị mất
    );
```

**Tại sao cần `ValueComparer`? (Bug đã gặp)**

EF Core's change tracking hoạt động như sau: khi bạn load entity từ DB, EF lưu một "snapshot" của giá trị ban đầu. Khi `SaveChanges()` được gọi, EF so sánh giá trị hiện tại với snapshot để detect changes.

Vấn đề: với `Dictionary`, mặc định EF so sánh bằng **reference equality** (`object.ReferenceEquals(a, b)`). Nếu bạn làm:

```csharp
user.SocialLinks["twitter"] = "newhandle";  // in-place mutation, cùng reference
```

EF thấy `ReferenceEquals(current, snapshot) == true` → nghĩ chưa có thay đổi → **không update database!**

`ValueComparer` với JSON serialization so sánh **nội dung** thay vì reference. EF sẽ detect rằng `{"twitter":"newhandle"}` khác với `{"twitter":"oldhandle"}` và update đúng.

**3 tham số của `ValueComparer`:**
1. **equals**: Hàm so sánh 2 values
2. **hashCode**: Hash function (phải consistent với equals)
3. **snapshot**: Deep copy function — EF dùng để tạo immutable snapshot. Nếu snapshot là shallow copy và original dict bị mutate, snapshot cũng bị mutate → EF không detect change.

---

### `Configurations/TagConfiguration.cs`

```csharp
builder.ToTable("tags");
builder.HasKey(t => t.Id);
builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
builder.Property(t => t.Slug)
    .HasMaxLength(100)
    .IsRequired()
    .HasConversion(s => s.Value, v => Slug.FromExisting(v));

builder.HasIndex(t => t.Slug).IsUnique();
builder.HasIndex(t => t.Name).IsUnique();
```

Cả `Slug` và `Name` đều có unique index — không thể có 2 tags cùng tên hay cùng slug. EF sẽ throw `DbUpdateException` nếu vi phạm.

---

## Task 2: Migrations + Blog.API + migration.sh

**Commit**: `195db19`

### Migration 1: `20260315060356_CreateUnaccentExtension.cs`

```csharp
public partial class CreateUnaccentExtension : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // suppressTransaction: true là REQUIRED
        // CREATE EXTENSION không thể chạy trong transaction block
        // Nếu thiếu flag này: "ERROR: CREATE EXTENSION cannot run inside a transaction block"
        migrationBuilder.Sql(
            "CREATE EXTENSION IF NOT EXISTS unaccent;",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP EXTENSION IF EXISTS unaccent;",
            suppressTransaction: true);
    }
}
```

**Tại sao phải `suppressTransaction: true`?**

EF Core mặc định wrap toàn bộ migration trong một PostgreSQL transaction:

```sql
BEGIN;
  CREATE TABLE posts (...);
  CREATE INDEX ...;
  -- Nếu có lỗi: ROLLBACK tất cả
COMMIT;
```

Điều này tốt cho DDL thông thường — nếu migration fail giữa chừng, rollback tự động. Nhưng PostgreSQL không cho phép `CREATE EXTENSION` trong transaction block:

```
ERROR: CREATE EXTENSION cannot run inside a transaction block
```

`suppressTransaction: true` bảo EF Core chạy lệnh đó **ngoài** transaction. Migration framework tạo một "save point" trước, chạy lệnh, rồi tiếp tục transaction.

**Tại sao tách thành migration riêng?** Nếu gộp vào `InitialSchema`, khi rollback (`Down()`) sẽ DROP extension và DROP tất cả tables cùng lúc. Tách riêng cho phép rollback schema mà giữ extension, hoặc rollback extension riêng.

**`IF NOT EXISTS` / `IF EXISTS`** — Idempotent SQL. Chạy lại nhiều lần không gây lỗi.

**Bug đã gặp**: Plan ban đầu viết `protected override bool SuppressTransaction => true;` trên Migration class. Nhưng `SuppressTransaction` không phải property của `Migration` base class trong EF Core 10 — nó là property của `SqlOperation`. Fix: dùng `migrationBuilder.Sql(sql, suppressTransaction: true)`.

---

### Migration 2: `20260315060357_InitialSchema.cs`

Migration lớn nhất — tạo tất cả 7 tables. Phân tích các quyết định quan trọng:

**`timestamp with time zone` cho tất cả timestamps:**

```csharp
created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
```

PostgreSQL có 2 loại timestamp:
- `timestamp` (không timezone) — lưu literal datetime, không biết đây là UTC hay local time
- `timestamptz` / `timestamp with time zone` — lưu UTC + convert theo timezone khi đọc

Dùng `DateTimeOffset` trong C# map tới `timestamp with time zone`. Đây là best practice: luôn lưu UTC, convert ở presentation layer. NEVER dùng `DateTime` / `timestamp` — sẽ gây confusion với servers ở timezone khác.

**Tất cả PK đều là `uuid`:**

```csharp
id = table.Column<Guid>(type: "uuid", nullable: false),
```

UUID/GUID thay vì integer auto-increment. Lý do:
- Có thể generate ID ở client side (Domain layer) mà không cần round-trip đến DB
- Không expose sequential IDs (security)
- Safe khi merge data từ multiple sources

**ON DELETE CASCADE cho owned entities:**

```csharp
table.ForeignKey(
    name: "fk_post_contents_posts_post_id",
    column: x => x.post_id,
    principalTable: "posts",
    principalColumn: "id",
    onDelete: ReferentialAction.Cascade);
```

Khi delete Post → tự động delete PostContent, PostVersion, post_tags của post đó. Đây là owned entities — chúng không tồn tại độc lập không có Post.

**`Down()` — rollback capability:**

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(name: "comments");
    migrationBuilder.DropTable(name: "post_contents");
    migrationBuilder.DropTable(name: "post_tags");
    migrationBuilder.DropTable(name: "post_versions");
    migrationBuilder.DropTable(name: "tags");
    migrationBuilder.DropTable(name: "users");
    migrationBuilder.DropTable(name: "posts");
}
```

Mọi migration BẮT BUỘC có `Down()`. Đây là rule không thể bỏ qua. Nếu deploy lên production và có bug, rollback = gọi `Down()` để undo schema changes.

---

### `Blog.API/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Đăng ký BlogDbContext với Npgsql + snake_case naming
builder.Services.AddDbContext<BlogDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

// Health check — verify PostgreSQL connection
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BlogDbContext>("database");

var app = builder.Build();

// Apply migrations khi startup (Phase 1 / local dev pattern)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/healthz");

app.Run();
```

**`.UseSnakeCaseNamingConvention()`**

Plugin `EFCore.NamingConventions` tự động convert:
- C# `CoverImageUrl` → SQL `cover_image_url`
- C# `PublishedAt` → SQL `published_at`
- C# `AuthorId` → SQL `author_id`

Không cần viết `.HasColumnName("cover_image_url")` thủ công cho mỗi property. Convention áp dụng toàn bộ model.

**`MigrateAsync()` trong startup — trade-offs:**

Pros: Developer chạy app lần đầu, database tự động được setup. Không cần manual steps.

Cons:
- Nếu nhiều instances (load balancer), tất cả race để apply cùng 1 migration — có thể gây conflict
- Chậm startup trong production (migration check)
- Không phù hợp với "review migration SQL trước khi apply" workflow

Comment trong code: "Phase 2+ sẽ add resilience/retry for production." Phase 1 ưu tiên developer experience, không phải production readiness.

**`/healthz` endpoint:**

```
GET /healthz → 200 OK với status "Healthy"
               403 khi không kết nối được DB
```

Kubernetes liveness/readiness probes dùng endpoint này để biết pod có healthy không. Cũng dùng khi debug: `curl http://localhost:5000/healthz` để verify DB connection.

---

### `appsettings.json` và `appsettings.Development.json`

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=blog_db;Username=blog;Password=blog"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}

// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"  // ← Log SQL queries
    }
  }
}
```

`Microsoft.EntityFrameworkCore.Database.Command: Information` trong Development cho phép xem SQL queries EF Core generate trong console. Rất hữu ích để debug N+1 query problems hoặc verify index usage.

Connection string trong file config **chỉ cho local dev**. Production credentials đi qua environment variables hoặc secrets — không bao giờ commit vào git.

---

### `scripts/migration.sh`

```bash
#!/bin/bash
# Helper script for EF Core migrations
# Always targets BlogDbContext to avoid two-DbContext ambiguity

CONTEXT="BlogDbContext"
PROJECT="apps/blog-api/src/Blog.Infrastructure"
STARTUP="apps/blog-api/src/Blog.API"

case "$1" in
  add)     dotnet ef migrations add "$2" --context $CONTEXT --project $PROJECT --startup-project $STARTUP ;;
  update)  dotnet ef database update --context $CONTEXT --project $PROJECT --startup-project $STARTUP ;;
  list)    dotnet ef migrations list --context $CONTEXT --project $PROJECT --startup-project $STARTUP ;;
  script)  dotnet ef migrations script "$2" "$3" --idempotent --context $CONTEXT --project $PROJECT --startup-project $STARTUP ;;
  rollback) dotnet ef database update "$2" --context $CONTEXT --project $PROJECT --startup-project $STARTUP ;;
esac
```

**Tại sao cần `--context BlogDbContext`?**

Phase 3 sẽ thêm `IdentityDbContext` cho ASP.NET Identity. Khi có 2 DbContexts trong solution, `dotnet ef migrations add` không biết context nào để dùng và throw lỗi "More than one DbContext was found."

`--context BlogDbContext` specify rõ ràng, tránh ambiguity.

**`--project` vs `--startup-project`:**
- `--project Blog.Infrastructure` — nơi chứa DbContext và Migrations files
- `--startup-project Blog.API` — nơi có Program.cs với dependency injection config. EF tools cần startup project để resolve services (DbContext options, connection string).

**Subcommands:**

| Command | Dùng khi | Ví dụ |
|---------|---------|-------|
| `add <name>` | Thêm migration mới | `./migration.sh add AddTagsTable` |
| `update` | Apply pending migrations lên DB | `./migration.sh update` |
| `list` | Xem migration history | `./migration.sh list` |
| `script <from> <to>` | Generate SQL cho review | `./migration.sh script 0 latest` |
| `rollback <to>` | Rollback đến migration cụ thể | `./migration.sh rollback 20260315060356_CreateUnaccentExtension` |

---

## Database Schema cuối cùng

```sql
-- posts table
posts (
    id UUID PK,
    author_id UUID NOT NULL,
    title VARCHAR(256) NOT NULL,
    slug VARCHAR(256) UNIQUE NOT NULL,
    excerpt VARCHAR(500),
    cover_image_url VARCHAR(2048),
    status VARCHAR(20) NOT NULL,           -- 'Draft' | 'Published' | 'Archived'
    is_featured BOOLEAN DEFAULT false,
    reading_time INTEGER,                   -- minutes (nullable until content set)
    published_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_posts_slug (unique)
INDEX: ix_posts_status WHERE status = 'Published' (partial)

-- post_contents table (1:1 with posts)
post_contents (
    id UUID PK,
    post_id UUID FK → posts(id) CASCADE,
    body_json JSONB NOT NULL,              -- ProseMirror JSON
    body_html TEXT NOT NULL,               -- Pre-rendered HTML
    updated_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_post_contents_post_id (unique — enforces 1:1)

-- post_versions table (1:many with posts, append-only)
post_versions (
    id UUID PK,
    post_id UUID FK → posts(id) CASCADE,
    body_json JSONB NOT NULL,
    version_number INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_post_versions_post_id_created_at

-- post_tags table (owned entity, composite PK)
post_tags (
    tag_id UUID NOT NULL,
    post_id UUID FK → posts(id) CASCADE,
    PRIMARY KEY (post_id, tag_id)
)

-- comments table (self-referencing via parent_id)
comments (
    id UUID PK,
    post_id UUID NOT NULL,
    author_id UUID NOT NULL,
    body VARCHAR(5000) NOT NULL,
    parent_id UUID,                        -- NULL = top-level; set = reply
    status VARCHAR(20) NOT NULL,           -- 'Pending' | 'Approved' | 'Rejected'
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_comments_post_id_created_at

-- users table (standalone, NO FK to AspNetUsers)
users (
    id UUID PK,                            -- same GUID as IdentityUser.Id (ADR-006)
    email VARCHAR(256) UNIQUE NOT NULL,
    display_name VARCHAR(100) NOT NULL,
    bio VARCHAR(500),
    avatar_url VARCHAR(2048),
    website VARCHAR(2048),
    social_links JSONB NOT NULL,           -- Dictionary<string,string>
    role VARCHAR(20) NOT NULL,             -- 'Reader' | 'Author' | 'Editor' | 'Admin'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_users_email (unique)

-- tags table
tags (
    id UUID PK,
    name VARCHAR(100) UNIQUE NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ NOT NULL
)
INDEX: ix_tags_slug (unique), ix_tags_name (unique)
```

---

## Câu hỏi tự kiểm tra

1. `PostContent.BodyJson` dùng column type `jsonb`, còn `PostContent.BodyHtml` dùng `text`. Tại sao không dùng `jsonb` cho cả hai?

2. Tại sao migration `CreateUnaccentExtension` được tạo trước `InitialSchema` và cần `suppressTransaction: true`?

3. Nếu không có `ValueComparer` cho `SocialLinks`, điều gì sẽ xảy ra khi admin update social links của user?

4. `--context BlogDbContext` trong migration.sh sẽ trở nên quan trọng khi nào? (Hint: Phase 3)

5. Tại sao `MigrateAsync()` trong startup là acceptable cho local dev nhưng không acceptable cho production?
