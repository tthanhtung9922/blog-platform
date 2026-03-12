
# Architecture Decisions

## ADR-001: Monorepo với Nx

**Decision:** Sử dụng Nx monorepo thay vì separate repos.
**Rationale:** Team size vừa, FE và BE cần share contracts/types; Nx cung cấp affected build, task caching giúp CI nhanh hơn.
**Consequences:** Cần đầu tư setup Nx remote cache (self-hosted qua S3/MinIO hoặc Nx Cloud nếu budget cho phép) khi repo lớn. **Lưu ý:** .NET project trong Nx yêu cầu cài plugin `@nx-dotnet/core` — không được hỗ trợ out-of-the-box. Cần cấu hình riêng trong `nx.json`.

## ADR-002: Clean Architecture + DDD

**Decision:** Backend theo Clean Architecture với 4 layers rõ ràng; Domain dùng DDD aggregates.
**Rationale:** Tách biệt business logic khỏi infrastructure giúp dễ test và scale; DDD phù hợp với domain Blog có nhiều business rules (publish workflow, permissions).
**Consequences:** Boilerplate nhiều hơn ở giai đoạn đầu; đổi lại maintainability cao.

## ADR-003: 2 Frontend Apps

**Decision:** Tách `blog-web` (public) và `blog-admin` (CMS) thành 2 Next.js apps riêng.
**Rationale:** Khác nhau hoàn toàn về mục đích — `blog-web` tối ưu SSG/SEO, `blog-admin` tối ưu interactivity; tách riêng giúp bundle size nhỏ hơn, deploy độc lập.
**Consequences:** Cần duy trì 2 apps; share code qua `shared-ui` và `shared-contracts`.

## ADR-004: RBAC Strategy

**Decision:** RBAC được enforce ở cả 3 tầng: API (ASP.NET Authorization Policies), Application (MediatR AuthorizationBehavior), và Frontend (CASL + PermissionGate).
**Rationale:** Defense in depth — không phụ thuộc vào một tầng duy nhất.
**Consequences:** Cần sync permission definitions giữa BE và FE qua `permissions.ts` trong shared-contracts.

## ADR-005: Caching Strategy

**Decision:** Cache-aside pattern với Redis 8, được tích hợp qua MediatR `CachingBehavior` ở Application layer.
**Rationale:** Tách biệt caching logic khỏi business logic; `CachingBehavior` cho phép từng Query tự khai báo cache key và TTL mà không cần sửa handler. Redis 8 hỗ trợ persistence (AOF/RDB) và phù hợp cho distributed cache khi scale horizontal.
**Consequences:** Cần quản lý cache invalidation cẩn thận khi Post được update/publish. Cache key convention được tập trung tại `CacheKeys.cs` trong Infrastructure layer.

**Cache Key Convention (`CacheKeys.cs`):**

```csharp
// Post
post:slug:{slug}          // TTL: 1 giờ  — GetPostBySlug
post:id:{id}              // TTL: 1 giờ  — GetPostById (internal)
post-list:page:{p}:size:{s}  // TTL: 5 phút — GetPostList (paginated)
post-list:tag:{tag}:{p}   // TTL: 5 phút — GetPostsByTag
post-list:author:{id}:{p} // TTL: 5 phút — GetPostsByAuthor

// User
user:profile:{username}   // TTL: 30 phút — GetUserProfile

// Comment
comments:post:{postId}:{p} // TTL: 2 phút — GetCommentsByPost (TTL ngắn vì real-time hơn)
```

**Cache Invalidation Map — Domain Event → Cache Keys bị xóa:**

| Domain Event | Cache Keys bị invalidate |
|---|---|
| `PostPublishedEvent` | `post:slug:{slug}`, `post:id:{id}`, `post-list:page:*`, `post-list:tag:*`, `post-list:author:{authorId}:*` |
| `PostUpdatedEvent` | `post:slug:{slug}`, `post:id:{id}` |
| `PostArchivedEvent` | `post:slug:{slug}`, `post:id:{id}`, `post-list:page:*`, `post-list:tag:*`, `post-list:author:{authorId}:*` |
| `CommentAddedEvent` | `comments:post:{postId}:*` |
| `CommentDeletedEvent` | `comments:post:{postId}:*` |
| `UserProfileUpdatedEvent` | `user:profile:{username}` |

**Cơ chế thực thi invalidation:**

- Domain Events được dispatch sau khi `SaveChanges()` thành công (via MediatR `INotificationHandler`)
- Handler tương ứng gọi `IRedisCacheService.RemoveByPatternAsync(pattern)` với wildcard `*`
- Redis 8 hỗ trợ `SCAN` + `DEL` pattern — **không dùng `KEYS *`** trong production (blocking)
- Wildcard invalidation (ví dụ `post-list:*`) dùng Lua script để đảm bảo atomic

---

## ADR-006: ASP.NET Identity vs Domain User Aggregate

**Decision:** `IdentityUser` (ASP.NET Identity) và `User` (Domain Aggregate) là hai model **tách biệt hoàn toàn**, liên kết với nhau chỉ qua `UserId` (shared GUID).

**Phân chia trách nhiệm:**

| | `IdentityUser` | `User` (Domain Aggregate) |
|---|---|---|
| **Layer** | Infrastructure | Domain |
| **Chịu trách nhiệm** | Authentication: password hash, email confirmation, lockout, OAuth provider | Business logic: profile, role behavior, content ownership, permissions |
| **Lưu ở đâu** | Bảng `AspNetUsers` (quản lý bởi EF Identity) | Bảng `Users` (quản lý bởi `UserRepository`) |
| **Ai dùng** | `IdentityService`, `JwtTokenService` | Application layer handlers, Domain services |

**Liên kết giữa hai model:**

- `IdentityUser.Id` == `User.Id` (cùng GUID, tạo đồng thời khi Register)
- Không kế thừa (`User` KHÔNG extends `IdentityUser`)
- Không reference lẫn nhau qua navigation property — chỉ dùng `Guid UserId`

**Quy trình Register (ví dụ):**

```
RegisterCommand
  → IdentityService.CreateAsync(email, password)   // tạo IdentityUser
  → UserRepository.AddAsync(new User(identityId))  // tạo Domain User cùng ID
  → raise UserRegisteredEvent
```

**Rationale:** Tách biệt giúp Domain Layer không phụ thuộc vào ASP.NET Identity. `IdentityUser` có thể thay thế (chuyển sang Keycloak, Auth0) mà không ảnh hưởng domain logic.

**Consequences:**

- Mọi thao tác authentication đi qua `IIdentityService` (Infrastructure)
- Mọi thao tác business logic liên quan user đi qua `IUserRepository` (Domain)
- Khi cần cả hai (ví dụ: Admin ban user), Application layer gọi cả hai service theo thứ tự, bọc trong transaction
- **Xem ADR-007** để biết chi tiết transaction strategy giữa hai DbContext

---

## ADR-007: Transaction Strategy — Register Flow

**Decision:** Sử dụng **shared `DbConnection`** giữa `IdentityDbContext` và `BlogDbContext` để đảm bảo atomicity khi thao tác cross-context (Register, Ban User, v.v.).

**Vấn đề:**
ADR-006 mô tả `IdentityUser` và `User` (Domain) là hai model tách biệt, lưu trong hai bảng khác nhau, có thể dùng hai `DbContext` khác nhau. Khi Register:

```
RegisterCommand
  → IdentityService.CreateAsync(email, password)   // tạo IdentityUser → IdentityDbContext
  → UserRepository.AddAsync(new User(identityId))  // tạo Domain User → BlogDbContext
```

Nếu bước 2 fail, sẽ tạo **orphaned IdentityUser** — user có authentication record nhưng không có domain record.

**Giải pháp — 2 options:**

**Option A — Shared DbConnection (khuyến nghị cho monolith):**

```csharp
// Cả hai DbContext dùng chung connection + transaction
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

var identityContext = new IdentityDbContext(
    new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(connection).Options);
identityContext.Database.UseTransaction(transaction);

var blogContext = new BlogDbContext(
    new DbContextOptionsBuilder<BlogDbContext>()
        .UseNpgsql(connection).Options);
blogContext.Database.UseTransaction(transaction);

// Nếu bất kỳ bước nào fail → cả hai đều rollback
await transaction.CommitAsync();
```

**Option B — Compensating action (cho distributed / microservices):**

```
RegisterCommand
  → IdentityService.CreateAsync(email, password)
  → try: UserRepository.AddAsync(new User(identityId))
  → catch: IdentityService.DeleteAsync(identityId)  // compensate
  → raise UserRegisteredEvent
```

**Rationale:** Option A đảm bảo ACID compliance đầy đủ, phù hợp khi cả hai DbContext dùng cùng một PostgreSQL instance. Option B phù hợp khi chuyển sang microservices (Phase 3+).

**Consequences:**

- Cần tạo `IUnitOfWork` abstraction để wrap shared connection logic
- Tất cả cross-context operations phải đi qua `IUnitOfWork`
- Khi migrate sang microservices, chuyển từ Option A sang Option B (Saga pattern)

---

## ADR-008: Cache Opt-in Mechanism

**Decision:** Chỉ các Query implement interface `ICacheableQuery` mới được cache bởi `CachingBehavior`. Đây là cơ chế **opt-in**, không phải opt-out.

**Vấn đề:**
`CachingBehavior` trong MediatR pipeline áp dụng cho tất cả requests đi qua pipeline. Nếu không có cơ chế opt-in, có thể cache nhầm các queries không nên cache:

- `GetCurrentUser` — phải luôn trả về data real-time
- Queries có side effects hoặc phụ thuộc vào thời gian

**Giải pháp:**

```csharp
// Interface opt-in
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }  // null = dùng default TTL
}

// Ví dụ: GetPostBySlug opt-in caching
public record GetPostBySlugQuery(string Slug) : IRequest<PostDto>, ICacheableQuery
{
    public string CacheKey => $"post:slug:{Slug}";
    public TimeSpan? CacheDuration => TimeSpan.FromHours(1);
}

// CachingBehavior chỉ xử lý khi request implement ICacheableQuery
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, ...)
    {
        if (request is not ICacheableQuery cacheable)
            return await next();  // bypass — không cache

        // Check cache → return cached or execute + store
    }
}
```

**Rationale:** Opt-in an toàn hơn opt-out. Developer phải chủ động khai báo cache key và TTL, giảm rủi ro cache nhầm data nhạy cảm.

**Consequences:**

- Mỗi Query muốn cache phải implement `ICacheableQuery`
- Cache key convention tập trung tại `CacheKeys.cs` nhưng khai báo tại Query level
- `GetCurrentUser`, `GetUserList` (admin) KHÔNG implement `ICacheableQuery`

---

## ADR-009: PostgreSQL FTS & Vietnamese Content

**Decision:** PostgreSQL Full-text Search (Phase 1) cần custom configuration cho nội dung tiếng Việt. Khi search volume tăng (Phase 3), migrate sang Meilisearch (open source) thay vì Elasticsearch (SSPL license).

**Vấn đề:**
PostgreSQL FTS mặc định sử dụng **`simple`** hoặc **`english`** text search configuration. Tiếng Việt là ngôn ngữ **không có dấu cách giữa các từ ghép** (ví dụ: "phát triển" là 2 từ nhưng đã có dấu cách), tuy nhiên cần xử lý:

- **Dấu tiếng Việt** (tonal marks): "phát triển" vs "phat trien"
- **Stopwords** tiếng Việt: "và", "là", "của", "các", v.v.
- **Stemming**: PostgreSQL không có built-in Vietnamese stemmer

**Giải pháp Phase 1 — PostgreSQL FTS with custom config:**

```sql
-- Tạo custom text search configuration cho tiếng Việt
CREATE TEXT SEARCH CONFIGURATION vietnamese (COPY = simple);

-- Thêm unaccent extension để search không dấu
CREATE EXTENSION IF NOT EXISTS unaccent;

-- Tạo custom dictionary với unaccent
ALTER TEXT SEARCH CONFIGURATION vietnamese
  ALTER MAPPING FOR asciiword, asciihword, hword_asciipart, word, hword, hword_part
  WITH unaccent, simple;

-- Index cho bài viết
CREATE INDEX idx_posts_fts ON posts
  USING GIN (to_tsvector('vietnamese', title || ' ' || content));
```

**Giải pháp Phase 3 — Migrate sang Meilisearch:**

- Meilisearch hỗ trợ tiếng Việt out-of-the-box (tokenization, typo tolerance)
- Open source (MIT license), viết bằng Rust, nhẹ và nhanh
- REST API đơn giản, dễ tích hợp
- Self-hosted, không có licensing concern như Elasticsearch (SSPL)

**Rationale:**

- Elasticsearch đã chuyển sang SSPL license (không phải true open source)
- Meilisearch (MIT) và OpenSearch (Apache 2.0) là alternatives thực sự open source
- Meilisearch phù hợp hơn cho blog search use case (search-as-you-type, typo tolerance, faceted search)

**Consequences:**

- Phase 1: Cần tạo `unaccent` extension trên PostgreSQL, maintain custom FTS config
- Phase 3: Cần migration script từ PostgreSQL FTS sang Meilisearch
- Search abstraction (`ISearchService`) phải đủ generic để swap implementation

---
