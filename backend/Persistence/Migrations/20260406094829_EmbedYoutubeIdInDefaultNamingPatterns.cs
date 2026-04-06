using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmbedYoutubeIdInDefaultNamingPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			const string withId = "{Upload Date} - {Video Title} [{Video Id}]";
			const string legacy = "{Upload Date} - {Video Title}";
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET DailyVideoFormat = '{withId}' WHERE DailyVideoFormat = '{legacy}';");
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET EpisodicVideoFormat = '{withId}' WHERE EpisodicVideoFormat = '{legacy}';");
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET StreamingVideoFormat = '{withId}' WHERE StreamingVideoFormat = '{legacy}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			const string withId = "{Upload Date} - {Video Title} [{Video Id}]";
			const string legacy = "{Upload Date} - {Video Title}";
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET DailyVideoFormat = '{legacy}' WHERE DailyVideoFormat = '{withId}';");
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET EpisodicVideoFormat = '{legacy}' WHERE EpisodicVideoFormat = '{withId}';");
			migrationBuilder.Sql(
				$"UPDATE NamingConfig SET StreamingVideoFormat = '{legacy}' WHERE StreamingVideoFormat = '{withId}';");
        }
    }
}
