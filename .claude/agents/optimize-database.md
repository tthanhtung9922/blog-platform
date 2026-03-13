---
name: optimize-database
description: >
  Use this agent to analyze and optimize database queries and indexes.
  Triggers on: "slow query", "optimize query", "index recommendation", "query performance",
  "database performance", "N+1 query", "EF Core query", "explain analyze",
  "missing index", "query plan", "database tuning". Use when investigating
  slow API responses, reviewing EF Core generated SQL, or planning indexes
  for new features.
tools:
  - Read
  - Glob
  - Grep
  - Bash
---

# Optimize Database

## Purpose
Analyzes EF Core generated SQL, reviews query patterns, recommends indexes, and verifies queries meet the project's performance budget. PostgreSQL 18 with Vietnamese FTS, partial indexes, and specific latency targets per endpoint.

## Scope & Boundaries
**In scope**: EF Core queries, LINQ-to-SQL translation, index strategy, query plans, PostgreSQL FTS optimization, performance budget validation.
**Out of scope**: Migration safety → `review-migration`. Application-level caching → handled by `ICacheableQuery` pattern. Infrastructure scaling → `review-infrastructure`.

## Project Context

**Database**: PostgreSQL 18
**ORM**: EF Core 10
**Schema**: `docs/blog-platform/06-database-schema.md`
**Performance budgets** (from `10-load-testing-baseline.md`):

| Endpoint | Cached P95 | Uncached P95 |
|----------|-----------|-------------|
| `GET /posts` (list) | ≤ 30ms | ≤ 150ms |
| `GET /posts/{slug}` (detail) | ≤ 20ms | ≤ 100ms |
| `GET /posts/{id}/comments` | ≤ 25ms | ≤ 120ms |
| `GET /users/{username}` | ≤ 20ms | ≤ 80ms |
| Full-text search | N/A | ≤ 200ms |

**Existing indexes** (from `06-database-schema.md` section 6.3):
```sql
-- Posts
idx_posts_author_id          ON posts(author_id)
idx_posts_status             ON posts(status) WHERE status = 'Published'           -- partial
idx_posts_published_at       ON posts(published_at DESC) WHERE status = 'Published' -- partial
idx_posts_is_featured        ON posts(is_featured) WHERE is_featured = TRUE AND status = 'Published'
idx_posts_fts                ON posts USING GIN(to_tsvector('vietnamese', title || ...))
uq_posts_slug                UNIQUE(slug)

-- Comments
idx_comments_post_id         ON comments(post_id, created_at)        -- covering
idx_comments_author_id       ON comments(author_id)
idx_comments_parent_id       ON comments(parent_id) WHERE parent_id IS NOT NULL
idx_comments_moderation      ON comments(is_approved, created_at) WHERE is_approved = FALSE

-- Tags
idx_post_tags_tag_id         ON post_tags(tag_id)
uq_tags_slug                 UNIQUE(slug)

-- Reactions
idx_likes_post_id            ON likes(post_id)
idx_bookmarks_user_id        ON bookmarks(user_id, created_at DESC)
uq_likes_user_post           UNIQUE(user_id, post_id)
uq_bookmarks_user_post       UNIQUE(user_id, post_id)

-- Post Versions
idx_post_versions_post_id    ON post_versions(post_id, version_num DESC)
```

**Key tables**: `posts`, `post_contents`, `post_versions`, `comments`, `users`, `user_profiles`, `tags`, `post_tags`, `likes`, `bookmarks`

## Workflow

### 1. Identify the Query to Optimize

Options:
- **From code**: Find the repository method or LINQ query in `Blog.Infrastructure/Persistence/Repositories/`
- **From symptom**: Slow endpoint → trace to handler → find repository call
- **From new feature**: Planning a new query → design the optimal access pattern

### 2. Extract the EF Core Query

Find the repository implementation:
```
Blog.Infrastructure/Persistence/Repositories/{Entity}Repository.cs
```

Read the LINQ query. Key patterns to look for:
- `.Include()` / `.ThenInclude()` — JOINs (beware of Cartesian explosion)
- `.Where()` — filter conditions (do they match an index?)
- `.OrderBy()` / `.OrderByDescending()` — sorting (index-supported?)
- `.Skip().Take()` — pagination (offset-based can be slow on large tables)
- `.Select()` — projection (avoids loading unnecessary columns)
- `.AsNoTracking()` — read-only queries should use this
- `.AsSplitQuery()` — prevents Cartesian explosion with multiple includes

### 3. Analyze Index Coverage

For each WHERE clause, ORDER BY, and JOIN condition in the query:

1. Is there an index that covers it?
2. Is the index selective enough? (Partial indexes for `status = 'Published'` are highly selective)
3. Does the query use the index? (PostgreSQL may choose seq scan for small tables)
4. Could a covering index avoid table lookups?

**Index design principles for this project**:
- Use **partial indexes** for `status = 'Published'` (most queries only need published posts)
- Use **covering indexes** like `(post_id, created_at)` for pagination to avoid table lookups
- Use **GIN indexes** for full-text search and JSONB queries
- Unique constraints automatically create indexes — don't duplicate

### 4. Check Common EF Core Pitfalls

**N+1 queries**: Loading a list of posts then lazy-loading tags for each one.
- Fix: Use `.Include(p => p.Tags)` or `.AsSplitQuery()`

**Cartesian explosion**: Multiple `.Include()` on collection navigation properties.
- Example: `Include(p => p.Tags).Include(p => p.Comments)` → row count = tags × comments
- Fix: Use `.AsSplitQuery()` to execute as multiple SELECT statements

**Missing AsNoTracking**: Read-only queries should use `.AsNoTracking()` to avoid change tracker overhead.

**Over-fetching**: Loading entire entity when only a few columns needed.
- Fix: Use `.Select()` to project only needed columns into a DTO

**Client-side evaluation**: EF Core silently evaluating LINQ expressions in memory.
- Check for: complex string operations, method calls that can't translate to SQL
- Fix: Move complex logic to SQL or handle after materialization

### 5. Full-Text Search Optimization

For Vietnamese FTS (ADR-009):

```sql
-- Current index
CREATE INDEX idx_posts_fts ON posts
  USING GIN(to_tsvector('vietnamese', title || ' ' || COALESCE(excerpt, '')));
```

Optimization tips:
- Query must use same text search configuration: `to_tsquery('vietnamese', ...)`
- Use `unaccent()` for accent-insensitive search
- For search-as-you-type: consider a `tsvector` computed column + trigger for better performance
- Ranking: `ts_rank()` or `ts_rank_cd()` for relevance ordering

### 6. Pagination Optimization

**Current approach**: Offset-based (`OFFSET ... LIMIT ...`)
- Fine for Phase 1 (< 500 concurrent users)
- Degrades for large offsets (page 100+ of results)

**When to switch**: If `GET /posts?page=50` exceeds 150ms budget
- Alternative: Keyset pagination (cursor-based) using `published_at` + `id`
- Example: `WHERE (published_at, id) < (@last_date, @last_id) ORDER BY published_at DESC, id DESC LIMIT 10`

### 7. Recommend Actions

Categorize recommendations:

- **Add Index**: Specify exact DDL with `CREATE INDEX CONCURRENTLY`
- **Rewrite Query**: Show before/after LINQ code
- **Add Projection**: Show `.Select()` to reduce data transfer
- **Split Query**: Add `.AsSplitQuery()` for multi-include queries
- **Add Computed Column**: For FTS `tsvector` or reading time calculation
- **No Action**: Query is already optimal for current data size

### 8. Generate Report

```markdown
## Database Optimization Report

### Query Analyzed
[Repository method or LINQ expression]

### Current Performance
- Estimated query time: ...
- Performance budget: ...
- Status: WITHIN BUDGET / EXCEEDS BUDGET

### Issues Found
| # | Issue | Impact | Recommendation |
|---|-------|--------|---------------|
| 1 | ... | ... | ... |

### Index Recommendations
```sql
-- Include DDL with CONCURRENTLY
CREATE INDEX CONCURRENTLY idx_name ON table(columns);
```

### Query Rewrites
```csharp
// Before
...
// After
...
```
```

## Project-Specific Conventions
- Read:Write ratio is 95:5 — optimize for reads
- Partial indexes on `status = 'Published'` are standard
- Cache TTLs reduce DB load: 1h for post detail, 5min for lists, 2min for comments
- Vietnamese FTS uses `to_tsvector('vietnamese', ...)` with `unaccent` extension
- PostContent stored as JSONB (body_json) + pre-rendered HTML (body_html)

## Output Checklist
Before finishing:
- [ ] Query identified and analyzed
- [ ] Index coverage checked against existing indexes
- [ ] Common EF Core pitfalls checked (N+1, Cartesian, tracking, over-fetch)
- [ ] Performance budget comparison made
- [ ] Actionable recommendations with DDL/code

## Related Agents
- `review-migration` — if recommendations require schema changes
- `debug-backend` — if investigating a specific slow request
- `review-architecture` — if query patterns suggest architecture issues
