---
description: >
  Apply these rules whenever creating or modifying database tables, columns, indexes,
  EF Core entity configurations, migrations, or SQL queries. Triggers on any work
  involving Blog.Infrastructure/Persistence, migration files, schema changes, or
  direct SQL.
---

# Database Rules

## MUST

- **Table and column names use snake_case** — PostgreSQL convention.
  ```sql
  CREATE TABLE post_contents (    -- snake_case table
      post_id UUID NOT NULL,      -- snake_case column
      body_json JSONB NOT NULL,
      created_at TIMESTAMPTZ
  );
  ```
- **All tables use UUID primary keys** via `gen_random_uuid()`.
- **All timestamp columns use `TIMESTAMPTZ`** (timestamp with time zone), never `TIMESTAMP`.
- **EF Core entity configurations go in `Blog.Infrastructure/Persistence/Configurations/`** — one file per entity, implementing `IEntityTypeConfiguration<T>`.
- **Migration naming follows established patterns:**
  | Pattern | When to use |
  |---|---|
  | `Add<Entity>` | New table |
  | `Add<Column>To<Table>` | New column |
  | `Remove<Column>From<Table>` | Drop column |
  | `AddIndex<Name>` | New index |
  | `Alter<Column>In<Table>` | Type/constraint change |
  | `Seed<Data>` | Data seeding |
  | `Create<Extension>` | PostgreSQL extension |
- **Always review both `Up()` and `Down()` methods** before applying any migration. Every migration must have a working `Down()` for rollback.
- **Destructive migrations (DROP TABLE, DROP COLUMN, ALTER data type) require 2 reviewers.**
- **Backward-compatible migrations:** column drops and renames use a 2-phase approach:
  1. Phase 1: Deploy code that stops using the column
  2. Phase 2: Migration removes the column
- **Large table migrations (> 1M rows):** use `CREATE INDEX CONCURRENTLY` and set `Migration.SuppressTransaction = true` for that migration.
- **`users` and `AspNetUsers` are separate tables** with no FK constraint between them — linked only by shared GUID (ADR-006).
- **Post content is stored as Tiptap ProseMirror JSON** in `body_json` (JSONB) column. `body_html` (TEXT) is pre-rendered for SSG/ISR performance.

## SHOULD

- Use **partial indexes** for status-filtered queries:
  ```sql
  CREATE INDEX idx_posts_status ON posts(status) WHERE status = 'Published';
  ```
- Use **covering indexes** for pagination queries to avoid table lookups:
  ```sql
  CREATE INDEX idx_comments_post_id ON comments(post_id, created_at);
  ```
- Use `ON DELETE RESTRICT` for user references (prevent orphaned content) and `ON DELETE CASCADE` for owned entities (post_contents, post_versions).
- Generate idempotent SQL scripts for production deployments:
  ```bash
  ./scripts/migration.sh script <from> <to>  # generates idempotent SQL
  ```

## NEVER

- Never apply migrations directly to production — generate SQL script, review, get approval, then apply.
  ```bash
  # NEVER on production
  dotnet ef database update

  # CORRECT for production
  dotnet ef migrations script --idempotent --output migrations.sql
  # Review → Approve → psql -f migrations.sql
  ```
- Never skip the `Down()` method in a migration — rollback capability is mandatory.
- Never use `TIMESTAMP` without time zone — always `TIMESTAMPTZ`.
- Never create a FK constraint between `users` and `AspNetUsers` tables.

## Edge Cases

- **PostgreSQL extensions** (like `unaccent` for Vietnamese FTS) must be created in their own migration since they require superuser privileges and may need separate deployment steps.
- **Data migrations** (transforming existing data) go in the `Up()` method AFTER schema changes, using raw SQL via `migrationBuilder.Sql()`.
