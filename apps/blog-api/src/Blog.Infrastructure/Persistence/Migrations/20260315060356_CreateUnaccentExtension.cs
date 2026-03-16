using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateUnaccentExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // suppressTransaction: true is REQUIRED — CREATE EXTENSION cannot run inside
            // a transaction block. Without this, MigrateAsync() throws:
            //   "ERROR: CREATE EXTENSION cannot run inside a transaction block"
            migrationBuilder.Sql(
                "CREATE EXTENSION IF NOT EXISTS unaccent;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP EXTENSION IF EXISTS unaccent;",
                suppressTransaction: true);
        }
    }
}
