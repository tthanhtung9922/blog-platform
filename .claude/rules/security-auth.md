---
description: >
  Apply these rules whenever implementing, modifying, or reviewing authentication,
  authorization, JWT handling, role assignments, permission checks, CASL definitions,
  ASP.NET Authorization Policies, or MediatR AuthorizationBehavior. Triggers on any
  code touching auth, roles, permissions, tokens, or access control.
---

# Security & Authorization Rules

## MUST

- **RBAC is enforced at all three layers (ADR-004) — defense in depth:**
  1. **API Layer:** ASP.NET Authorization Policies on controllers/endpoints
  2. **Application Layer:** MediatR `AuthorizationBehavior` checks before handler execution
  3. **Frontend:** CASL permission checks via `PermissionGate` component and `usePermission` hook
- **All three layers must stay in sync.** Permission definitions are shared via `libs/shared-contracts/src/permissions.ts`. When a permission changes, update all three enforcement points.
- **Four roles with strict hierarchy:**
  ```
  Admin > Editor > Author > Reader
  ```
  | Permission | Admin | Editor | Author | Reader |
  |---|:---:|:---:|:---:|:---:|
  | Read published posts | yes | yes | yes | yes |
  | Create/edit own posts | yes | yes | yes | no |
  | Publish posts | yes | yes | no | no |
  | Delete others' posts | yes | no | no | no |
  | Moderate comments | yes | yes | no | no |
  | Manage users & roles | yes | no | no | no |
  | View analytics | yes | yes | own only | no |
  | System settings | yes | no | no | no |
- **CASL version must be >= 6.8.0** to include the CVE-2026-1774 security fix.
- **JWT authentication** uses Bearer tokens in the Authorization header. Refresh tokens are rotated on use.
- **Authentication logic lives in Infrastructure** (`IdentityService`, `JwtTokenService`, `CurrentUserService`) — never in Domain or Application layers.

## SHOULD

- Use `ICurrentUserService` (Application abstraction) to get the current user's identity and role in handlers — never access HTTP context directly from Application layer.
- Return `403 Forbidden` (not 401) when a user is authenticated but lacks the required role for an action.

## NEVER

- Never hardcode secrets, JWT signing keys, connection strings, or API keys in source code.
  ```csharp
  // NEVER
  var key = "my-super-secret-jwt-key-12345";

  // CORRECT
  var key = configuration["Jwt:SigningKey"]; // from secrets/env
  ```
- Never store plain-text passwords — ASP.NET Identity handles hashing.
- Never trust frontend CASL checks alone — they are for UX only. The API and Application layers are the security boundary.
- Never expose internal user IDs, stack traces, or connection strings in API error responses.
- Never skip the `AuthorizationBehavior` in the MediatR pipeline — it runs for every request that requires authorization.
