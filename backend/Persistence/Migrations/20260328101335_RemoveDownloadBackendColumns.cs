using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDownloadBackendColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtensionCaptureCloseTabOnFinish",
                table: "ServerSettings");

            migrationBuilder.DropColumn(
                name: "ExtensionCaptureDefaultTimeoutMs",
                table: "ServerSettings");

            migrationBuilder.DropColumn(
                name: "ExtensionCaptureEnabled",
                table: "ServerSettings");

            migrationBuilder.DropColumn(
                name: "ExtensionCaptureLogLevel",
                table: "ServerSettings");

            migrationBuilder.DropColumn(
                name: "ExtensionCaptureTabReuse",
                table: "ServerSettings");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExtensionCaptureCloseTabOnFinish",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ExtensionCaptureDefaultTimeoutMs",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ExtensionCaptureEnabled",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionCaptureLogLevel",
                table: "ServerSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ExtensionCaptureTabReuse",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

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
                defaultValue: "");

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
    }
}
