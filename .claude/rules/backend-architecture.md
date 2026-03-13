---
description: >
  Apply these rules whenever writing, editing, or reviewing any C# backend code
  in Blog.Domain, Blog.Application, Blog.Infrastructure, or Blog.API projects.
  Triggers on: aggregate design, repository patterns, MediatR handlers, pipeline
  behaviors, domain events, service registration, dependency injection.
---

# Backend Architecture Rules

## MUST

- **Four layers with strict dependency direction:** Domain → Application → Infrastructure → Presentation. A layer may only reference layers inward (to its left). Domain references nothing; API may reference all.
- **Aggregates live in `Blog.Domain/Aggregates/<Name>/`** with the Aggregate Root class, child Entities, and related Enums in the same folder.
- **Value Objects live in `Blog.Domain/ValueObjects/`** — they are immutable, compared by value, and self-validating.
- **Domain Events live in `Blog.Domain/DomainEvents/`** — named as past-tense facts (e.g., `PostPublishedEvent`, `CommentAddedEvent`).
- **Repository interfaces are defined in `Blog.Domain/Repositories/`** — implementations go in `Blog.Infrastructure/Persistence/Repositories/`.
- **Domain services are stateless** and live in `Blog.Domain/Services/`.
- **CQRS via MediatR:** Commands in `Blog.Application/Features/<Aggregate>/Commands/<Action>/`, Queries in `Features/<Aggregate>/Queries/<Action>/`. Each folder contains the Request, Handler, and Validator.
- **Command naming:** `<Action><Entity>Command` (e.g., `CreatePostCommand`, `PublishPostCommand`).
- **Query naming:** `Get<Entity>By<Criteria>Query` (e.g., `GetPostBySlugQuery`, `GetPostListQuery`).
- **Each Command has a FluentValidation validator** in the same folder (`<Command>Validator.cs`).
- **Pipeline behavior order is fixed and must not be reordered:** `ValidationBehavior → LoggingBehavior → AuthorizationBehavior → CachingBehavior`.
- **Domain Events are dispatched after `SaveChanges()` succeeds**, via MediatR `INotificationHandler`. Never dispatch before persistence confirms.
- **IdentityUser (Infrastructure) and User (Domain) are completely separate.** No inheritance (`User` does NOT extend `IdentityUser`). No navigation properties. Linked only by shared GUID. Authentication goes through `IIdentityService`; business logic goes through `IUserRepository`.
- **Cross-context operations (Register, Ban)** use shared `DbConnection` via `IUnitOfWork` to ensure atomicity across `IdentityDbContext` and `BlogDbContext`.
- **Application layer abstractions** (`ICurrentUserService`, `IDateTimeService`, `IEmailService`, `IStorageService`) are defined in `Blog.Application/Abstractions/` and implemented in Infrastructure.

## SHOULD

- Use structured logging in `LoggingBehavior` — log request type, execution time, and user context as structured properties, not interpolated strings.
- Expose `/metrics` endpoint in Blog.API for Prometheus scraping.
- Keep DTOs in `Blog.Application/DTOs/` — they are the contract between Application and Presentation layers.

## NEVER

- Domain layer must never reference `Blog.Infrastructure`, `Blog.API`, or any external framework (EF Core, ASP.NET Identity, Redis, etc.).
  ```csharp
  // Blog.Domain/Aggregates/Posts/Post.cs
  using Blog.Infrastructure.Persistence; // NEVER
  using Microsoft.EntityFrameworkCore;    // NEVER
  ```
- Application layer must never directly instantiate Infrastructure classes — always go through interfaces.
- Never put business logic in controllers. Controllers delegate to MediatR and return responses.
- Never dispatch Domain Events before `SaveChanges()` — side effects must only fire after persistence succeeds.
- Never create a `User` domain entity by extending `IdentityUser`:
  ```csharp
  // NEVER
  public class User : IdentityUser { ... }

  // CORRECT
  public class User  // standalone aggregate root
  {
      public Guid Id { get; }  // same GUID as IdentityUser.Id
  }
  ```

## Edge Cases

- **When both IdentityUser and User need updating** (e.g., Admin bans a user): the Application handler calls both `IIdentityService` and `IUserRepository` within a single `IUnitOfWork` transaction scope.
- **When migrating to microservices** (Phase 3+): replace shared DbConnection (Option A) with compensating actions / Saga pattern (Option B). The `IUnitOfWork` abstraction makes this swap possible.
