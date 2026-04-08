using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlskdConfigAndExternalAcquisition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalAcquisitionJson",
                table: "DownloadQueue",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalWorkPending",
                table: "DownloadQueue",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SlskdConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    LocalDownloadsPath = table.Column<string>(type: "TEXT", nullable: false),
                    SearchTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCandidatesStored = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoPickMinScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ManualReviewOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetryAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    AcquisitionOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    FallbackToSlskdOnYtDlpFailure = table.Column<bool>(type: "INTEGER", nullable: false),
                    FallbackToYtDlpOnSlskdFailure = table.Column<bool>(type: "INTEGER", nullable: false),
                    HigherQualityHandling = table.Column<int>(type: "INTEGER", nullable: false),
                    RequireManualReviewOnTranscode = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeepOriginalAfterTranscode = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlskdConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadQueue_Status_ExternalWorkPending",
                table: "DownloadQueue",
                columns: new[] { "Status", "ExternalWorkPending" });

            migrationBuilder.InsertData(
                table: "SlskdConfig",
                columns: new[]
                {
                    "Id", "Enabled", "BaseUrl", "ApiKey", "LocalDownloadsPath", "SearchTimeoutSeconds", "MaxCandidatesStored",
                    "AutoPickMinScore", "ManualReviewOnly", "RetryAttempts", "AcquisitionOrder", "FallbackToSlskdOnYtDlpFailure",
                    "FallbackToYtDlpOnSlskdFailure", "HigherQualityHandling", "RequireManualReviewOnTranscode", "KeepOriginalAfterTranscode"
                },
                values: new object[]
                {
                    1, false, "", "", "", 30, 50, 85, true, 2, 0, true, true, 0, true, false
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlskdConfig");

            migrationBuilder.DropIndex(
                name: "IX_DownloadQueue_Status_ExternalWorkPending",
                table: "DownloadQueue");

            migrationBuilder.DropColumn(
                name: "ExternalAcquisitionJson",
                table: "DownloadQueue");

            migrationBuilder.DropColumn(
                name: "ExternalWorkPending",
                table: "DownloadQueue");
        }
    }
}
