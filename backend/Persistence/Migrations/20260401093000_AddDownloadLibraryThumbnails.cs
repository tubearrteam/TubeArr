using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddDownloadLibraryThumbnails : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "DownloadLibraryThumbnails",
				table: "MediaManagementConfig",
				type: "INTEGER",
				nullable: false,
				defaultValue: false);

			migrationBuilder.Sql(@"
UPDATE MediaManagementConfig
SET DownloadLibraryThumbnails = 1
WHERE EXISTS (
	SELECT 1 FROM PlexProviderConfig p
	WHERE p.Id = 1 AND p.Enabled = 1
);
");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "DownloadLibraryThumbnails",
				table: "MediaManagementConfig");
		}
	}
}
