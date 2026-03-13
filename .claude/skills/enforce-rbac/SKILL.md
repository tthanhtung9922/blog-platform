# enforce-rbac

Implement role-based access control (RBAC) at all 3 enforcement layers: API controller policies, MediatR AuthorizationBehavior, and frontend CASL permission checks.

## Arguments

- `permission` (required) — The permission to enforce (e.g., `posts:publish`, `comments:moderate`, `users:manage`)
- `roles` (required) — Comma-separated roles that have this permission: `Admin`, `Editor`, `Author`, `Reader`
- `resource` (optional) — The entity/resource this permission applies to (e.g., `Post`, `Comment`)
- `ownership` (optional) — Whether ownership check is needed: `own-only`, `any`, or omit for no ownership check

## Instructions

You are implementing 3-layer RBAC for the blog-platform. Authorization MUST be enforced at all 3 layers — no single layer can be the sole defense. This is a defense-in-depth strategy (ADR-004).

### Permission Matrix Reference

| Permission | Admin | Editor | Author | Reader |
|---|:---:|:---:|:---:|:---:|
| Read published posts | Yes | Yes | Yes | Yes |
| Create/edit own posts | Yes | Yes | Yes | No |
| Publish posts | Yes | Yes | No | No |
| Delete others' posts | Yes | No | No | No |
| Approve/delete comments | Yes | Yes | No | No |
| Manage users & roles | Yes | No | No | No |
| View analytics | Yes | Yes | Own only | No |
| System settings | Yes | No | No | No |

### Layer 1: API Controller — ASP.NET Policy-Based Authorization

**Location:** `apps/blog-api/src/Blog.API/Controllers/{EntityPlural}Controller.cs`

Apply `[Authorize]` attributes on controller actions:

```csharp
// Simple role check
[Authorize(Roles = "Admin,Editor")]
[HttpPost("{id}/publish")]
public async Task<IActionResult> Publish(Guid id) { ... }

// Policy-based (for complex rules like ownership)
[Authorize(Policy = "CanEditPost")]
[HttpPut("{id}")]
public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostCommand command) { ... }
```

**Register policies in Program.cs / ServiceCollectionExtensions.cs:**

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("CanEditPost", policy =>
        policy.Requirements.Add(new ResourceOwnerOrRoleRequirement("Admin", "Editor")));

    options.AddPolicy("CanPublishPost", policy =>
        policy.RequireRole("Admin", "Editor"));

    options.AddPolicy("CanModerateComment", policy =>
        policy.RequireRole("Admin", "Editor"));

    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireRole("Admin"));
});
```

**Custom authorization handler (for ownership checks):**

```csharp
// apps/blog-api/src/Blog.Infrastructure/Authorization/Requirements/ResourceOwnerOrRoleRequirement.cs
public class ResourceOwnerOrRoleRequirement : IAuthorizationRequirement
{
    public string[] AllowedRoles { get; }
    public ResourceOwnerOrRoleRequirement(params string[] allowedRoles) => AllowedRoles = allowedRoles;
}

// apps/blog-api/src/Blog.Infrastructure/Authorization/Handlers/ResourceOwnerOrRoleHandler.cs
public class ResourceOwnerOrRoleHandler : AuthorizationHandler<ResourceOwnerOrRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerOrRoleRequirement requirement)
    {
        // Check if user has one of the allowed roles
        if (requirement.AllowedRoles.Any(role => context.User.IsInRole(role)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check ownership via resource
        if (context.Resource is { } resource)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // Compare userId with resource owner — implement per resource type
        }

        return Task.CompletedTask;
    }
}
```

### Layer 2: MediatR AuthorizationBehavior

**Location:** `apps/blog-api/src/Blog.Application/Behaviors/AuthorizationBehavior.cs`

Commands/Queries that need authorization implement `IAuthorizedRequest`:

```csharp
// Interface
public interface IAuthorizedRequest
{
    string[] RequiredRoles { get; }
    Guid? ResourceOwnerId { get; }  // null if no ownership check needed
}

// Command example
public record PublishPostCommand(Guid PostId) : IRequest<PostDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "Editor" };
    public Guid? ResourceOwnerId => null;  // Any Admin/Editor can publish
}

// Command with ownership check
public record UpdatePostCommand(Guid PostId, string Title, ...) : IRequest<PostDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "Editor", "Author" };
    public Guid? ResourceOwnerId { get; init; }  // Set by handler after loading entity
}
```

**AuthorizationBehavior implementation:**

```csharp
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUser;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuthorizedRequest authorized)
            return await next();

        var userRole = _currentUser.Role;

        // Check role
        if (!authorized.RequiredRoles.Contains(userRole))
            throw new ForbiddenAccessException();

        // Check ownership (if applicable)
        if (authorized.ResourceOwnerId.HasValue
            && userRole == "Author"
            && authorized.ResourceOwnerId.Value != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("You can only modify your own resources.");
        }

        return await next();
    }
}
```

**Pipeline order matters:** Validation → Logging → **Authorization** → Caching

### Layer 3: Frontend CASL Permission Checks

**Location:** `apps/blog-admin/src/lib/permissions.ts` and shared via `libs/shared-contracts/`

**Define CASL abilities:**

```typescript
// libs/shared-contracts/src/permissions.ts
import { AbilityBuilder, createMongoAbility, MongoAbility } from '@casl/ability';

export type Actions = 'read' | 'create' | 'update' | 'delete' | 'publish' | 'moderate' | 'manage';
export type Subjects = 'Post' | 'Comment' | 'User' | 'Analytics' | 'Settings' | 'all';

export type AppAbility = MongoAbility<[Actions, Subjects]>;

export function defineAbilitiesFor(role: string, userId: string): AppAbility {
  const { can, cannot, build } = new AbilityBuilder<AppAbility>(createMongoAbility);

  switch (role) {
    case 'Admin':
      can('manage', 'all');  // Admin can do everything
      break;

    case 'Editor':
      can('read', 'Post');
      can('create', 'Post');
      can('update', 'Post');
      can('publish', 'Post');
      can('read', 'Comment');
      can('moderate', 'Comment');
      can('read', 'Analytics');
      break;

    case 'Author':
      can('read', 'Post');
      can('create', 'Post');
      can('update', 'Post', { authorId: userId });  // Own posts only
      can('read', 'Comment');
      can('create', 'Comment');
      can('read', 'Analytics', { authorId: userId });  // Own analytics only
      break;

    case 'Reader':
      can('read', 'Post');
      can('read', 'Comment');
      can('create', 'Comment');
      break;
  }

  return build();
}
```

**PermissionGate component:**

```tsx
// apps/blog-admin/src/components/auth/PermissionGate.tsx
'use client';

import { useAbility } from '@casl/react';
import { AbilityContext } from '@/lib/ability-context';

interface PermissionGateProps {
  action: Actions;
  subject: Subjects;
  field?: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function PermissionGate({ action, subject, children, fallback = null }: PermissionGateProps) {
  const ability = useAbility(AbilityContext);

  if (ability.can(action, subject)) {
    return <>{children}</>;
  }

  return <>{fallback}</>;
}
```

**ProtectedRoute component:**

```tsx
// apps/blog-admin/src/components/auth/ProtectedRoute.tsx
'use client';

import { redirect } from 'next/navigation';
import { useAbility } from '@casl/react';
import { AbilityContext } from '@/lib/ability-context';

interface ProtectedRouteProps {
  action: Actions;
  subject: Subjects;
  children: React.ReactNode;
  redirectTo?: string;
}

export function ProtectedRoute({ action, subject, children, redirectTo = '/unauthorized' }: ProtectedRouteProps) {
  const ability = useAbility(AbilityContext);

  if (!ability.can(action, subject)) {
    redirect(redirectTo);
  }

  return <>{children}</>;
}
```

### Sync Checklist

When adding a new permission, you MUST update ALL 3 layers:

1. [ ] **Backend Policy** — Add/update `[Authorize]` attribute on controller action
2. [ ] **MediatR** — Implement `IAuthorizedRequest` on the Command/Query with correct `RequiredRoles`
3. [ ] **Frontend CASL** — Update `defineAbilitiesFor()` in `libs/shared-contracts/src/permissions.ts`
4. [ ] **Permission Gate** — Wrap UI elements with `<PermissionGate>` where needed
5. [ ] **Protected Route** — Add `<ProtectedRoute>` for admin pages that require permissions

### Common Permission Patterns

| Action | Backend Policy | MediatR Roles | CASL |
|---|---|---|---|
| Create post | `[Authorize]` (any authenticated) | `Admin, Editor, Author` | `can('create', 'Post')` |
| Edit own post | `[Authorize(Policy = "CanEditPost")]` | `Admin, Editor, Author` + ownership | `can('update', 'Post', { authorId })` |
| Publish post | `[Authorize(Policy = "CanPublishPost")]` | `Admin, Editor` | `can('publish', 'Post')` |
| Delete any post | `[Authorize(Roles = "Admin")]` | `Admin` | `can('delete', 'Post')` |
| Moderate comment | `[Authorize(Policy = "CanModerateComment")]` | `Admin, Editor` | `can('moderate', 'Comment')` |
| Manage users | `[Authorize(Policy = "CanManageUsers")]` | `Admin` | `can('manage', 'User')` |
