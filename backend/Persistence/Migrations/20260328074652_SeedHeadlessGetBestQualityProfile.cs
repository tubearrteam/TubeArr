using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedHeadlessGetBestQualityProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "QualityProfiles",
                columns: new[]
                {
                    "Id", "Name", "Enabled", "MaxHeight", "MinHeight", "MinFps", "MaxFps",
                    "AllowHdr", "AllowSdr",
                    "AllowedVideoCodecsJson", "PreferredVideoCodecsJson",
                    "AllowedAudioCodecsJson", "PreferredAudioCodecsJson",
                    "AllowedContainersJson", "PreferredContainersJson",
                    "PreferSeparateStreams", "AllowMuxedFallback",
                    "FallbackMode", "DegradeOrderJson", "DegradeHeightStepsJson",
                    "FailIfBelowMinHeight", "RetryForBetterFormats", "RetryWindowMinutes",
                    "SelectionArgs", "MuxArgs", "AudioArgs", "TimeArgs", "SubtitleArgs", "ThumbnailArgs", "MetadataArgs", "CleanupArgs", "SponsorblockArgs",
                    "DownloadBackendOverride"
                },
                values: new object[]
                {
                    1000001, "Get best", true, null, 0, null, null,
                    true, true,
                    null, null, null, null, null, null,
                    true, true,
                    3, null, null,
                    false, false, null,
                    null, null, null, null, null, null, null, null, null,
                    null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "QualityProfiles",
                keyColumn: "Id",
                keyValue: 1000001);
        }
    }
}
