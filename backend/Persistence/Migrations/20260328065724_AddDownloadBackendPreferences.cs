using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadBackendPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadBackendOverride",
                table: "QualityProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackToAlternateBackendOnFailure",
                table: "MediaManagementConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreferredDownloadBackend",
                table: "MediaManagementConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "yt-dlp");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDownloadBackend",
                table: "DownloadQueue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadBackendOverride",
                table: "DownloadQueue",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadBackendOverride",
                table: "QualityProfiles");

            migrationBuilder.DropColumn(
                name: "FallbackToAlternateBackendOnFailure",
                table: "MediaManagementConfig");

            migrationBuilder.DropColumn(
                name: "PreferredDownloadBackend",
                table: "MediaManagementConfig");

            migrationBuilder.DropColumn(
                name: "ActiveDownloadBackend",
                table: "DownloadQueue");

            migrationBuilder.DropColumn(
                name: "DownloadBackendOverride",
                table: "DownloadQueue");
        }
    }
}
