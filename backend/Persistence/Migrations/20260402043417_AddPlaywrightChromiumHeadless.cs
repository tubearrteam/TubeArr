using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <summary>
    /// No-op: Playwright was shipped as runtime/filesystem assets only; nothing in SQLite needed to change.
    /// This migration id must stay in the project so databases that already have
    /// <c>20260402043417_AddPlaywrightChromiumHeadless</c> in <c>__EFMigrationsHistory</c> still match the assembly
    /// (removing it would break <c>dotnet ef</c> / startup migration checks for those DBs).
    /// </summary>
    public partial class AddPlaywrightChromiumHeadless : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberate no-op — see class summary.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberate no-op — see class summary.
        }
    }
}
