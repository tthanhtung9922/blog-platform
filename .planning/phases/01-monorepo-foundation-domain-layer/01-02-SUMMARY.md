---
phase: 01-monorepo-foundation-domain-layer
plan: "02"
subsystem: domain
tags: [csharp, ddd, domain-events, value-objects, aggregate-roots, mediatr, mediatR]

# Dependency graph
requires:
  - phase: 01-monorepo-foundation-domain-layer
    plan: "01"
    provides: Blog.Domain.csproj with MediatR package reference

provides:
  - AggregateRoot<TId> base class with domain events list
  - ValueObject base class with structural equality (==, !=, GetHashCode)
  - IDomainEvent interface extending MediatR.INotification
  - DomainException for domain rule violations
  - Slug value object with Vietnamese diacritics normalization
  - Email value object with regex validation (lowercase normalized)
  - ReadingTime value object (250 wpm, min 1 minute)
  - TagReference value object (Post→Tag decoupling by ID only)
  - Post aggregate root (Draft/Published/Archived lifecycle, tags, versioning)
  - Comment aggregate root (1-level nesting constraint enforced)
  - User aggregate root (ADR-006: standalone GUID, NOT IdentityUser)
  - Tag aggregate root (Name, Slug, TagCreatedEvent)
  - PostContent child entity (BodyJson+BodyHtml, EF materializer)
  - PostVersion child entity (append-only snapshot)
  - 12 domain event record types (Post, Comment, User, Tag events)
  - IPostRepository, ICommentRepository, IUserRepository, ITagRepository interfaces
  - 21 unit tests covering value objects and aggregates

affects:
  - 01-03 (Blog.Infrastructure will implement repository interfaces)
  - 01-04 (Blog.Application will use aggregates in CQRS handlers)
  - testing (Blog.UnitTests extended with domain test coverage)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Factory method pattern (Post.Create, Comment.Create, User.Create, Tag.Create)
    - Private constructor + static factory (all aggregates and value objects)
    - Domain events raised in aggregate methods, cleared after SaveChanges
    - Value object equality via GetEqualityComponents()
    - Repository interface in Domain layer, implementation deferred to Infrastructure

key-files:
  created:
    - apps/blog-api/src/Blog.Domain/Common/IDomainEvent.cs
    - apps/blog-api/src/Blog.Domain/Common/AggregateRoot.cs
    - apps/blog-api/src/Blog.Domain/Common/ValueObject.cs
    - apps/blog-api/src/Blog.Domain/Exceptions/DomainException.cs
    - apps/blog-api/src/Blog.Domain/ValueObjects/Slug.cs
    - apps/blog-api/src/Blog.Domain/ValueObjects/Email.cs
    - apps/blog-api/src/Blog.Domain/ValueObjects/ReadingTime.cs
    - apps/blog-api/src/Blog.Domain/ValueObjects/TagReference.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Posts/Post.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostContent.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostVersion.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostStatus.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Comments/Comment.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Comments/CommentStatus.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Users/User.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Users/UserRole.cs
    - apps/blog-api/src/Blog.Domain/Aggregates/Tags/Tag.cs
    - apps/blog-api/src/Blog.Domain/DomainEvents/PostEvents.cs
    - apps/blog-api/src/Blog.Domain/DomainEvents/CommentEvents.cs
    - apps/blog-api/src/Blog.Domain/DomainEvents/UserEvents.cs
    - apps/blog-api/src/Blog.Domain/DomainEvents/TagEvents.cs
    - apps/blog-api/src/Blog.Domain/Repositories/IPostRepository.cs
    - apps/blog-api/src/Blog.Domain/Repositories/ICommentRepository.cs
    - apps/blog-api/src/Blog.Domain/Repositories/IUserRepository.cs
    - apps/blog-api/src/Blog.Domain/Repositories/ITagRepository.cs
    - tests/Blog.UnitTests/Domain/ValueObjects/SlugTests.cs
    - tests/Blog.UnitTests/Domain/ValueObjects/EmailTests.cs
    - tests/Blog.UnitTests/Domain/ValueObjects/ReadingTimeTests.cs
    - tests/Blog.UnitTests/Domain/ValueObjects/TagReferenceTests.cs
    - tests/Blog.UnitTests/Domain/Aggregates/PostTests.cs
    - tests/Blog.UnitTests/Domain/Aggregates/CommentTests.cs
    - tests/Blog.UnitTests/Domain/Aggregates/UserTests.cs
    - tests/Blog.UnitTests/Domain/Aggregates/TagTests.cs
  modified:
    - tests/Blog.UnitTests/Blog.UnitTests.csproj (added Blog.Domain and MediatR.Contracts references)

key-decisions:
  - "User aggregate uses standalone GUID matching IdentityUser.Id (ADR-006) — no inheritance, no FK, documented in Create() XML comment"
  - "Comment.AddReply() throws DomainException when called on a reply (ParentId != null) — nesting limited to 1 level"
  - "Post.Publish() throws DomainException (not InvalidOperationException) for consistency with domain exception pattern"
  - "TagReference is a value object on Post holding only TagId — Post does not reference Tag entity to avoid cross-aggregate coupling"
  - "PostContent and PostVersion are internal child entities (internal static Create) — cannot be created outside the Post aggregate"
  - "Slug normalization handles Vietnamese đ/Đ → d before FormD decomposition, covering all 6 tonal marks"

patterns-established:
  - "Factory method pattern: all aggregates use static Create() with private constructor — prevents invalid state at construction"
  - "Domain events raised inside aggregate methods, not in handlers — keeps business invariant enforcement in the domain"
  - "Value objects use GetEqualityComponents() for structural equality — no mutable state, compared by value"
  - "Repository interfaces in Blog.Domain/Repositories/ — pure contracts with no implementation detail"

requirements-completed:
  - INFR-01

# Metrics
duration: 6min
completed: 2026-03-15
---

# Phase 1 Plan 02: Blog.Domain Layer Summary

**Pure C# domain layer with 4 aggregate roots, 4 value objects, 12 domain event records, and 4 repository interfaces — zero infrastructure references, 21 tests green**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-03-15T05:52:31Z
- **Completed:** 2026-03-15T05:58:06Z
- **Tasks:** 2
- **Files created:** 33 (25 source + 8 test)
- **Files modified:** 1 (Blog.UnitTests.csproj)

## Accomplishments

- Complete Blog.Domain class library — only MediatR as external dependency, zero EF Core / ASP.NET / Npgsql
- 4 aggregate roots (Post, Comment, User, Tag) all inheriting AggregateRoot<Guid> with factory method pattern
- 4 value objects (Slug, Email, ReadingTime, TagReference) — all immutable, structural equality, validated at construction
- 12 domain event `record` types grouped by aggregate — ready for MediatR INotificationHandler wiring in Phase 2
- 4 repository interfaces in Domain layer — pure contracts unblocking Blog.Infrastructure implementation in Plan 03
- 21 unit tests green, covering all value object behaviors and critical aggregate business rules

## Task Commits

Each task was committed atomically:

1. **Task 1: Common base classes and value objects** - `0ff49eb` (feat)
2. **Task 2: All aggregate roots, domain events, and repository interfaces** - `6eea61b` (feat)

_Note: TDD tasks — tests written first (RED), then implementation (GREEN), committed together per task_

## Files Created/Modified

**Common:**
- `apps/blog-api/src/Blog.Domain/Common/IDomainEvent.cs` - Marker interface extending MediatR.INotification
- `apps/blog-api/src/Blog.Domain/Common/AggregateRoot.cs` - Base class with domain events list and ClearDomainEvents()
- `apps/blog-api/src/Blog.Domain/Common/ValueObject.cs` - Structural equality via GetEqualityComponents()
- `apps/blog-api/src/Blog.Domain/Exceptions/DomainException.cs` - Domain rule violation exception

**Value Objects:**
- `apps/blog-api/src/Blog.Domain/ValueObjects/Slug.cs` - Vietnamese diacritics normalization (FormD + đ→d)
- `apps/blog-api/src/Blog.Domain/ValueObjects/Email.cs` - Regex validation, lowercase normalized
- `apps/blog-api/src/Blog.Domain/ValueObjects/ReadingTime.cs` - 250 wpm calculation, min 1 minute
- `apps/blog-api/src/Blog.Domain/ValueObjects/TagReference.cs` - Holds TagId only (cross-aggregate decoupling)

**Aggregates:**
- `apps/blog-api/src/Blog.Domain/Aggregates/Posts/Post.cs` - Core aggregate root (90 lines)
- `apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostContent.cs` - BodyJson+BodyHtml child entity
- `apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostVersion.cs` - Append-only snapshot
- `apps/blog-api/src/Blog.Domain/Aggregates/Posts/PostStatus.cs` - Draft/Published/Archived enum
- `apps/blog-api/src/Blog.Domain/Aggregates/Comments/Comment.cs` - 1-level nesting constraint
- `apps/blog-api/src/Blog.Domain/Aggregates/Comments/CommentStatus.cs` - Pending/Approved/Rejected enum
- `apps/blog-api/src/Blog.Domain/Aggregates/Users/User.cs` - Standalone aggregate (ADR-006)
- `apps/blog-api/src/Blog.Domain/Aggregates/Users/UserRole.cs` - Reader/Author/Editor/Admin enum
- `apps/blog-api/src/Blog.Domain/Aggregates/Tags/Tag.cs` - Name, Slug, TagCreatedEvent

**Domain Events:**
- `apps/blog-api/src/Blog.Domain/DomainEvents/PostEvents.cs` - PostCreated/Published/Updated/Archived
- `apps/blog-api/src/Blog.Domain/DomainEvents/CommentEvents.cs` - CommentAdded/Approved/Rejected/Deleted
- `apps/blog-api/src/Blog.Domain/DomainEvents/UserEvents.cs` - UserProfileUpdated/Banned
- `apps/blog-api/src/Blog.Domain/DomainEvents/TagEvents.cs` - TagCreated/Deleted

**Repository Interfaces:**
- `apps/blog-api/src/Blog.Domain/Repositories/IPostRepository.cs` - GetById/Slug, GetPublished (paginated), Add/Update/Delete
- `apps/blog-api/src/Blog.Domain/Repositories/ICommentRepository.cs` - GetById, GetByPostId, Add/Update/Delete
- `apps/blog-api/src/Blog.Domain/Repositories/IUserRepository.cs` - GetById/Email, Add/Update
- `apps/blog-api/src/Blog.Domain/Repositories/ITagRepository.cs` - GetById/Slug, GetAll, Add/Update/Delete

## Decisions Made

- User.Create() takes a `Guid id` parameter (same GUID as IdentityUser.Id per ADR-006) — no auto-generated Id in User.Create(), unlike other aggregates
- Comment.AddReply() enforces nesting at the domain level: if the comment already has a ParentId, DomainException is thrown
- Post.Publish() uses DomainException (not InvalidOperationException) — consistent with project exception strategy
- SocialLinks is `Dictionary<string, string>` on User — flexible for any social platform without schema changes
- IPostRepository.GetPublishedAsync returns a value tuple `(Items, TotalCount)` — ready for pagination without a separate count query

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added MediatR.Contracts package to Blog.UnitTests.csproj**
- **Found during:** Task 2 (aggregate tests build)
- **Issue:** Tests referencing DomainEvents (which implement MediatR.INotification via IDomainEvent) failed with CS0012 because MediatR.Contracts was only a transitive reference, not a direct one
- **Fix:** Added `<PackageReference Include="MediatR.Contracts" Version="2.*" />` to Blog.UnitTests.csproj
- **Files modified:** `tests/Blog.UnitTests/Blog.UnitTests.csproj`
- **Verification:** `dotnet test` passes — 21 tests green
- **Committed in:** `6eea61b` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Required to unblock test compilation. No scope creep.

## Issues Encountered

None — build succeeded on first attempt after auto-fix.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Blog.Domain is complete and ready to be referenced by Blog.Infrastructure (Plan 03)
- Blog.Application (Plan 04) can start defining CQRS handlers against the repository interfaces
- Architecture tests (Blog.ArchTests) can now enforce layer boundaries against the real Domain assembly
- No blockers. The Class1.cs placeholder remains in Blog.Domain root (not deleted, harmless).

---
*Phase: 01-monorepo-foundation-domain-layer*
*Completed: 2026-03-15*
