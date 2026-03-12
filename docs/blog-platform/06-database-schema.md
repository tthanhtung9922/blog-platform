# Database Schema

## 6.1 ERD — Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        DATABASE SCHEMA (PostgreSQL 18)                  │
│                     Blog Platform — DDD Aggregates                      │
└─────────────────────────────────────────────────────────────────────────┘

  ┌─────────────────────┐         ┌─────────────────────┐
  │   AspNetUsers        │         │   AspNetRoles        │
  │   (Identity Layer)   │         │   (Identity Layer)   │
  ├─────────────────────┤         ├─────────────────────┤
  │ Id          (PK, GUID)│◄──────│ Id          (PK, GUID)│
  │ UserName    VARCHAR   │  M:N  │ Name        VARCHAR   │
  │ Email       VARCHAR   │       │ NormalizedName        │
  │ PasswordHash          │       └─────────────────────┘
  │ EmailConfirmed BOOL   │              │
  │ LockoutEnd   DTOFFSET │              │ AspNetUserRoles (join table)
  │ ...Identity columns   │              │
  └──────────┬────────────┘              │
             │ Shared GUID (ADR-006)     │
             │ (NOT FK — logical link)   │
             ▼                           │
  ┌─────────────────────┐               │
  │   users              │◄──────────────┘
  │   (Domain Layer)     │
  ├─────────────────────┤        ┌─────────────────────┐
  │ id          (PK, GUID)│       │   user_profiles      │
  │ display_name VARCHAR  │       ├─────────────────────┤
  │ bio          TEXT     │──1:1─►│ id          (PK, GUID)│
  │ avatar_url   VARCHAR  │       │ user_id     (FK, UQ) │
  │ role         VARCHAR  │       │ website_url  VARCHAR │
  │ is_active    BOOL     │       │ social_links JSONB   │
  │ created_at   TIMESTAMPTZ│     │ location     VARCHAR │
  │ updated_at   TIMESTAMPTZ│     └─────────────────────┘
  └──────────┬────────────┘
             │
     ┌───────┼──────────────────────────┐
     │       │                          │
     ▼       ▼                          ▼
  ┌─────────────────────┐   ┌─────────────────────┐   ┌──────────────────┐
  │   posts              │   │   comments           │   │   bookmarks      │
  ├─────────────────────┤   ├─────────────────────┤   ├──────────────────┤
  │ id       (PK, GUID) │   │ id       (PK, GUID) │   │ id    (PK, GUID) │
  │ author_id (FK→users)│   │ post_id  (FK→posts) │   │ user_id (FK)     │
  │ title     VARCHAR(256)│  │ author_id (FK→users)│   │ post_id (FK)     │
  │ slug      VARCHAR(256)│  │ parent_id (FK→self) │   │ created_at       │
  │ excerpt   VARCHAR(512)│  │ content   TEXT      │   │ (UQ: user+post)  │
  │ status    VARCHAR(20) │  │ is_approved BOOL    │   └──────────────────┘
  │ cover_image_url      │   │ created_at TIMESTAMPTZ│
  │ reading_time_minutes │   │ updated_at TIMESTAMPTZ│  ┌──────────────────┐
  │ is_featured  BOOL    │   └─────────────────────┘   │   likes           │
  │ published_at TIMESTAMPTZ│                          ├──────────────────┤
  │ created_at  TIMESTAMPTZ│                           │ id    (PK, GUID) │
  │ updated_at  TIMESTAMPTZ│                           │ user_id (FK)     │
  └──────────┬────────────┘                            │ post_id (FK)     │
             │                                         │ created_at       │
     ┌───────┼───────────┐                             │ (UQ: user+post)  │
     │       │           │                             └──────────────────┘
     ▼       ▼           ▼
  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐
  │ post_contents│ │ post_versions│ │ post_tags (join)  │
  ├──────────────┤ ├──────────────┤ ├──────────────────┤
  │ id  (PK,GUID)│ │ id  (PK,GUID)│ │ post_id (FK, PK) │
  │ post_id (FK) │ │ post_id (FK) │ │ tag_id  (FK, PK) │
  │ body_json    │ │ version_num  │ └──────────────────┘
  │   JSONB      │ │ title VARCHAR│         │
  │ body_html    │ │ body_json    │         ▼
  │   TEXT       │ │   JSONB      │ ┌──────────────────┐
  │ updated_at   │ │ created_at   │ │   tags            │
  └──────────────┘ │ created_by   │ ├──────────────────┤
                   │  (FK→users)  │ │ id    (PK, GUID) │
                   └──────────────┘ │ name  VARCHAR(64) │
                                    │ slug  VARCHAR(64) │
                                    │ (UQ: slug)        │
                                    └──────────────────┘
```

**Quan hệ giữa các Aggregates:**

| Quan hệ | Loại | Ghi chú |
|---|---|---|
| `users` ↔ `AspNetUsers` | 1:1 logical | Shared GUID, KHÔNG có FK constraint (ADR-006) |
| `users` → `user_profiles` | 1:1 | FK + Unique constraint |
| `users` → `posts` | 1:N | `posts.author_id` FK |
| `users` → `comments` | 1:N | `comments.author_id` FK |
| `posts` → `post_contents` | 1:1 | Current content (Tiptap JSON + rendered HTML) |
| `posts` → `post_versions` | 1:N | Content history / versioning |
| `posts` ↔ `tags` | M:N | Via `post_tags` join table |
| `posts` → `comments` | 1:N | `comments.post_id` FK |
| `comments` → `comments` | 1:N (self) | `comments.parent_id` FK — nested replies |
| `users` → `likes` | 1:N | Unique constraint (user_id, post_id) |
| `users` → `bookmarks` | 1:N | Unique constraint (user_id, post_id) |

---

## 6.2 Table Definitions

#### `posts` — Aggregate Root

```sql
CREATE TABLE posts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    author_id       UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    title           VARCHAR(256) NOT NULL,
    slug            VARCHAR(256) NOT NULL,
    excerpt         VARCHAR(512),
    status          VARCHAR(20) NOT NULL DEFAULT 'Draft'
                    CHECK (status IN ('Draft', 'Published', 'Archived')),
    cover_image_url VARCHAR(2048),
    reading_time_minutes SMALLINT,
    is_featured     BOOLEAN NOT NULL DEFAULT FALSE,
    published_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_posts_slug UNIQUE (slug)
);

-- Indexes: xem Section 6.3
```

#### `post_contents` — Entity (belongs to Post)

```sql
CREATE TABLE post_contents (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id     UUID NOT NULL UNIQUE REFERENCES posts(id) ON DELETE CASCADE,
    body_json   JSONB NOT NULL,                    -- Tiptap v3 ProseMirror JSON (primary)
    body_html   TEXT NOT NULL DEFAULT '',           -- Pre-rendered HTML (for SSG/ISR performance)
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

#### `post_versions` — Entity (content versioning)

```sql
CREATE TABLE post_versions (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id     UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    version_num INTEGER NOT NULL,
    title       VARCHAR(256) NOT NULL,
    body_json   JSONB NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by  UUID NOT NULL REFERENCES users(id),

    CONSTRAINT uq_post_versions UNIQUE (post_id, version_num)
);
```

#### `comments` — Aggregate Root (nested via self-reference)

```sql
CREATE TABLE comments (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id     UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    author_id   UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    parent_id   UUID REFERENCES comments(id) ON DELETE CASCADE,  -- NULL = top-level comment
    content     TEXT NOT NULL CHECK (length(content) <= 5000),
    is_approved BOOLEAN NOT NULL DEFAULT TRUE,                   -- moderation flag
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

#### `users` — Aggregate Root (Domain Layer)

```sql
CREATE TABLE users (
    id           UUID PRIMARY KEY,                               -- same as AspNetUsers.Id (ADR-006)
    display_name VARCHAR(128) NOT NULL,
    bio          TEXT,
    avatar_url   VARCHAR(2048),
    role         VARCHAR(20) NOT NULL DEFAULT 'Reader'
                 CHECK (role IN ('Admin', 'Editor', 'Author', 'Reader')),
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

#### `user_profiles` — Entity (extended profile data)

```sql
CREATE TABLE user_profiles (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    website_url  VARCHAR(2048),
    social_links JSONB DEFAULT '{}',                             -- {"github": "...", "twitter": "..."}
    location     VARCHAR(256),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

#### `tags`, `post_tags` — Value Object + Join Table

```sql
CREATE TABLE tags (
    id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(64) NOT NULL,
    slug VARCHAR(64) NOT NULL,

    CONSTRAINT uq_tags_slug UNIQUE (slug)
);

CREATE TABLE post_tags (
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    tag_id  UUID NOT NULL REFERENCES tags(id) ON DELETE CASCADE,

    PRIMARY KEY (post_id, tag_id)
);
```

#### `likes`, `bookmarks` — Reactions

```sql
CREATE TABLE likes (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    post_id    UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_likes_user_post UNIQUE (user_id, post_id)
);

CREATE TABLE bookmarks (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    post_id    UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_bookmarks_user_post UNIQUE (user_id, post_id)
);
```

---

## 6.3 Index Strategy

```sql
-- === Posts ===
CREATE INDEX idx_posts_author_id ON posts(author_id);
CREATE INDEX idx_posts_status ON posts(status) WHERE status = 'Published';     -- partial index
CREATE INDEX idx_posts_published_at ON posts(published_at DESC NULLS LAST)
    WHERE status = 'Published';                                                 -- feed pagination
CREATE INDEX idx_posts_is_featured ON posts(is_featured)
    WHERE is_featured = TRUE AND status = 'Published';                          -- homepage featured
CREATE INDEX idx_posts_slug ON posts(slug);                                    -- slug lookup (covered by UQ)

-- Full-text search (ADR-009 — Vietnamese support)
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE INDEX idx_posts_fts ON posts
    USING GIN (to_tsvector('vietnamese', title || ' ' || COALESCE(excerpt, '')));

-- === Comments ===
CREATE INDEX idx_comments_post_id ON comments(post_id, created_at);            -- comments by post
CREATE INDEX idx_comments_author_id ON comments(author_id);
CREATE INDEX idx_comments_parent_id ON comments(parent_id)
    WHERE parent_id IS NOT NULL;                                                -- nested replies
CREATE INDEX idx_comments_moderation ON comments(is_approved, created_at)
    WHERE is_approved = FALSE;                                                  -- moderation queue

-- === Tags ===
CREATE INDEX idx_post_tags_tag_id ON post_tags(tag_id);                        -- posts by tag
CREATE INDEX idx_tags_slug ON tags(slug);                                      -- covered by UQ

-- === Reactions ===
CREATE INDEX idx_likes_post_id ON likes(post_id);                              -- like count per post
CREATE INDEX idx_bookmarks_user_id ON bookmarks(user_id, created_at DESC);     -- user's bookmarks

-- === Post Versions ===
CREATE INDEX idx_post_versions_post_id ON post_versions(post_id, version_num DESC);
```

**Index Design Principles:**

- **Partial indexes** cho `status = 'Published'` — phần lớn queries chỉ cần published posts, giảm index size
- **Covering indexes** — `(post_id, created_at)` cho comments pagination tránh table lookup
- **GIN index** cho full-text search — hiệu quả với `to_tsvector`
- **Unique constraints** tự tạo index — không duplicate index cho slug, user+post reactions

---
