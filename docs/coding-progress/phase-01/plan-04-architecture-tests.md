# Plan 01-04: Architecture Tests

**Thời gian**: ~3 phút (2026-03-15T06:12Z → 06:14Z)
**Trạng thái**: ✅ Hoàn thành
**Commits**: `7fb0be6`
**Files tạo mới**: 2 files (`LayerBoundaryTests.cs`, `DomainModelIntegrityTests.cs`)

---

## Mục tiêu Plan này

Tạo "guardrails tự động" — tests chạy trên CI và bắt architectural violations ngay khi chúng xảy ra, không phải qua code review 3 ngày sau.

9 tests pass → Phase 1 architecture là sound. Nếu bất kỳ test nào fail, có nghĩa ai đó đã vi phạm Clean Architecture rules.

---

## Tại sao cần Architecture Tests?

Hãy tưởng tượng không có tests này:

```csharp
// Một developer vội vã, viết trong Blog.Domain:
using Microsoft.EntityFrameworkCore; // ← vi phạm clean architecture

public class PostRepository : IPostRepository
{
    private readonly BlogDbContext _ctx;
    // ...
}
```

Code này compile được. Unit tests pass. PR được merge. 3 tháng sau, ai đó mới phát hiện Domain đang phụ thuộc Infrastructure — quá trễ để refactor dễ dàng.

Architecture tests chạy trong CI. Ngay khi PR được tạo, `dotnet test Blog.ArchTests` fail → developer biết ngay mình đã vi phạm rule.

---

## NetArchTest — Library dùng để làm gì?

NetArchTest là library cho phép viết assertions về cấu trúc code theo dạng natural language:

```csharp
Types.InAssembly(DomainAssembly)
    .ShouldNot()
    .HaveDependencyOn("Blog.Infrastructure")
    .GetResult()
    .IsSuccessful;
```

"Các types trong Domain assembly không nên có dependency lên Blog.Infrastructure."

**NetArchTest hoạt động ở IL level** — đọc compiled assembly (file .dll), không phải source code. Nó detect:
- Namespace imports
- Method calls
- Base class references
- Interface implementations
- Attribute usage

Điều này có nghĩa ngay cả **transitive dependencies** (A dùng B, B dùng C) cũng bị detect nếu EF Core types "leak" vào Domain assembly.

---

## File 1: `tests/Blog.ArchTests/LayerBoundaryTests.cs`

4 tests kiểm tra dependency direction giữa layers.

```csharp
public class LayerBoundaryTests
{
    // Assembly anchors — tìm assembly từ known types, không dùng string names
    private static readonly Assembly DomainAssembly = typeof(Post).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(BlogDbContext).Assembly;
```

**Assembly anchor pattern**

Thay vì dùng magic string `"Blog.Domain"` để locate assembly, dùng `typeof(Post).Assembly`. Nếu sau này rename assembly, code sẽ fail to compile (rõ ràng) thay vì test pass silently vì string không match.

---

### Test 1: `Domain_ShouldNot_ReferenceBlogInfrastructure`

```csharp
[Fact]
public void Domain_ShouldNot_ReferenceBlogInfrastructure()
{
    var result = Types.InAssembly(DomainAssembly)
        .ShouldNot()
        .HaveDependencyOn("Blog.Infrastructure")
        .GetResult();

    result.IsSuccessful.Should().BeTrue(
        because: "Domain layer must not reference Infrastructure (dependency direction rule)");
}
```

**Điều gì sẽ trigger test này fail?**

```csharp
// Bất kỳ file nào trong Blog.Domain
using Blog.Infrastructure.Persistence; // ← FAIL
using Blog.Infrastructure.Caching;     // ← FAIL
```

Kể cả nếu import bị comment out, compiler không include nó → test pass. Chỉ fail khi type thực sự được referenced.

---

### Test 2: `Domain_ShouldNot_ReferenceBlogAPI`

Tương tự, nhưng cho API namespace. Domain không bao giờ nên biết về controllers, HTTP, hay ASP.NET.

---

### Test 3: `Infrastructure_ShouldNot_ReferenceBlogAPI`

```csharp
[Fact]
public void Infrastructure_ShouldNot_ReferenceBlogAPI()
{
    var result = Types.InAssembly(InfrastructureAssembly)
        .ShouldNot()
        .HaveDependencyOn("Blog.API")
        .GetResult();
    // ...
}
```

Infrastructure biết về Domain (để implement interfaces) và Application (Phase 2). Nó không nên biết về API layer (controllers, endpoints).

---

### Test 4: `Domain_AggregatiesAndValueObjects_ShouldNot_ReferenceMediatRDirectly`

Đây là test phức tạp nhất, và là nơi có bug cần fix.

```csharp
[Fact]
public void Domain_AggregatiesAndValueObjects_ShouldNot_ReferenceMediatRDirectly()
{
    // Domain dùng MediatR ở chính xác 2 namespaces:
    // 1. Blog.Domain.Common       — IDomainEvent : MediatR.INotification
    // 2. Blog.Domain.DomainEvents — event records implement IDomainEvent
    //
    // Aggregates, Value Objects, Repositories, Exceptions KHÔNG được implement
    // MediatR interfaces trực tiếp.

    var outsideAllowedNamespaces = DomainAssembly
        .GetTypes()
        .Where(t => t.Namespace != null
                    && !t.Namespace.StartsWith("Blog.Domain.Common")
                    && !t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
        .ToList();

    var violations = outsideAllowedNamespaces
        .Where(t =>
            t.GetInterfaces().Any(i => i.Assembly.GetName().Name?.StartsWith("MediatR") == true)
            || (t.BaseType != null && t.BaseType.Assembly.GetName().Name?.StartsWith("MediatR") == true))
        .Select(t => t.FullName ?? t.Name)
        .ToList();

    violations.Should().BeEmpty(
        because: "Aggregates, value objects, repositories, and exceptions must not implement " +
                 "MediatR interfaces directly...");
}
```

**Bug đã gặp và fix: tại sao không dùng NetArchTest cho test này?**

Attempt đầu tiên dùng NetArchTest predicate:

```csharp
// FAILED — NetArchTest API không support pattern này
var result = Types.InAssembly(DomainAssembly)
    .That()
    .ResideInNamespace("Blog.Domain.Common")
    .Should()
    .HaveDependencyOn("MediatR")
    .GetResult();  // ← GetResult() không available trên PredicateList
```

`GetResult()` không available trên kiểu `PredicateList` trả về từ `.Should()` chain — NetArchTest API không hỗ trợ exact pattern "only THESE namespaces may have MediatR dependency."

**Fix: dùng reflection trực tiếp:**

1. Lấy tất cả types OUTSIDE allowed namespaces (Common, DomainEvents)
2. Filter những type nào implement MediatR interface trực tiếp
3. Assert list violations là empty

**Tại sao DomainEvents namespace cũng được phép có MediatR?**

Khi `PostPublishedEvent` implement `IDomainEvent`, ở IL level nó cũng "implement" `MediatR.INotification` (vì `IDomainEvent : MediatR.INotification`). NetArchTest thấy `PostPublishedEvent` có MediatR dependency.

Điều này là **đúng về mặt kiến trúc** — domain events phải implement `IDomainEvent` để được dispatch bởi MediatR. Chỉ có Aggregates, ValueObjects, Repositories không được implement MediatR trực tiếp. Test phản ánh điều đó.

---

## File 2: `tests/Blog.ArchTests/DomainModelIntegrityTests.cs`

5 tests kiểm tra cấu trúc nội tại của Domain model.

### Test 5: `ValueObjects_ShouldBe_Immutable`

```csharp
[Fact]
public void ValueObjects_ShouldBe_Immutable()
{
    var result = Types.InAssembly(DomainAssembly)
        .That()
        .ResideInNamespace("Blog.Domain.ValueObjects")
        .And()
        .AreClasses()
        .Should()
        .BeImmutable()
        .GetResult();

    result.IsSuccessful.Should().BeTrue(
        because: "Value objects must be immutable — all properties must use { get; } with no public setter");
}
```

**`BeImmutable()` kiểm tra gì?** NetArchTest kiểm tra rằng không có property nào có `public set` accessor. `{ get; }` và `{ get; private set; }` đều pass. `{ get; set; }` sẽ fail.

**Điều gì xảy ra nếu ai đó thêm mutable property vào Slug?**

```csharp
public class Slug : ValueObject
{
    public string Value { get; set; } // ← set thay vì không có setter
}
```

Test fail ngay. Value object mutable là oxymoron — nếu `Slug.Value` có thể thay đổi sau khi tạo, equality semantics bị vỡ.

---

### Test 6: `ValueObjects_ShouldInherit_ValueObjectBase`

```csharp
[Fact]
public void ValueObjects_ShouldInherit_ValueObjectBase()
{
    var result = Types.InAssembly(DomainAssembly)
        .That()
        .ResideInNamespace("Blog.Domain.ValueObjects")
        .And()
        .AreClasses()
        .Should()
        .Inherit(typeof(ValueObject))
        .GetResult();
    // ...
}
```

Đảm bảo mọi class trong `Blog.Domain.ValueObjects` namespace đều kế thừa từ `ValueObject` base class. Không ai có thể tạo "value object" mà không có structural equality.

---

### Test 7: `DomainEvents_ShouldBe_RecordTypes`

```csharp
[Fact]
public void DomainEvents_ShouldBe_RecordTypes()
{
    var domainEventTypes = DomainAssembly
        .GetTypes()
        .Where(t => t.Namespace != null && t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
        .Where(t => t.IsClass && !t.IsAbstract)
        .ToList();

    foreach (var eventType in domainEventTypes)
    {
        // C# compiler tạo property "EqualityContract" trên mọi record type
        // Đây là cách reliable nhất để detect record types qua reflection
        var isRecord = eventType.GetProperty("EqualityContract",
            BindingFlags.NonPublic | BindingFlags.Instance) != null;

        isRecord.Should().BeTrue(
            because: $"{eventType.Name} must be a `record` type...");
    }
}
```

**Tại sao không thể dùng `typeof(record)` hay `Type.IsRecord`?**

C# reflection API không có `.IsRecord` property trực tiếp (tính đến .NET 9). `record` là C# compiler feature, không phải CLR feature.

**`EqualityContract` property là gì?**

Khi bạn viết:
```csharp
public record PostPublishedEvent(Guid PostId) : IDomainEvent;
```

C# compiler generate:
```csharp
public class PostPublishedEvent : IDomainEvent
{
    protected virtual Type EqualityContract => typeof(PostPublishedEvent); // ← Đây!
    public Guid PostId { get; init; }
    // ... equals, hashcode, deconstruct methods
}
```

`EqualityContract` (`NonPublic, Instance` vì `protected`) là dấu hiệu reliable nhất rằng một class là record. Tất cả C# versions từ 9.0 trở lên đều generate property này.

---

### Test 8: `AggregateRoots_ShouldInherit_AggregateRootBase`

```csharp
[Fact]
public void AggregateRoots_ShouldInherit_AggregateRootBase()
{
    // Enumerate explicitly thay vì scan namespace
    // Tránh false negative từ PostContent, PostVersion (cùng namespace nhưng không phải AR)
    var aggregateRootTypes = new[]
    {
        typeof(Blog.Domain.Aggregates.Posts.Post),
        typeof(Blog.Domain.Aggregates.Comments.Comment),
        typeof(Blog.Domain.Aggregates.Users.User),
        typeof(Blog.Domain.Aggregates.Tags.Tag),
    };

    foreach (var aggregateType in aggregateRootTypes)
    {
        var inheritsAggregateRoot = InheritsFromGenericAggregateRoot(aggregateType);

        inheritsAggregateRoot.Should().BeTrue(
            because: $"{aggregateType.Name} must inherit from AggregateRoot<TId>...");
    }
}

private static bool InheritsFromGenericAggregateRoot(Type type)
{
    var current = type.BaseType;
    while (current != null && current != typeof(object))
    {
        if (current.IsGenericType &&
            current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            return true;
        current = current.BaseType;
    }
    return false;
}
```

**Tại sao enumerate explicit thay vì scan namespace?**

Namespace `Blog.Domain.Aggregates.Posts` chứa không chỉ `Post` mà còn `PostContent` và `PostVersion`. Nếu scan tất cả classes trong namespace:

```csharp
// WRONG — sẽ check PostContent và PostVersion cũng phải inherit AggregateRoot
Types.InAssembly(DomainAssembly)
    .That()
    .ResideInNamespace("Blog.Domain.Aggregates")
    .Should()
    .Inherit(typeof(AggregateRoot<Guid>))
```

`PostContent` không inherit `AggregateRoot` → test fail → nhưng đây là WRONG failure vì PostContent không phải AR.

Enumerate explicit 4 types chính xác những gì là Aggregate Roots.

**`InheritsFromGenericAggregateRoot()` — Generic base type traversal:**

`typeof(AggregateRoot<>)` là "open generic type" (chưa có type parameter). `typeof(Post).BaseType` là `AggregateRoot<Guid>` — "closed generic type". Không thể `Post.BaseType == typeof(AggregateRoot<>)` trực tiếp.

Fix: dùng `GetGenericTypeDefinition()` để lấy open generic từ closed generic, sau đó so sánh với `typeof(AggregateRoot<>)`.

Traversal loop (`while (current != null)`) xử lý deep inheritance chains trong tương lai — nếu ai đó tạo `ConcretePost : BasePost : AggregateRoot<Guid>`, test vẫn pass.

---

### Test 9: `DomainEvents_ShouldImplement_IDomainEvent`

```csharp
[Fact]
public void DomainEvents_ShouldImplement_IDomainEvent()
{
    var domainEventTypes = DomainAssembly
        .GetTypes()
        .Where(t => t.Namespace != null && t.Namespace.StartsWith("Blog.Domain.DomainEvents"))
        .Where(t => t.IsClass && !t.IsAbstract)
        .ToList();

    foreach (var eventType in domainEventTypes)
    {
        var implementsIDomainEvent = typeof(IDomainEvent).IsAssignableFrom(eventType);

        implementsIDomainEvent.Should().BeTrue(
            because: $"{eventType.Name} must implement IDomainEvent to be dispatched by MediatR");
    }
}
```

`IsAssignableFrom(eventType)` — checks nếu `IDomainEvent` variable có thể hold value của `eventType`. Tức là `eventType` implements `IDomainEvent` (hoặc inherit từ class implement nó).

Test này đảm bảo không ai tạo class trong `DomainEvents` namespace mà không implement interface cần thiết — nếu thiếu, MediatR sẽ không dispatch được event đó.

---

## Tóm tắt: 9 tests và ý nghĩa

| # | Test | Vi phạm nào sẽ trigger fail |
|---|------|---------------------------|
| 1 | `Domain_ShouldNot_ReferenceBlogInfrastructure` | Bất kỳ `using Blog.Infrastructure.*` trong Domain |
| 2 | `Domain_ShouldNot_ReferenceBlogAPI` | Bất kỳ `using Blog.API.*` trong Domain |
| 3 | `Infrastructure_ShouldNot_ReferenceBlogAPI` | Bất kỳ `using Blog.API.*` trong Infrastructure |
| 4 | `Domain_AggregatiesAndValueObjects_ShouldNot_ReferenceMediatRDirectly` | Aggregate hay VO implement `INotification` trực tiếp |
| 5 | `ValueObjects_ShouldBe_Immutable` | Value Object property có `public set` |
| 6 | `ValueObjects_ShouldInherit_ValueObjectBase` | Value Object không kế thừa `ValueObject` |
| 7 | `DomainEvents_ShouldBe_RecordTypes` | Domain event là `class` thay vì `record` |
| 8 | `AggregateRoots_ShouldInherit_AggregateRootBase` | Aggregate Root không kế thừa `AggregateRoot<TId>` |
| 9 | `DomainEvents_ShouldImplement_IDomainEvent` | Domain event class không implement `IDomainEvent` |

---

## Cách chạy Architecture Tests

```bash
# Chạy chỉ architecture tests
dotnet test tests/Blog.ArchTests/

# Output khi tất cả pass:
# Test run for Blog.ArchTests.dll (.NETCoreApp,Version=v10.0)
# Passed!  - Failed:  0, Passed:  9, Skipped:  0, Total:  9

# Output khi fail (ví dụ: ai thêm using EF Core vào Domain):
# Failed Domain_ShouldNot_ReferenceBlogInfrastructure
# Because: Domain layer must not reference Infrastructure (dependency direction rule)
# But the following types do:
#   Blog.Domain.Aggregates.Posts.PostRepository
```

---

## Phase 2 sẽ thêm tests gì?

Khi Phase 2 thêm `Blog.Application` layer với MediatR handlers, sẽ cần thêm:

```csharp
// Sẽ thêm vào LayerBoundaryTests.cs:
[Fact]
public void Application_ShouldNot_ReferenceBlogAPI()
{
    var result = Types.InAssembly(ApplicationAssembly)
        .ShouldNot()
        .HaveDependencyOn("Blog.API")
        .GetResult();
    // ...
}

[Fact]
public void Application_ShouldNot_ReferenceBlogInfrastructure()
{
    // Application không được import EF Core types trực tiếp
    // (phải đi qua interfaces được defined trong Application layer)
    // ...
}
```

Existing 9 tests không cần sửa — chúng tự động enforce boundaries ngay khi Application layer code được thêm.

---

## Câu hỏi tự kiểm tra

1. Tại sao dùng `typeof(Post).Assembly` thay vì `Assembly.Load("Blog.Domain")` để locate assembly? Rủi ro gì nếu dùng string?

2. Nếu developer tạo class `PostValidator` trong `Blog.Domain.ValueObjects` namespace (lỡ tay đặt nhầm folder), test nào sẽ fail? Tại sao?

3. Test `DomainEvents_ShouldBe_RecordTypes` dùng `EqualityContract` property để detect records. Giải thích tại sao `Type.IsRecord` không tồn tại trong .NET reflection API.

4. `InheritsFromGenericAggregateRoot()` dùng while loop thay vì check `type.BaseType == typeof(AggregateRoot<Guid>)` trực tiếp. Scenario nào khiến while loop cần thiết?

5. Test 4 về MediatR dùng reflection thủ công thay vì NetArchTest fluent API. Tại sao NetArchTest không đủ cho test này?
