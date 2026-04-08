using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddYtDlpDownloadRetrySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadRetryDelaysSecondsJson",
                table: "YtDlpConfig",
                type: "TEXT",
                nullable: false,
                defaultValue: "[30,60,120]");

            migrationBuilder.AddColumn<int>(
                name: "DownloadTransientMaxRetries",
                table: "YtDlpConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadRetryDelaysSecondsJson",
                table: "YtDlpConfig");

            migrationBuilder.DropColumn(
                name: "DownloadTransientMaxRetries",
                table: "YtDlpConfig");
        }
    }
}
