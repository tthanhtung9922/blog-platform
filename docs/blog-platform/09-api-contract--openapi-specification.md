# API Contract & OpenAPI Specification

## 9.1 OpenAPI Overview

**Specification:** OpenAPI 3.1 · JSON format
**Base URL:** `https://api.blog-platform.dev/api/v1`
**Authentication:** Bearer JWT (Authorization header)
**Documentation UI:** Scalar UI (`/scalar`) — thay thế Swagger UI, giao diện hiện đại hơn
**Code generation:** `scripts/gen-types.sh` → `libs/shared-contracts/src/api.types.ts`

```yaml
openapi: 3.1.0
info:
  title: Blog Platform API
  version: 1.0.0
  description: |
    RESTful API cho Blog Platform.
    Authentication qua JWT Bearer token.
    RBAC: Admin > Editor > Author > Reader.
  contact:
    name: Engineering Team
    email: engineering@blog-platform.dev
  license:
    name: MIT

servers:
  - url: https://api.blog-platform.dev/api/v1
    description: Production
  - url: https://staging-api.blog-platform.dev/api/v1
    description: Staging
  - url: http://localhost:5000/api/v1
    description: Local Development

security:
  - BearerAuth: []

components:
  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
```

---

## 9.2 Posts API

**Controller:** `PostsController.cs` · Route prefix: `/api/v1/posts`

```yaml
paths:
  /posts:
    get:
      operationId: getPostList
      summary: Lấy danh sách bài viết (published)
      tags: [Posts]
      security: []                           # Public — không cần auth
      parameters:
        - name: page
          in: query
          schema: { type: integer, default: 1, minimum: 1 }
        - name: pageSize
          in: query
          schema: { type: integer, default: 10, minimum: 1, maximum: 50 }
        - name: tag
          in: query
          schema: { type: string }
          description: Filter by tag slug
        - name: author
          in: query
          schema: { type: string, format: uuid }
          description: Filter by author ID
        - name: search
          in: query
          schema: { type: string }
          description: Full-text search (PostgreSQL FTS — hỗ trợ tiếng Việt, ADR-009)
      responses:
        '200':
          description: Paginated list of published posts
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PaginatedPostList'
              example:
                items:
                  - id: "f47ac10b-58cc-4372-a567-0e02b2c3d479"
                    title: "Hướng dẫn Clean Architecture với ASP.NET Core 10"
                    slug: "huong-dan-clean-architecture-aspnet-core-10"
                    excerpt: "Tìm hiểu cách xây dựng ứng dụng .NET theo kiến trúc Clean Architecture..."
                    authorId: "550e8400-e29b-41d4-a716-446655440000"
                    authorDisplayName: "Nguyễn Văn A"
                    status: "Published"
                    coverImageUrl: "https://cdn.blog-platform.dev/covers/clean-arch.webp"
                    readingTimeMinutes: 12
                    isFeatured: true
                    publishedAt: "2026-03-10T08:00:00Z"
                    tags:
                      - { id: "...", name: ".NET", slug: "dotnet" }
                      - { id: "...", name: "Architecture", slug: "architecture" }
                    likeCount: 42
                    commentCount: 7
                totalCount: 156
                page: 1
                pageSize: 10
                totalPages: 16

    post:
      operationId: createPost
      summary: Tạo bài viết mới (Draft)
      tags: [Posts]
      security:
        - BearerAuth: []                     # Requires: Author, Editor, Admin
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreatePostRequest'
            example:
              title: "Hướng dẫn Clean Architecture với ASP.NET Core 10"
              excerpt: "Tìm hiểu cách xây dựng ứng dụng .NET theo kiến trúc Clean Architecture..."
              bodyJson:
                type: "doc"
                content:
                  - type: "paragraph"
                    content:
                      - type: "text"
                        text: "Clean Architecture là một pattern..."
              coverImageUrl: "https://cdn.blog-platform.dev/covers/clean-arch.webp"
              tagIds: ["uuid-1", "uuid-2"]
      responses:
        '201':
          description: Post created (status = Draft)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PostDto'
          headers:
            Location:
              schema: { type: string }
              description: URL of created post
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '422': { $ref: '#/components/responses/ValidationError' }

  /posts/{slug}:
    get:
      operationId: getPostBySlug
      summary: Lấy chi tiết bài viết theo slug
      tags: [Posts]
      security: []                           # Public
      parameters:
        - name: slug
          in: path
          required: true
          schema: { type: string }
          example: "huong-dan-clean-architecture-aspnet-core-10"
      responses:
        '200':
          description: Post detail with content
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PostDetailDto'
              example:
                id: "f47ac10b-58cc-4372-a567-0e02b2c3d479"
                title: "Hướng dẫn Clean Architecture với ASP.NET Core 10"
                slug: "huong-dan-clean-architecture-aspnet-core-10"
                excerpt: "Tìm hiểu cách xây dựng..."
                bodyJson: { type: "doc", content: [...] }
                bodyHtml: "<p>Clean Architecture là một pattern...</p>"
                author:
                  id: "550e8400-e29b-41d4-a716-446655440000"
                  displayName: "Nguyễn Văn A"
                  avatarUrl: "https://cdn.blog-platform.dev/avatars/nguyen-van-a.webp"
                status: "Published"
                coverImageUrl: "https://cdn.blog-platform.dev/covers/clean-arch.webp"
                readingTimeMinutes: 12
                isFeatured: true
                publishedAt: "2026-03-10T08:00:00Z"
                createdAt: "2026-03-08T14:30:00Z"
                updatedAt: "2026-03-10T07:55:00Z"
                tags:
                  - { id: "...", name: ".NET", slug: "dotnet" }
                likeCount: 42
                commentCount: 7
                isLikedByCurrentUser: false
                isBookmarkedByCurrentUser: false
        '404': { $ref: '#/components/responses/NotFound' }

  /posts/{id}:
    put:
      operationId: updatePost
      summary: Cập nhật bài viết
      tags: [Posts]
      security:
        - BearerAuth: []                     # Owner (Author) hoặc Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdatePostRequest'
      responses:
        '200':
          description: Post updated
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PostDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }
        '422': { $ref: '#/components/responses/ValidationError' }

    delete:
      operationId: deletePost
      summary: Xóa bài viết (soft delete → Archive)
      tags: [Posts]
      security:
        - BearerAuth: []                     # Owner hoặc Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '204': { description: Post archived }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }

  /posts/{id}/publish:
    post:
      operationId: publishPost
      summary: Publish bài viết (Draft → Published)
      tags: [Posts]
      security:
        - BearerAuth: []                     # Requires: Editor hoặc Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200':
          description: Post published
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PostDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403':
          description: "Forbidden — chỉ Editor/Admin mới có quyền publish (ADR-004)"
        '404': { $ref: '#/components/responses/NotFound' }
        '409':
          description: "Conflict — post đã ở trạng thái Published"

  /posts/{id}/archive:
    post:
      operationId: archivePost
      summary: Archive bài viết (Published → Archived)
      tags: [Posts]
      security:
        - BearerAuth: []                     # Owner hoặc Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200':
          description: Post archived
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PostDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }
```

---

## 9.3 Comments API

**Controller:** `CommentsController.cs` · Route prefix: `/api/v1/posts/{postId}/comments`

```yaml
paths:
  /posts/{postId}/comments:
    get:
      operationId: getCommentsByPost
      summary: Lấy danh sách comments theo bài viết
      tags: [Comments]
      security: []                           # Public
      parameters:
        - name: postId
          in: path
          required: true
          schema: { type: string, format: uuid }
        - name: page
          in: query
          schema: { type: integer, default: 1 }
        - name: pageSize
          in: query
          schema: { type: integer, default: 20, maximum: 100 }
      responses:
        '200':
          description: Paginated comments (nested — includes replies)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PaginatedCommentList'
              example:
                items:
                  - id: "a1b2c3d4-..."
                    postId: "f47ac10b-..."
                    author:
                      id: "550e8400-..."
                      displayName: "Trần Thị B"
                      avatarUrl: "https://cdn.blog-platform.dev/avatars/tran-thi-b.webp"
                    content: "Bài viết rất hữu ích! Cảm ơn tác giả."
                    isApproved: true
                    createdAt: "2026-03-11T09:15:00Z"
                    replies:
                      - id: "e5f6g7h8-..."
                        author:
                          id: "550e8400-..."
                          displayName: "Nguyễn Văn A"
                          avatarUrl: "..."
                        content: "Cảm ơn bạn đã đọc!"
                        parentId: "a1b2c3d4-..."
                        createdAt: "2026-03-11T10:00:00Z"
                totalCount: 7
                page: 1
                pageSize: 20
        '404': { description: Post not found }

    post:
      operationId: addComment
      summary: Thêm comment vào bài viết
      tags: [Comments]
      security:
        - BearerAuth: []                     # Requires: bất kỳ authenticated user
      parameters:
        - name: postId
          in: path
          required: true
          schema: { type: string, format: uuid }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [content]
              properties:
                content:
                  type: string
                  maxLength: 5000
                parentId:
                  type: string
                  format: uuid
                  description: "ID của comment cha (nếu là reply)"
            example:
              content: "Bài viết rất hữu ích! Cảm ơn tác giả."
              parentId: null
      responses:
        '201':
          description: Comment created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CommentDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '404': { description: Post not found }
        '422': { $ref: '#/components/responses/ValidationError' }

  /comments/{id}:
    delete:
      operationId: deleteComment
      summary: Xóa comment
      tags: [Comments]
      security:
        - BearerAuth: []                     # Owner hoặc Editor/Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '204': { description: Comment deleted }
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }

  /comments/{id}/moderate:
    post:
      operationId: moderateComment
      summary: Approve/reject comment (Editor/Admin only)
      tags: [Comments]
      security:
        - BearerAuth: []                     # Requires: Editor hoặc Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [isApproved]
              properties:
                isApproved: { type: boolean }
            example:
              isApproved: true
      responses:
        '200':
          description: Comment moderation updated
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CommentDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }
```

---

## 9.4 Auth API

**Controller:** `AuthController.cs` · Route prefix: `/api/v1/auth`

```yaml
paths:
  /auth/register:
    post:
      operationId: register
      summary: Đăng ký tài khoản mới
      tags: [Auth]
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/RegisterRequest'
            example:
              email: "user@example.com"
              password: "SecureP@ss123"
              displayName: "Nguyễn Văn A"
      responses:
        '201':
          description: User registered — cần xác nhận email
          content:
            application/json:
              schema:
                type: object
                properties:
                  userId: { type: string, format: uuid }
                  message: { type: string }
              example:
                userId: "550e8400-e29b-41d4-a716-446655440000"
                message: "Registration successful. Please check your email to confirm."
        '409':
          description: Email đã tồn tại
        '422': { $ref: '#/components/responses/ValidationError' }

  /auth/login:
    post:
      operationId: login
      summary: Đăng nhập — trả về JWT + Refresh Token
      tags: [Auth]
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/LoginRequest'
            example:
              email: "user@example.com"
              password: "SecureP@ss123"
      responses:
        '200':
          description: Login successful
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/TokenResponse'
              example:
                accessToken: "eyJhbGciOiJIUzI1NiIs..."
                refreshToken: "dGhpcyBpcyBhIHJlZnJlc2..."
                expiresIn: 3600
                tokenType: "Bearer"
          headers:
            Set-Cookie:
              description: "HttpOnly cookie chứa refresh token (optional — tùy strategy)"
              schema: { type: string }
        '401':
          description: "Email/password không đúng hoặc tài khoản chưa xác nhận"

  /auth/refresh:
    post:
      operationId: refreshToken
      summary: Làm mới access token bằng refresh token
      tags: [Auth]
      security: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [refreshToken]
              properties:
                refreshToken: { type: string }
      responses:
        '200':
          description: New token pair
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/TokenResponse'
        '401':
          description: "Refresh token không hợp lệ hoặc đã hết hạn"

  /auth/revoke:
    post:
      operationId: revokeToken
      summary: Thu hồi refresh token (logout)
      tags: [Auth]
      security:
        - BearerAuth: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [refreshToken]
              properties:
                refreshToken: { type: string }
      responses:
        '204': { description: Token revoked }
        '401': { $ref: '#/components/responses/Unauthorized' }

  /auth/me:
    get:
      operationId: getCurrentUser
      summary: Lấy thông tin user hiện tại
      tags: [Auth]
      security:
        - BearerAuth: []
      responses:
        '200':
          description: Current user info
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CurrentUserDto'
              example:
                id: "550e8400-e29b-41d4-a716-446655440000"
                email: "user@example.com"
                displayName: "Nguyễn Văn A"
                avatarUrl: "https://cdn.blog-platform.dev/avatars/nguyen-van-a.webp"
                role: "Author"
                permissions:
                  - "posts:create"
                  - "posts:update:own"
                  - "comments:create"
        '401': { $ref: '#/components/responses/Unauthorized' }
```

---

## 9.5 Reactions API

**Controller:** `ReactionsController.cs` · Route prefix: `/api/v1/posts/{postId}/reactions`

```yaml
paths:
  /posts/{postId}/like:
    post:
      operationId: toggleLike
      summary: Toggle like cho bài viết
      tags: [Reactions]
      security:
        - BearerAuth: []
      parameters:
        - name: postId
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200':
          description: Like toggled
          content:
            application/json:
              schema:
                type: object
                properties:
                  isLiked: { type: boolean }
                  totalLikes: { type: integer }
              example:
                isLiked: true
                totalLikes: 43
        '401': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }

  /posts/{postId}/bookmark:
    post:
      operationId: toggleBookmark
      summary: Toggle bookmark cho bài viết
      tags: [Reactions]
      security:
        - BearerAuth: []
      parameters:
        - name: postId
          in: path
          required: true
          schema: { type: string, format: uuid }
      responses:
        '200':
          description: Bookmark toggled
          content:
            application/json:
              schema:
                type: object
                properties:
                  isBookmarked: { type: boolean }
              example:
                isBookmarked: true
        '401': { $ref: '#/components/responses/Unauthorized' }
        '404': { $ref: '#/components/responses/NotFound' }
```

---

## 9.6 Users API

**Controller:** `UsersController.cs` · Route prefix: `/api/v1/users`

```yaml
paths:
  /users/{username}:
    get:
      operationId: getUserProfile
      summary: Lấy profile công khai của user
      tags: [Users]
      security: []                           # Public
      parameters:
        - name: username
          in: path
          required: true
          schema: { type: string }
      responses:
        '200':
          description: User public profile
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/UserProfileDto'
              example:
                id: "550e8400-e29b-41d4-a716-446655440000"
                displayName: "Nguyễn Văn A"
                bio: "Senior Software Engineer. Viết về .NET, Architecture, và DDD."
                avatarUrl: "https://cdn.blog-platform.dev/avatars/nguyen-van-a.webp"
                websiteUrl: "https://nguyenvana.dev"
                socialLinks:
                  github: "nguyenvana"
                  twitter: "nguyenvana_dev"
                postCount: 24
                joinedAt: "2025-06-15T00:00:00Z"
        '404': { $ref: '#/components/responses/NotFound' }

  /users/me/profile:
    put:
      operationId: updateProfile
      summary: Cập nhật profile của user hiện tại
      tags: [Users]
      security:
        - BearerAuth: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateProfileRequest'
            example:
              displayName: "Nguyễn Văn A"
              bio: "Senior Software Engineer"
              websiteUrl: "https://nguyenvana.dev"
              socialLinks:
                github: "nguyenvana"
      responses:
        '200':
          description: Profile updated
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/UserProfileDto'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '422': { $ref: '#/components/responses/ValidationError' }

  /users:
    get:
      operationId: getUserList
      summary: Danh sách users (Admin only)
      tags: [Users]
      security:
        - BearerAuth: []                     # Requires: Admin
      parameters:
        - name: page
          in: query
          schema: { type: integer, default: 1 }
        - name: pageSize
          in: query
          schema: { type: integer, default: 20 }
        - name: role
          in: query
          schema:
            type: string
            enum: [Admin, Editor, Author, Reader]
        - name: isActive
          in: query
          schema: { type: boolean }
      responses:
        '200':
          description: Paginated user list
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PaginatedUserList'
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }

  /users/{id}/role:
    put:
      operationId: assignRole
      summary: Gán role cho user (Admin only)
      tags: [Users]
      security:
        - BearerAuth: []                     # Requires: Admin
      parameters:
        - name: id
          in: path
          required: true
          schema: { type: string, format: uuid }
      requestBody:
        required: true
        content:
          application/json:
            schema:
              type: object
              required: [role]
              properties:
                role:
                  type: string
                  enum: [Admin, Editor, Author, Reader]
            example:
              role: "Editor"
      responses:
        '200':
          description: Role assigned
        '401': { $ref: '#/components/responses/Unauthorized' }
        '403': { $ref: '#/components/responses/Forbidden' }
        '404': { $ref: '#/components/responses/NotFound' }
```

---

## 9.7 Common Schemas

```yaml
components:
  schemas:
    # === Request Schemas ===
    CreatePostRequest:
      type: object
      required: [title, bodyJson]
      properties:
        title: { type: string, maxLength: 256 }
        excerpt: { type: string, maxLength: 512 }
        bodyJson: { type: object, description: "Tiptap v3 ProseMirror JSON" }
        coverImageUrl: { type: string, format: uri, maxLength: 2048 }
        tagIds:
          type: array
          items: { type: string, format: uuid }

    UpdatePostRequest:
      type: object
      properties:
        title: { type: string, maxLength: 256 }
        excerpt: { type: string, maxLength: 512 }
        bodyJson: { type: object }
        coverImageUrl: { type: string, format: uri, maxLength: 2048 }
        tagIds:
          type: array
          items: { type: string, format: uuid }

    RegisterRequest:
      type: object
      required: [email, password, displayName]
      properties:
        email: { type: string, format: email, maxLength: 256 }
        password: { type: string, minLength: 8, maxLength: 128 }
        displayName: { type: string, minLength: 2, maxLength: 128 }

    LoginRequest:
      type: object
      required: [email, password]
      properties:
        email: { type: string, format: email }
        password: { type: string }

    UpdateProfileRequest:
      type: object
      properties:
        displayName: { type: string, maxLength: 128 }
        bio: { type: string, maxLength: 2000 }
        avatarUrl: { type: string, format: uri }
        websiteUrl: { type: string, format: uri }
        socialLinks: { type: object, additionalProperties: { type: string } }

    # === Response Schemas ===
    PostDto:
      type: object
      properties:
        id: { type: string, format: uuid }
        title: { type: string }
        slug: { type: string }
        excerpt: { type: string }
        authorId: { type: string, format: uuid }
        authorDisplayName: { type: string }
        status: { type: string, enum: [Draft, Published, Archived] }
        coverImageUrl: { type: string }
        readingTimeMinutes: { type: integer }
        isFeatured: { type: boolean }
        publishedAt: { type: string, format: date-time, nullable: true }
        createdAt: { type: string, format: date-time }
        updatedAt: { type: string, format: date-time }
        tags:
          type: array
          items: { $ref: '#/components/schemas/TagDto' }
        likeCount: { type: integer }
        commentCount: { type: integer }

    PostDetailDto:
      allOf:
        - $ref: '#/components/schemas/PostDto'
        - type: object
          properties:
            bodyJson: { type: object, description: "Tiptap ProseMirror JSON" }
            bodyHtml: { type: string, description: "Pre-rendered HTML" }
            author:
              $ref: '#/components/schemas/AuthorSummary'
            isLikedByCurrentUser: { type: boolean }
            isBookmarkedByCurrentUser: { type: boolean }

    CommentDto:
      type: object
      properties:
        id: { type: string, format: uuid }
        postId: { type: string, format: uuid }
        author:
          $ref: '#/components/schemas/AuthorSummary'
        content: { type: string }
        parentId: { type: string, format: uuid, nullable: true }
        isApproved: { type: boolean }
        createdAt: { type: string, format: date-time }
        replies:
          type: array
          items: { $ref: '#/components/schemas/CommentDto' }

    TokenResponse:
      type: object
      properties:
        accessToken: { type: string }
        refreshToken: { type: string }
        expiresIn: { type: integer, description: "Seconds until access token expires" }
        tokenType: { type: string, default: "Bearer" }

    CurrentUserDto:
      type: object
      properties:
        id: { type: string, format: uuid }
        email: { type: string, format: email }
        displayName: { type: string }
        avatarUrl: { type: string }
        role: { type: string, enum: [Admin, Editor, Author, Reader] }
        permissions:
          type: array
          items: { type: string }

    UserProfileDto:
      type: object
      properties:
        id: { type: string, format: uuid }
        displayName: { type: string }
        bio: { type: string }
        avatarUrl: { type: string }
        websiteUrl: { type: string }
        socialLinks: { type: object }
        postCount: { type: integer }
        joinedAt: { type: string, format: date-time }

    TagDto:
      type: object
      properties:
        id: { type: string, format: uuid }
        name: { type: string }
        slug: { type: string }

    AuthorSummary:
      type: object
      properties:
        id: { type: string, format: uuid }
        displayName: { type: string }
        avatarUrl: { type: string }

    # === Pagination ===
    PaginatedPostList:
      type: object
      properties:
        items:
          type: array
          items: { $ref: '#/components/schemas/PostDto' }
        totalCount: { type: integer }
        page: { type: integer }
        pageSize: { type: integer }
        totalPages: { type: integer }

    PaginatedCommentList:
      type: object
      properties:
        items:
          type: array
          items: { $ref: '#/components/schemas/CommentDto' }
        totalCount: { type: integer }
        page: { type: integer }
        pageSize: { type: integer }

    PaginatedUserList:
      type: object
      properties:
        items:
          type: array
          items: { $ref: '#/components/schemas/UserProfileDto' }
        totalCount: { type: integer }
        page: { type: integer }
        pageSize: { type: integer }

    # === Error Responses ===
    ProblemDetails:
      type: object
      description: "RFC 9457 Problem Details format"
      properties:
        type: { type: string, format: uri }
        title: { type: string }
        status: { type: integer }
        detail: { type: string }
        instance: { type: string }
        errors:
          type: object
          additionalProperties:
            type: array
            items: { type: string }

  responses:
    Unauthorized:
      description: "401 — Missing hoặc invalid JWT token"
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
          example:
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.2"
            title: "Unauthorized"
            status: 401
            detail: "Bearer token is missing or invalid"

    Forbidden:
      description: "403 — Không đủ quyền (RBAC)"
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
          example:
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            title: "Forbidden"
            status: 403
            detail: "You do not have permission to perform this action"

    NotFound:
      description: "404 — Resource không tồn tại"
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
          example:
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.5"
            title: "Not Found"
            status: 404
            detail: "The requested resource was not found"

    ValidationError:
      description: "422 — Validation errors (FluentValidation)"
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
          example:
            type: "https://tools.ietf.org/html/rfc4918#section-11.2"
            title: "Validation Error"
            status: 422
            detail: "One or more validation errors occurred"
            errors:
              title: ["Title is required", "Title must not exceed 256 characters"]
              bodyJson: ["Content body is required"]
```

---
