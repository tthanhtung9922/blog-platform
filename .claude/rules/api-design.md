---
description: >
  Apply these rules whenever creating, modifying, or reviewing REST API endpoints,
  controllers, request/response DTOs, error responses, or OpenAPI specifications.
  Triggers on: Blog.API/Controllers, endpoint routing, HTTP status codes, pagination,
  validation responses.
---

# API Design Rules

## MUST

- **All endpoints are prefixed with `/api/v1/`** — version is part of the URL path.
  ```csharp
  [Route("api/v1/[controller]")]
  public class PostsController : ControllerBase { }
  ```
- **Error responses use RFC 9457 ProblemDetails format:**
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    "title": "Not Found",
    "status": 404,
    "detail": "The requested resource was not found"
  }
  ```
- **Validation errors return HTTP 422** with field-level error details from FluentValidation:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
    "title": "Validation Error",
    "status": 422,
    "errors": {
      "title": ["Title is required", "Title must not exceed 256 characters"]
    }
  }
  ```
- **Paginated responses use a consistent envelope:**
  ```json
  {
    "items": [...],
    "totalCount": 156,
    "page": 1,
    "pageSize": 10,
    "totalPages": 16
  }
  ```
- **The OpenAPI 3.1 spec** (`docs/blog-platform/09-api-contract--openapi-specification.md`) **is the source of truth.** Controller implementations must match the spec. When the spec changes, regenerate TypeScript types via `scripts/gen-types.sh`.
- **Authentication uses Bearer JWT** in the `Authorization` header. Public endpoints explicitly declare `security: []` in the OpenAPI spec.
- **Controllers only delegate to MediatR** — no business logic in controllers. Send a Command or Query, return the result.
  ```csharp
  [HttpPost]
  public async Task<IActionResult> Create(CreatePostRequest request)
  {
      var result = await _mediator.Send(new CreatePostCommand(request));
      return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
  }
  ```

## SHOULD

- Use Scalar UI at `/scalar` for interactive API documentation (instead of Swagger UI).
- Comments API is nested under posts: `/api/v1/posts/{postId}/comments`.
- Use `201 Created` with `Location` header for resource creation endpoints.
- Use `204 No Content` for delete and revoke operations.
- Use `409 Conflict` when an action conflicts with current resource state (e.g., publishing an already-published post).

## NEVER

- Never return 200 for validation failures — use 422.
- Never expose stack traces, internal exception details, or connection strings in error responses.
  ```json
  // NEVER
  { "error": "NpgsqlException: connection refused to localhost:5432..." }
  ```
- Never put business logic or data access in controllers.
