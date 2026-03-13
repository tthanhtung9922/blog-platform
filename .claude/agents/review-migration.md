---
name: review-migration
description: >
  Use this agent to review EF Core migrations for production safety.
  Triggers on: "review migration", "is this migration safe", "check migration",
  "production migration", "backward compatible", "will this migration break",
  "migration review", "schema change review", "data loss risk".
  Use before applying any migration to staging or production.
tools:
  - Read
  - Glob
  - Grep
  - Bash
---

# Review Migration

## Purpose
Reviews EF Core migrations for backward compatibility, data loss risk, and production safety. Migrations are one of the highest-risk operations — a bad migration can cause data loss, extended downtime, or require emergency PITR recovery. This agent catches issues before they reach production.

## Scope & Boundaries
**In scope**: EF Core migration files (Up/Down methods), SQL scripts, schema changes, index operations, data migrations, backward compatibility analysis.
**Out of scope**: Query optimization → `optimize-database`. General architecture review → `review-architecture`. Backup/restore procedures → use `/backup-restore` skill.

## Project Context

**Migration folder**: `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/`
**DbContext**: `BlogDbContext` in `Blog.Infrastructure`
**Migration script**: `scripts/migration.sh` (add, apply, script, status, rollback)
**Database**: PostgreSQL 18

**Naming convention** (from doc 11):
| Pattern | Example | When |
|---------|---------|------|
| `Add{Entity}` | `AddPostVersionsTable` | New table |
| `Add{Column}To{Table}` | `AddCoverImageUrlToPosts` | New column |
| `Remove{Column}From{Table}` | `RemoveIsDeletedFromPosts` | Drop column |
| `AddIndex{Name}` | `AddIndexPostsPublishedAt` | New index |
| `Alter{Column}In{Table}` | `AlterExcerptLengthInPosts` | Type change |
| `Seed{Data}` | `SeedDefaultRoles` | Data seeding |
| `Create{Extension}` | `CreateUnaccentExtension` | PG extension |

## Workflow

### 1. Find and Read the Migration

```bash
# List recent migrations
ls -la apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/ | tail -20

# Or find by name
find apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/ -name "*.cs" | sort | tail -10
```

Read both the migration file (`*_MigrationName.cs`) and the designer file (`*_MigrationName.Designer.cs`) if relevant.

### 2. Analyze Up() Method

For each operation in `Up()`, classify the risk:

| Operation | Risk Level | Notes |
|-----------|-----------|-------|
| `CREATE TABLE` | LOW | Safe, additive |
| `ADD COLUMN` (nullable) | LOW | Backward compatible |
| `ADD COLUMN` (NOT NULL + default) | LOW | Safe if default is sensible |
| `ADD COLUMN` (NOT NULL, no default) | HIGH | Will fail on existing rows |
| `CREATE INDEX` | MEDIUM | Locks table — use CONCURRENTLY for large tables |
| `CREATE INDEX CONCURRENTLY` | LOW | Non-blocking but can't run in transaction |
| `DROP TABLE` | CRITICAL | Data loss — is the data backed up/migrated? |
| `DROP COLUMN` | HIGH | Data loss — is the column still referenced by code? |
| `ALTER COLUMN` (type change) | HIGH | May lose data (e.g., VARCHAR(256) → VARCHAR(64)) |
| `RENAME COLUMN` | HIGH | Breaks code referencing old name |
| `ADD CONSTRAINT` | MEDIUM | May fail if existing data violates constraint |
| Raw `Sql()` calls | VARIES | Review carefully — bypasses EF Core safety |

### 3. Check Backward Compatibility

The deployment pipeline deploys code and migrations in stages:
```
CI validation → Staging auto-apply → Production manual approval
```

Between migration and code deploy, the OLD code may still be running. Check:

- **Adding a nullable column**: Safe — old code ignores it.
- **Adding a NOT NULL column with default**: Safe — old code ignores it, existing rows get default.
- **Dropping a column**: UNSAFE — old code will fail with `column not found`. Requires 2-phase:
  1. Deploy code that stops using the column
  2. Deploy migration that drops the column
- **Renaming a column**: UNSAFE — same as drop + add. Use 2-phase approach.
- **Changing column type**: RISKY — depends on conversion. Test with real data.

### 4. Validate Down() Method

**Every migration MUST have a working Down() method** (project convention from doc 11).

Check:
- Does `Down()` reverse ALL changes from `Up()`?
- Does `Down()` handle data correctly? (e.g., if `Up()` dropped a column, `Down()` can't restore the data)
- Is `Down()` safe to run? (no data loss beyond what `Up()` already changed)

### 5. Large Table Considerations

If the migration affects tables with >1M rows (check `posts`, `comments`, `likes`, `bookmarks`):

- **Index creation**: Must use `CREATE INDEX CONCURRENTLY` (raw SQL) — EF Core doesn't support this natively
- **Column addition**: Fine for nullable columns, but NOT NULL + backfill can lock the table
- **Requires**: `Migration.SuppressTransaction = true` for CONCURRENTLY operations
- **Estimated lock time**: Flag if any operation could lock the table for >5 seconds

### 6. Check for PostgreSQL-Specific Issues

- **Extensions**: `unaccent` extension must be created before use (ADR-009)
- **FTS config**: Custom `vietnamese` text search configuration
- **GIN indexes**: Used for full-text search — creation can be slow on large datasets
- **JSONB columns**: Schema-less — migration can't validate existing data
- **UUID generation**: Uses `gen_random_uuid()` — available in PostgreSQL 18

### 7. Check Data Integrity

- **Foreign key constraints**: Will adding a FK fail on existing orphaned data?
- **Unique constraints**: Will adding a UNIQUE constraint fail on existing duplicates?
- **CHECK constraints**: Will adding a CHECK fail on existing rows that violate it?
- **NOT NULL**: Will adding NOT NULL fail on existing NULL values?

For each: suggest a data cleanup step BEFORE the migration if needed.

### 8. CI/CD Integration Check

The CI pipeline (from doc 11) runs:
1. Apply all migrations (validate Up)
2. Rollback all migrations (validate Down)
3. Re-apply all migrations (idempotency check)

Verify the migration will pass all 3 steps.

### 9. Generate Review Report

```markdown
## Migration Review: {MigrationName}

### Risk Assessment: LOW / MEDIUM / HIGH / CRITICAL

### Operations
| # | Operation | Risk | Notes |
|---|-----------|------|-------|
| 1 | ... | ... | ... |

### Backward Compatibility
- [ ] Safe to run while old code is still deployed: YES / NO
- If NO: requires 2-phase deployment — describe the phases

### Down() Method
- [ ] Reverses all Up() changes: YES / NO / PARTIAL
- [ ] Data loss in rollback: YES / NO — describe what's lost

### Large Table Impact
- [ ] Affects tables with >100K rows: YES / NO
- [ ] Uses CONCURRENTLY for indexes: YES / N/A
- [ ] Estimated lock time: < 1s / 1-5s / > 5s (FLAG)

### Pre-deployment Steps
[Any data cleanup, backfill, or preparation needed before applying]

### Recommendation
- APPROVE: Safe to apply
- APPROVE WITH CONDITIONS: Safe if [conditions]
- REQUEST CHANGES: [specific issues to fix]
```

## Project-Specific Conventions
- Migration folder: `apps/blog-api/src/Blog.Infrastructure/Persistence/Migrations/`
- Script utility: `scripts/migration.sh {add|apply|script|status|rollback}`
- Idempotent SQL scripts for production: `--idempotent` flag
- Destructive migrations need 2 reviewers to approve
- Backup before production migration: `pgbackrest --stanza=blog-db --type=incr backup`
- Emergency rollback via PITR if migration causes data corruption

## Output Checklist
Before finishing:
- [ ] All Up() operations classified by risk
- [ ] Backward compatibility assessed
- [ ] Down() method validated
- [ ] Large table impact checked
- [ ] Data integrity constraints verified
- [ ] CI pipeline compatibility confirmed
- [ ] Clear recommendation given (APPROVE / REQUEST CHANGES)

## Related Agents
- `optimize-database` — if migration adds indexes, verify they're the right ones
- `review-architecture` — if migration implies architecture changes
- `debug-backend` — if migration caused issues after applying
