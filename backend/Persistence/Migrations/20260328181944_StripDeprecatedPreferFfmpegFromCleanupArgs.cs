using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StripDeprecatedPreferFfmpegFromCleanupArgs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			// yt-dlp deprecated --prefer-ffmpeg (no longer has effect); strip from saved profiles.
			migrationBuilder.Sql(
				"""
				UPDATE QualityProfiles
				SET CleanupArgs = NULLIF(TRIM(
					REPLACE(REPLACE(REPLACE(
						COALESCE(CleanupArgs, ''),
						' --prefer-ffmpeg', ''),
						'--prefer-ffmpeg ', ''),
						'--prefer-ffmpeg', '')
				), '')
				WHERE CleanupArgs IS NOT NULL AND CleanupArgs LIKE '%prefer-ffmpeg%';
				""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			// Data cleanup is not reversed.
        }
    }
}
