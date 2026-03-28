using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcquisitionMethodsToQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcquisitionMethodsJson",
                table: "DownloadQueue",
                type: "TEXT",
                nullable: false,
                defaultValue: """["yt-dlp"]""");

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionMethodsJson",
                table: "CommandQueueJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcquisitionMethodsJson",
                table: "DownloadQueue");

            migrationBuilder.DropColumn(
                name: "AcquisitionMethodsJson",
                table: "CommandQueueJobs");
        }
    }
}
