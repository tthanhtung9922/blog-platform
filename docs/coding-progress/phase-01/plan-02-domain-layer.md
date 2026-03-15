# Plan 01-02: Blog.Domain Layer

**Thời gian**: ~6 phút (2026-03-15T05:52Z → 05:58Z)
**Trạng thái**: ✅ Hoàn thành
**Commits**: `0ff49eb` (base classes + value objects) → `6eea61b` (aggregates + events + repo interfaces)
**Files tạo mới**: 33 source files + 1 file sửa = 34 files

---

## Mục tiêu Plan này

Xây dựng toàn bộ **Blog.Domain** — lớp trong cùng của Clean Architecture, không có dependency nào ngoại trừ MediatR. Khi plan này xong:

- 4 Aggregate Roots: `Post`, `Comment`, `User`, `Tag`
- 4 Value Objects: `Slug`, `Email`, `ReadingTime`, `TagReference`
- 12 Domain Event records
- 4 Repository interfaces (contracts, chưa có implementation)
- 21 unit tests xanh

---

## Bức tranh tổng thể: DDD là gì và tại sao?

**Domain-Driven Design (DDD)** là cách tổ chức code theo ngôn ngữ và khái niệm của business domain, thay vì theo cấu trúc kỹ thuật (database tables, API endpoints...).

Thay vì nghĩ: *"Tôi cần bảng `posts` và bảng `post_tags`"*
DDD nghĩ: *"Tôi cần `Post` aggregate có thể thêm Tags, và mỗi thay đổi tạo ra Domain Events"*

**Tại sao điều này quan trọng trong dự án này?**
- Business rules (ví dụ: "chỉ Draft post mới được Publish") sống trong Domain, không bị rải rác ở controller hay service
- Code trong Domain không phụ thuộc framework → dễ test, dễ thay đổi infrastructure
- Junior có thể đọc `post.Publish()` và hiểu business rule mà không cần hiểu EF Core

---

## Phần 1: Base Classes (Common/)

### `Blog.Domain/Common/IDomainEvent.cs`

```csharp
namespace Blog.Domain.Common;

public interface IDomainEvent : MediatR.INotification { }
```

File nhỏ nhất trong dự án nhưng quan trọng bậc nhất. Chỉ 1 dòng.

**Phân tích từng thứ:**

`IDomainEvent` — Interface này là "hợp đồng" cho tất cả domain events trong hệ thống. Mọi event đều phải implement interface này.

`: MediatR.INotification` — Kế thừa từ MediatR. Điều này có nghĩa: mọi `IDomainEvent` tự động là một `MediatR.INotification` và có thể được dispatch bởi `IMediator.Publish()`. Đây là lý do duy nhất Blog.Domain phụ thuộc vào MediatR.

**Tại sao không dùng trực tiếp `MediatR.INotification` trong các event?** Vì nếu sau này muốn thay MediatR bằng library khác, chỉ cần sửa file này — không cần chạm vào 12 event files.

---

### `Blog.Domain/Common/AggregateRoot.cs`

```csharp
namespace Blog.Domain.Common;

public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Phân tích từng thành phần:**

`abstract class AggregateRoot<TId>` — `abstract` vì không ai nên tạo `AggregateRoot` trực tiếp. `<TId>` là generic type parameter — thông thường là `Guid`, nhưng có thể là `int` hay bất cứ thứ gì. Tất cả 4 aggregates trong dự án này đều dùng `Guid`.

`public TId Id { get; protected set; }` — Mọi aggregate root đều có Id. `protected set` cho phép chỉ class con set Id (thông qua `static Create()` factory method).

`private readonly List<IDomainEvent> _domainEvents` — Danh sách events "chờ dispatch". `private` vì không ai ngoài AggregateRoot được thêm event trực tiếp. `readonly` vì list không thể bị replace bằng list mới.

`public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly()` — Expose list ra ngoài nhưng read-only. Code bên ngoài có thể *đọc* events (để dispatch chúng) nhưng không thể *modify* list.

`protected void AddDomainEvent(...)` — `protected` để chỉ aggregate root và subclasses mới gọi được. Subclass gọi `AddDomainEvent(new PostPublishedEvent(Id))` khi business rule được thỏa mãn.

`public void ClearDomainEvents()` — Được gọi sau khi SaveChanges() thành công để clear danh sách. Tại sao `public`? Vì code Infrastructure (DbContext hoặc UnitOfWork) sẽ gọi method này sau khi dispatch.

**Luồng hoạt động trong runtime:**
```
1. post.Publish()           → AddDomainEvent(new PostPublishedEvent(postId))
2. SaveChanges() success    → Infrastructure dispatches DomainEvents
3. ClearDomainEvents()      → List được clear
```

Nếu SaveChanges() fails → events không được dispatch → side effects không xảy ra → consistency được bảo đảm.

---

### `Blog.Domain/Common/ValueObject.cs`

```csharp
namespace Blog.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        if (GetType() != other.GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(1, (current, obj) => HashCode.Combine(current, obj));

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);
}
```

**Vấn đề mà class này giải quyết:**

Trong C#, mặc định `new Slug("hello") == new Slug("hello")` trả về `false` vì `==` so sánh memory address, không phải content. Muốn 2 `Slug` với cùng value được coi là bằng nhau, phải override `Equals` và `GetHashCode`.

`GetEqualityComponents()` là abstract — mỗi Value Object tự định nghĩa "những gì cấu thành equality" của nó. Ví dụ `Slug` trả về `Value`, `Email` trả về `Value`.

`GetType() != other.GetType()` — Kiểm tra này đảm bảo `Email.Create("a@b.com") != Slug.Create("a-b-com")` dù cả hai đều implement `ValueObject`.

`SequenceEqual` so sánh từng component theo thứ tự. Nếu Value Object có nhiều components (ví dụ Money có Amount + Currency), tất cả đều phải bằng nhau.

**Hash code được tính như thế nào?** `Aggregate` fold các components lại bằng `HashCode.Combine`. Điều này đảm bảo nếu `Equals` trả về `true`, `GetHashCode()` của cả hai phải bằng nhau — đây là contract bắt buộc trong C#.

`operator ==` và `!=` — Override để `slug1 == slug2` hoạt động như mong đợi thay vì so sánh reference.

---

### `Blog.Domain/Exceptions/DomainException.cs`

```csharp
namespace Blog.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

Exception chuyên biệt cho business rule violations. Tại sao không dùng `InvalidOperationException` thẳng?

Vì ở Infrastructure/API layer, chúng ta bắt `DomainException` và map nó thành HTTP 422 Unprocessable Entity — "client gửi request hợp lệ nhưng vi phạm business rule." Nếu dùng `InvalidOperationException`, không phân biệt được với lỗi kỹ thuật.

---

## Phần 2: Value Objects

### `Blog.Domain/ValueObjects/Slug.cs`

```csharp
public sealed class Slug : ValueObject
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        var slug = title.ToLowerInvariant();

        // Vietnamese-specific: replace đ before removing diacritics
        slug = slug.Replace("đ", "d").Replace("Đ", "d");

        // Remove diacritics (normalise to form D, then strip combining characters)
        var normalized = slug.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(NormalizationForm.FormC);

        slug = Regex.Replace(slug, @"[^a-z0-9\s\-]", "");
        slug = Regex.Replace(slug, @"[\s\-]+", "-");
        slug = slug.Trim('-');

        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("Title produces an empty slug.", nameof(title));

        return new Slug(slug);
    }

    public static Slug FromExisting(string value) { ... }  // Load từ DB — không transform

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

**Tại sao `sealed`?** Slug không được kế thừa — nó là concrete value object cuối cùng. `sealed` cũng cải thiện performance nhẹ vì compiler biết không có virtual dispatch.

**Tại sao `private Slug(string value)` thay vì `public`?** Buộc người dùng phải đi qua `Slug.Create()` (có validation) hoặc `Slug.FromExisting()` (load từ DB). Không thể tạo Slug với `new Slug("invalid value!!!")`.

**Thuật toán normalize tiếng Việt — từng bước:**

```
Input: "Lập Trình Việt Nam"

Bước 1: ToLowerInvariant()
→ "lập trình việt nam"

Bước 2: Replace đ/Đ → d  (TRƯỚC khi FormD vì đ không decompose được)
→ "lập trình việt nam"  (không có đ trong ví dụ này)

Bước 3: Normalize FormD (tách dấu khỏi ký tự gốc)
"ậ" → "a" + "combining hook below" + "combining grave"
"ề" → "e" + "combining circumflex" + "combining grave"
"ế" → "e" + "combining circumflex" + "combining acute"

Bước 4: Filter NonSpacingMark (bỏ các combining characters)
→ chỉ còn lại "a", "e", "i" (ký tự gốc)

Bước 5: FormC (normalize lại)
→ "lap trinh viet nam"

Bước 6: Remove non-alphanumeric (giữ spaces và dashes)
→ "lap trinh viet nam"

Bước 7: Replace spaces/multiple dashes thành single dash
→ "lap-trinh-viet-nam"

Bước 8: Trim leading/trailing dashes
→ "lap-trinh-viet-nam"  ← Final result
```

**Tại sao xử lý `đ` riêng trước FormD?** Ký tự `đ` (U+0111) là ký tự đặc biệt trong tiếng Việt — nó không có "base character + combining diacritic" form. Unicode FormD không tách được `đ`. Phải replace thủ công trước.

**`FromExisting(string value)` vs `Create(string title)`:**

| Method | Dùng khi nào | Có transform không? |
|--------|-------------|---------------------|
| `Create(title)` | Tạo slug mới từ title bài viết | Có — normalize, lowercase, loại dấu |
| `FromExisting(value)` | Load slug từ database | Không — giả định đã valid sẵn |

EF Core dùng `FromExisting` trong conversion: khi đọc `"lap-trinh-viet-nam"` từ column, nó gọi `Slug.FromExisting("lap-trinh-viet-nam")` chứ không chạy lại thuật toán normalize.

---

### `Blog.Domain/ValueObjects/Email.cs`

```csharp
public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(email));

        return new Email(email.ToLowerInvariant());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

**Regex `^[^@\s]+@[^@\s]+\.[^@\s]+$` làm gì?**
- `^[^@\s]+` — bắt đầu, 1+ ký tự không phải @ và không phải whitespace (phần trước @)
- `@` — literal @ character
- `[^@\s]+\.[^@\s]+` — domain.tld (có ít nhất 1 dấu chấm trong phần domain)
- `$` — kết thúc

Đây là validation đơn giản, không phải RFC 5321 đầy đủ. Đủ cho business domain mà không phức tạp quá mức.

**`ToLowerInvariant()` trước khi store:** `User@Example.COM` → `user@example.com`. Ngăn duplicate accounts cho cùng 1 email address.

---

### `Blog.Domain/ValueObjects/ReadingTime.cs`

```csharp
public sealed class ReadingTime : ValueObject
{
    private const int WordsPerMinute = 250;

    public int Minutes { get; }

    private ReadingTime(int minutes) => Minutes = minutes;

    public static ReadingTime FromWordCount(int wordCount)
    {
        if (wordCount < 0) throw new ArgumentOutOfRangeException(nameof(wordCount));
        var minutes = (int)Math.Ceiling((double)wordCount / WordsPerMinute);
        return new ReadingTime(Math.Max(1, minutes));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minutes;
    }

    public override string ToString() => $"{Minutes} min read";
}
```

**Tại sao 250 words/minute?** Đây là average reading speed theo nhiều nghiên cứu (range thực tế: 200-300 wpm). `const` ở mức class — nếu sau này muốn thay đổi, chỉ sửa 1 chỗ.

**`Math.Max(1, minutes)`** — Đảm bảo minimum là 1 phút. Tránh hiển thị "0 min read" cho bài viết rất ngắn.

**`Math.Ceiling` thay vì `Math.Round`?** 500 words / 250 wpm = 2.0 → 2 phút (đúng). 300 words / 250 wpm = 1.2 → ceiling = 2 phút (conservative estimate — better to say 2 than 1 khi thực tế gần 2).

**Tại sao ReadingTime là Value Object chứ không phải `int`?** Nếu dùng `int`, không thể gọi `post.ReadingTime.Minutes` hay `post.ReadingTime.ToString()` → "2 min read". Value Object đóng gói logic display.

---

### `Blog.Domain/ValueObjects/TagReference.cs`

```csharp
public sealed class TagReference : ValueObject
{
    public Guid TagId { get; }

    private TagReference(Guid tagId) => TagId = tagId;

    public static TagReference Create(Guid tagId)
    {
        if (tagId == Guid.Empty)
            throw new ArgumentException("TagId cannot be empty.", nameof(tagId));
        return new TagReference(tagId);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TagId;
    }
}
```

Value Object đơn giản nhất, nhưng quyết định thiết kế đằng sau nó là quan trọng nhất.

**Tại sao không dùng `Tag` entity trực tiếp trên `Post`?**

Hãy so sánh 2 cách:

```csharp
// Cách A — Navigation property đến Tag entity
public class Post
{
    public ICollection<Tag> Tags { get; } = new List<Tag>();
}

// Cách B — TagReference Value Object (cách chúng ta dùng)
public class Post
{
    public IReadOnlyList<TagReference> Tags { get; }
}
```

Vấn đề với Cách A:
- Khi load Post, EF Core phải load cả Tag entities (hoặc lazy load gây N+1)
- Post aggregate "biết" về Tag aggregate internals
- 2 aggregates trở thành 1 — mất boundary rõ ràng

Với Cách B (TagReference):
- Post chỉ biết "post này có tag với id X" — không cần biết name, slug của tag
- Load Post không cần join tag table
- Khi cần Tag details, dùng ITagRepository riêng

**Equality của TagReference:** `post.Tags.Contains(TagReference.Create(tagId))` hoạt động đúng vì ValueObject override `Equals`.

---

## Phần 3: Aggregate Roots

### `Blog.Domain/Aggregates/Posts/Post.cs`

```csharp
public class Post : AggregateRoot<Guid>
{
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public string? Excerpt { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public PostStatus Status { get; private set; }
    public bool IsFeatured { get; private set; }
    public ReadingTime? ReadingTime { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PostContent? _content;
    public PostContent? Content => _content;

    private readonly List<PostVersion> _versions = new();
    public IReadOnlyList<PostVersion> Versions => _versions.AsReadOnly();

    private readonly List<TagReference> _tags = new();
    public IReadOnlyList<TagReference> Tags => _tags.AsReadOnly();

    private Post() { }  // EF Core materializer

    public static Post Create(Guid authorId, string title, Slug slug) { ... }
    public void UpdateDetails(string title, Slug slug, string? excerpt, string? coverImageUrl) { ... }
    public void SetContent(string bodyJson, string bodyHtml, int wordCount) { ... }
    public void Publish() { ... }
    public void Archive() { ... }
    public void SetFeatured(bool isFeatured) { ... }
    public void AddTag(TagReference tag) { ... }
    public void RemoveTag(Guid tagId) { ... }
}
```

**Pattern: tất cả properties đều `private set`**

Không có property nào có `public set`. Muốn thay đổi Title, phải gọi `post.UpdateDetails(...)`. Tại sao?

1. Kiểm soát side effects — `UpdateDetails()` set `UpdatedAt = DateTimeOffset.UtcNow` và raise `PostUpdatedEvent`. Nếu cho phép `post.Title = "new"` trực tiếp, UpdatedAt sẽ không được cập nhật.
2. Enforce business rules — `Publish()` kiểm tra `Status == Draft` trước khi thay đổi. Nếu `post.Status = PostStatus.Published` trực tiếp, rule bị bypass.

**`private Post() { }` — EF Core materializer constructor**

EF Core cần constructor không tham số để tạo objects khi đọc từ database (process gọi là "materialization"). Constructor này là `private` để ngăn code ứng dụng gọi nó. EF Core có thể truy cập `private` constructor qua reflection.

**`static Post Create(Guid authorId, string title, Slug slug)` — Factory method**

```csharp
public static Post Create(Guid authorId, string title, Slug slug)
{
    if (string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Title cannot be empty.", nameof(title));

    var post = new Post
    {
        Id = Guid.NewGuid(),
        AuthorId = authorId,
        Title = title,
        Slug = slug,
        Status = PostStatus.Draft,          // ← Mọi Post bắt đầu ở Draft
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    post.AddDomainEvent(new PostCreatedEvent(post.Id));
    return post;
}
```

Mọi Post mới đều bắt đầu ở `Draft`. Không thể tạo Published Post trực tiếp — phải qua `Create()` → `Publish()`. Đây là enforcement của business rule "post phải được draft trước khi publish".

**`SetContent(string bodyJson, string bodyHtml, int wordCount)`**

```csharp
public void SetContent(string bodyJson, string bodyHtml, int wordCount)
{
    if (_content is null)
        _content = PostContent.Create(Id, bodyJson, bodyHtml);
    else
        _content.Update(bodyJson, bodyHtml);

    ReadingTime = ReadingTime.FromWordCount(wordCount);
    var versionNumber = _versions.Count + 1;
    _versions.Add(PostVersion.Create(Id, bodyJson, versionNumber));
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

Mỗi lần content được set: (1) PostContent được tạo hoặc update, (2) ReadingTime được tính lại, (3) PostVersion snapshot được thêm vào. 3 thứ này luôn xảy ra cùng nhau — không thể update content mà quên tạo version.

**`Publish()` — Business rule enforcement**

```csharp
public void Publish()
{
    if (Status != PostStatus.Draft)
        throw new DomainException(
            $"Cannot publish a post with status '{Status}'. Only Draft posts can be published.");

    Status = PostStatus.Published;
    PublishedAt = DateTimeOffset.UtcNow;
    UpdatedAt = DateTimeOffset.UtcNow;
    AddDomainEvent(new PostPublishedEvent(Id));
}
```

Rule: chỉ Draft post mới được Publish. Nếu cố Publish một post đã Published → `DomainException`. Sau khi Publish, `PostPublishedEvent` được add vào queue — Phase 2 sẽ handle event này để invalidate cache.

**`AddTag(TagReference tag)` — Idempotent operation**

```csharp
public void AddTag(TagReference tag)
{
    if (!_tags.Any(t => t.TagId == tag.TagId))
        _tags.Add(tag);
}
```

Kiểm tra duplicate trước khi add. Nhờ ValueObject equality, `_tags.Any(t => t.TagId == tag.TagId)` hoạt động đúng.

---

### `Blog.Domain/Aggregates/Posts/PostContent.cs`

```csharp
public class PostContent
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string BodyJson { get; private set; } = string.Empty;
    public string BodyHtml { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    private PostContent() { }  // EF Core materializer

    internal static PostContent Create(Guid postId, string bodyJson, string bodyHtml) => new() { ... };
    internal void Update(string bodyJson, string bodyHtml) { ... }
}
```

**`internal` thay vì `public`** — `PostContent` là child entity, chỉ tồn tại trong context của `Post`. `internal static Create(...)` chỉ được gọi từ code trong cùng assembly (Blog.Domain). Code bên ngoài không thể tạo PostContent mà không đi qua `Post.SetContent()`.

**`BodyJson` vs `BodyHtml`:**
- `BodyJson` — ProseMirror JSON từ Tiptap editor, là "source of truth"
- `BodyHtml` — pre-rendered HTML, tạo server-side từ JSON, dùng cho blog-web SSG/ISR

Tại sao lưu cả hai? Performance. SSG (Static Site Generation) cần HTML sẵn — nếu render JSON thành HTML mỗi lần page load, sẽ chậm. `BodyHtml` được tạo khi save (server-side), không phải khi read.

---

### `Blog.Domain/Aggregates/Posts/PostVersion.cs`

```csharp
public class PostVersion
{
    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string BodyJson { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PostVersion() { }

    internal static PostVersion Create(Guid postId, string bodyJson, int versionNumber) => new() { ... };
}
```

**Append-only design** — PostVersion không có `Update()` method. Không bao giờ sửa version đã tạo. Đây là **immutable audit trail**.

`VersionNumber` được tính từ `_versions.Count + 1` trong Post.SetContent(). Version 1, 2, 3... theo thứ tự chronological.

**Không raise domain event** — Tạo version là internal implementation detail của Post, không phải business event đáng chú ý từ perspective bên ngoài.

---

### `Blog.Domain/Aggregates/Comments/Comment.cs`

```csharp
public class Comment : AggregateRoot<Guid>
{
    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }   // null = top-level; set = 1st-level reply
    public CommentStatus Status { get; private set; }
    // ...

    public static Comment Create(Guid postId, Guid authorId, string body, Guid? parentId = null) { ... }

    public Comment AddReply(Guid authorId, string body)
    {
        if (ParentId.HasValue)
            throw new DomainException("Cannot nest replies more than one level deep.");

        var reply = Create(PostId, authorId, body, parentId: Id);
        AddDomainEvent(new CommentAddedEvent(reply.Id, PostId));
        return reply;
    }

    public void Approve() { Status = CommentStatus.Approved; AddDomainEvent(new CommentApprovedEvent(Id)); }
    public void Reject()  { Status = CommentStatus.Rejected; AddDomainEvent(new CommentRejectedEvent(Id)); }
    public void Delete()  { AddDomainEvent(new CommentDeletedEvent(Id, PostId)); }
}
```

**`Guid? ParentId`** — nullable. `null` = top-level comment. Non-null = reply to another comment. Đây là self-referencing pattern.

**Nesting rule ở Domain level:**
```csharp
if (ParentId.HasValue)
    throw new DomainException("Cannot nest replies more than one level deep.");
```

Nếu comment B là reply của comment A (ParentId = A.Id), thì B.AddReply() sẽ throw. Không thể tạo reply của reply. Rule này được enforce ở Domain — không thể bypass bằng cách gọi repository trực tiếp.

**`CommentStatus.Pending`** — Comment mới luôn ở trạng thái Pending. Phải được moderator Approve trước khi hiển thị public. Editor/Admin có thể gọi `Approve()` hoặc `Reject()`.

**`AddReply()` return `Comment`** — Method trả về reply object mới tạo. Caller cần save reply này thông qua repository riêng.

---

### `Blog.Domain/Aggregates/Users/User.cs`

```csharp
public class User : AggregateRoot<Guid>
{
    public Email Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Website { get; private set; }
    public Dictionary<string, string> SocialLinks { get; private set; } = new();
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The Id parameter is the SAME Guid as the corresponding IdentityUser.Id (ADR-006).
    /// The User domain aggregate and IdentityUser share only this GUID — no inheritance,
    /// no navigation properties, no FK constraints.
    /// </summary>
    public static User Create(Guid id, Email email, string displayName, UserRole role = UserRole.Reader)
    {
        return new User
        {
            Id = id,             // ← Nhận GUID từ ngoài (của IdentityUser)
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Ban()
    {
        if (!IsActive)
            throw new DomainException("User is already banned.");
        IsActive = false;
        AddDomainEvent(new UserBannedEvent(Id));
    }
}
```

**ADR-006: Tại sao User không extends IdentityUser?**

`IdentityUser` là class của ASP.NET Identity — nằm ở `Microsoft.AspNetCore.Identity` namespace, thuộc về Infrastructure layer. Nếu `User` extends `IdentityUser`:

```csharp
// NEVER — vi phạm Clean Architecture
public class User : IdentityUser
{
    public string? Bio { get; set; }
}
```

Thì Domain layer phụ thuộc vào Infrastructure (`Microsoft.AspNetCore.Identity`) — vi phạm dependency direction hoàn toàn. `dotnet test Blog.ArchTests` sẽ fail ngay.

**Shared GUID là gì?** Khi user đăng ký:
1. ASP.NET Identity tạo `IdentityUser` với auto-generated `Id` (Guid)
2. Code lấy `identityUser.Id` đó và truyền vào `User.Create(identityUser.Id, ...)`
3. 2 rows trong 2 tables khác nhau cùng chung 1 Guid — không có FK constraint

Để lookup user domain object từ identity user: `IUserRepository.GetByIdAsync(identityUser.Id)`.

**`Dictionary<string, string> SocialLinks`** — Flexible cho bất kỳ social platform nào: `{"github": "username", "twitter": "handle", "linkedin": "profile"}`. Không cần schema change khi thêm platform mới.

---

### `Blog.Domain/Aggregates/Tags/Tag.cs`

```csharp
public class Tag : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Tag Create(string name, Slug slug)
    {
        var tag = new Tag { Id = Guid.NewGuid(), Name = name, Slug = slug, CreatedAt = DateTimeOffset.UtcNow };
        tag.AddDomainEvent(new TagCreatedEvent(tag.Id));
        return tag;
    }

    public void Update(string name, Slug slug) { Name = name; Slug = slug; }
}
```

Aggregate đơn giản nhất. Lưu ý Tag có cả `Name` (hiển thị: "Lập Trình") và `Slug` (URL: "lap-trinh"). Slug được truyền vào từ ngoài (caller tạo `Slug.Create(name)` trước).

---

## Phần 4: Domain Events

### `DomainEvents/PostEvents.cs`, `CommentEvents.cs`, `UserEvents.cs`, `TagEvents.cs`

```csharp
// PostEvents.cs
public record PostCreatedEvent(Guid PostId) : IDomainEvent;
public record PostPublishedEvent(Guid PostId) : IDomainEvent;
public record PostUpdatedEvent(Guid PostId) : IDomainEvent;
public record PostArchivedEvent(Guid PostId) : IDomainEvent;

// CommentEvents.cs
public record CommentAddedEvent(Guid CommentId, Guid PostId) : IDomainEvent;
public record CommentApprovedEvent(Guid CommentId) : IDomainEvent;
public record CommentRejectedEvent(Guid CommentId) : IDomainEvent;
public record CommentDeletedEvent(Guid CommentId, Guid PostId) : IDomainEvent;

// UserEvents.cs
public record UserProfileUpdatedEvent(Guid UserId) : IDomainEvent;
public record UserBannedEvent(Guid UserId) : IDomainEvent;

// TagEvents.cs
public record TagCreatedEvent(Guid TagId) : IDomainEvent;
public record TagDeletedEvent(Guid TagId) : IDomainEvent;
```

**Tại sao dùng `record` thay vì `class`?**

C# `record` tự động có:
- Immutability (constructor-only initialization)
- Value-based equality (2 records với cùng data bằng nhau)
- Deconstruction support

Domain events là "facts" về quá khứ — chúng không thay đổi. `record` là lựa chọn tự nhiên cho immutable data.

**Naming convention: past tense**

`PostPublishedEvent` — "Post đã được publish" (quá khứ). Không phải `PublishPostEvent` hay `PostPublishEvent`. Domain events mô tả điều đã xảy ra, không phải lệnh cần thực hiện.

**Tại sao event chỉ chứa IDs?**

`PostPublishedEvent(Guid PostId)` — Chỉ có PostId, không có Title, Slug, hay bất kỳ data nào khác.

Vì handler nhận event sẽ tự query thêm data nếu cần. Truyền full data vào event sẽ tạo coupling — nếu Post thêm field mới, phải update event và handler cùng lúc. IDs đủ để handler "tìm kiếm" thêm data từ repository.

**Events sẽ được dùng trong Phase 2 thế nào?**

```csharp
// Phase 2 — INotificationHandler implementations
public class PostPublishedEventHandler : INotificationHandler<PostPublishedEvent>
{
    public async Task Handle(PostPublishedEvent notification, CancellationToken ct)
    {
        // Invalidate cache: post:slug:*, post:id:*, post-list:*
        await _cache.RemoveByPatternAsync("post-list:*");
    }
}
```

---

## Phần 5: Repository Interfaces

### `IPostRepository.cs`

```csharp
public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Post?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<(IReadOnlyList<Post> Items, int TotalCount)> GetPublishedAsync(
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Post post, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task DeleteAsync(Post post, CancellationToken ct = default);
}
```

**`Task<Post?>` — nullable return type**

`GetByIdAsync` trả về `Post?` (nullable). Caller phải xử lý trường hợp `null` (post không tồn tại). Thay vì throw exception, trả về null và để caller quyết định có throw `NotFoundException` hay làm gì khác.

**`GetPublishedAsync` trả về value tuple `(Items, TotalCount)`**

Thay vì `Task<IReadOnlyList<Post>>`, trả về cả danh sách lẫn total count trong một query. Cần thiết cho pagination — phải biết tổng số items để tính total pages. Nếu return chỉ list, phải thực hiện 2 queries (SELECT + COUNT) riêng.

**`CancellationToken ct = default`**

Tham số optional cho phép caller cancel operation (ví dụ: user đóng browser trước khi request hoàn thành). `= default` cho phép gọi mà không cần truyền ct.

### `ICommentRepository.cs`, `IUserRepository.cs`, `ITagRepository.cs`

```csharp
public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetByPostIdAsync(Guid postId, CancellationToken ct = default);
    Task AddAsync(Comment comment, CancellationToken ct = default);
    Task UpdateAsync(Comment comment, CancellationToken ct = default);
    Task DeleteAsync(Comment comment, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tag?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Tag tag, CancellationToken ct = default);
    Task UpdateAsync(Tag tag, CancellationToken ct = default);
    Task DeleteAsync(Tag tag, CancellationToken ct = default);
}
```

**Lưu ý**: `IUserRepository` không có `DeleteAsync` — Users không bị delete, chỉ bị Ban (`IsActive = false`). Đây là soft-delete pattern. Content của user vẫn còn, chỉ account bị disabled.

---

## Phần 6: Unit Tests (21 tests)

Tests viết theo TDD: test trước (RED → build fail), implementation sau (GREEN → all pass).

**Ví dụ test pattern:**

```csharp
// SlugTests.cs
[Theory]
[InlineData("Lập Trình Việt Nam", "lap-trinh-viet-nam")]
[InlineData("Đường dẫn URL", "duong-dan-url")]
[InlineData("  hello  world  ", "hello-world")]
public void Create_WithVietnameseTitle_ProducesCorrectSlug(string input, string expected)
{
    var slug = Slug.Create(input);
    slug.Value.Should().Be(expected);
}

// PostTests.cs
[Fact]
public void Publish_WhenStatusIsPublished_ThrowsDomainException()
{
    var post = Post.Create(Guid.NewGuid(), "Title", Slug.Create("title"));
    post.Publish();  // First publish — should work

    // Second publish — should throw
    var act = () => post.Publish();
    act.Should().Throw<DomainException>()
       .WithMessage("*Only Draft posts can be published*");
}
```

**Tên test theo pattern `Method_Scenario_ExpectedResult`:**
- `Create_WithVietnameseTitle_ProducesCorrectSlug`
- `Publish_WhenStatusIsPublished_ThrowsDomainException`
- `AddReply_WhenCommentIsAlreadyReply_ThrowsDomainException`

Pattern này cho phép đọc test name và hiểu ngay: method gì, trong tình huống nào, kết quả mong đợi là gì.

---

## Câu hỏi tự kiểm tra

1. Nếu bạn muốn thêm field `Language` vào Post, bạn sẽ: (a) thêm trực tiếp `post.Language = "vi"`, hay (b) thêm parameter vào `UpdateDetails()` method? → Tại sao?

2. Giải thích tại sao `private Post() { }` cần tồn tại dù không ai được gọi nó trong application code.

3. `PostVersion` không có `Update()` method. Điều đó có nghĩa gì về design intent của entity này?

4. Tại sao `CommentAddedEvent` có cả `CommentId` lẫn `PostId`, trong khi `CommentApprovedEvent` chỉ có `CommentId`?

5. `GetPublishedAsync` trả về `(Items, TotalCount)` tuple thay vì chỉ `Items`. Nếu trả về chỉ `Items`, vấn đề gì sẽ xảy ra trong pagination UI?
