using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClearQualityProfileHeadlessBackendOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			const int id = 1000001;
			migrationBuilder.Sql(
				$"UPDATE QualityProfiles SET DownloadBackendOverride = NULL WHERE Id = {id};");
			migrationBuilder.Sql(
				$"UPDATE QualityProfiles SET Name = 'Get best' WHERE Id = {id} AND Name = 'Get best (headless)';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			const int id = 1000001;
			migrationBuilder.Sql(
				$"UPDATE QualityProfiles SET DownloadBackendOverride = 'headless-browser' WHERE Id = {id};");
			migrationBuilder.Sql(
				$"UPDATE QualityProfiles SET Name = 'Get best (headless)' WHERE Id = {id} AND Name = 'Get best';");
        }
    }
}
