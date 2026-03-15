#!/bin/bash
# EF Core migration helper for blog-platform
# Usage:
#   ./scripts/migration.sh add <MigrationName>     # Create new migration
#   ./scripts/migration.sh update                  # Apply all pending migrations
#   ./scripts/migration.sh list                    # List migrations and their status
#   ./scripts/migration.sh script <from> <to>      # Generate idempotent SQL (for production)
#   ./scripts/migration.sh rollback <target>       # Rollback to a specific migration

set -e

BLOG_API="apps/blog-api/src/Blog.API"
INFRASTRUCTURE="apps/blog-api/src/Blog.Infrastructure/Blog.Infrastructure.csproj"
CONTEXT="BlogDbContext"

case "$1" in
  add)
    if [ -z "$2" ]; then
      echo "Usage: $0 add <MigrationName>"
      exit 1
    fi
    dotnet ef migrations add "$2" \
      --project "$INFRASTRUCTURE" \
      --startup-project "$BLOG_API/Blog.API.csproj" \
      --context "$CONTEXT" \
      --output-dir Persistence/Migrations
    ;;
  update)
    dotnet ef database update \
      --project "$INFRASTRUCTURE" \
      --startup-project "$BLOG_API/Blog.API.csproj" \
      --context "$CONTEXT"
    ;;
  list)
    dotnet ef migrations list \
      --project "$INFRASTRUCTURE" \
      --startup-project "$BLOG_API/Blog.API.csproj" \
      --context "$CONTEXT"
    ;;
  script)
    FROM="${2:-0}"
    TO="${3:-}"
    dotnet ef migrations script "$FROM" $TO \
      --project "$INFRASTRUCTURE" \
      --startup-project "$BLOG_API/Blog.API.csproj" \
      --context "$CONTEXT" \
      --idempotent \
      --output migrations.sql
    echo "Idempotent SQL written to migrations.sql"
    ;;
  rollback)
    if [ -z "$2" ]; then
      echo "Usage: $0 rollback <TargetMigrationName>"
      exit 1
    fi
    dotnet ef database update "$2" \
      --project "$INFRASTRUCTURE" \
      --startup-project "$BLOG_API/Blog.API.csproj" \
      --context "$CONTEXT"
    ;;
  *)
    echo "Unknown command: $1"
    echo "Available commands: add, update, list, script, rollback"
    exit 1
    ;;
esac
