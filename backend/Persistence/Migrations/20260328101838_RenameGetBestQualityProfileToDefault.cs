using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameGetBestQualityProfileToDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
	        // Seeded id from SeedHeadlessGetBestQualityProfile (matches EditQualityProfileModalContentConnector "Maximum Compatibility" + MP4 + SDR + 360p–4320p).
	        migrationBuilder.Sql("""
				UPDATE QualityProfiles SET
					Name = 'Default',
					MaxHeight = 4320,
					MinHeight = 360,
					MinFps = 24,
					MaxFps = 60,
					AllowHdr = 0,
					AllowSdr = 1,
					AllowedVideoCodecsJson = '["AVC"]',
					PreferredVideoCodecsJson = '["AVC"]',
					AllowedAudioCodecsJson = '["MP4A"]',
					PreferredAudioCodecsJson = '["MP4A"]',
					AllowedContainersJson = '["mp4"]',
					PreferredContainersJson = '["mp4"]',
					PreferSeparateStreams = 1,
					AllowMuxedFallback = 1,
					FallbackMode = 1,
					DegradeOrderJson = NULL,
					DegradeHeightStepsJson = NULL,
					FailIfBelowMinHeight = 1,
					RetryForBetterFormats = 0,
					RetryWindowMinutes = NULL,
					SelectionArgs = NULL,
					MuxArgs = NULL,
					AudioArgs = NULL,
					TimeArgs = NULL,
					SubtitleArgs = NULL,
					ThumbnailArgs = NULL,
					MetadataArgs = NULL,
					CleanupArgs = '--fixup warn --prefer-ffmpeg',
					SponsorblockArgs = NULL
				WHERE Id = 1000001
				   OR Name = 'Get best'
				   OR Name = 'Get best (headless)';
				""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
	        migrationBuilder.Sql("""
				UPDATE QualityProfiles SET
					Name = 'Get best',
					MaxHeight = NULL,
					MinHeight = 0,
					MinFps = NULL,
					MaxFps = NULL,
					AllowHdr = 1,
					AllowSdr = 1,
					AllowedVideoCodecsJson = NULL,
					PreferredVideoCodecsJson = NULL,
					AllowedAudioCodecsJson = NULL,
					PreferredAudioCodecsJson = NULL,
					AllowedContainersJson = NULL,
					PreferredContainersJson = NULL,
					PreferSeparateStreams = 1,
					AllowMuxedFallback = 1,
					FallbackMode = 3,
					DegradeOrderJson = NULL,
					DegradeHeightStepsJson = NULL,
					FailIfBelowMinHeight = 0,
					RetryForBetterFormats = 0,
					RetryWindowMinutes = NULL,
					SelectionArgs = NULL,
					MuxArgs = NULL,
					AudioArgs = NULL,
					TimeArgs = NULL,
					SubtitleArgs = NULL,
					ThumbnailArgs = NULL,
					MetadataArgs = NULL,
					CleanupArgs = NULL,
					SponsorblockArgs = NULL
				WHERE Id = 1000001;
				""");
        }
    }
}
