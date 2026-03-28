using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtensionCaptureSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExtensionCaptureCloseTabOnFinish",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtensionCaptureDefaultTimeoutMs",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 120000);

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
                defaultValue: "info");

            migrationBuilder.AddColumn<bool>(
                name: "ExtensionCaptureTabReuse",
                table: "ServerSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
