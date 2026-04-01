using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TubeArr.Backend.Data;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(TubeArrDbContext))]
	[Migration("20260330190000_AddPlexProviderAndStableNumbering")]
	public partial class AddPlexProviderAndStableNumbering : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "SeasonIndex",
				table: "Playlists",
				type: "INTEGER",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "SeasonIndexLocked",
				table: "Playlists",
				type: "INTEGER",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<int>(
				name: "PlexPrimaryPlaylistId",
				table: "Videos",
				type: "INTEGER",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "PlexSeasonIndex",
				table: "Videos",
				type: "INTEGER",
				nullable: true);

			migrationBuilder.AddColumn<int>(
				name: "PlexEpisodeIndex",
				table: "Videos",
				type: "INTEGER",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "PlexIndexLocked",
				table: "Videos",
				type: "INTEGER",
				nullable: false,
				defaultValue: false);

			migrationBuilder.CreateTable(
				name: "PlexProviderConfig",
				columns: table => new
				{
					Id = table.Column<int>(type: "INTEGER", nullable: false),
					Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
					BasePath = table.Column<string>(type: "TEXT", nullable: false),
					ExposeArtworkUrls = table.Column<bool>(type: "INTEGER", nullable: false),
					IncludeChildrenByDefault = table.Column<bool>(type: "INTEGER", nullable: false),
					VerboseRequestLogging = table.Column<bool>(type: "INTEGER", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_PlexProviderConfig", x => x.Id);
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "PlexProviderConfig");

			migrationBuilder.DropColumn(
				name: "SeasonIndex",
				table: "Playlists");

			migrationBuilder.DropColumn(
				name: "SeasonIndexLocked",
				table: "Playlists");

			migrationBuilder.DropColumn(
				name: "PlexPrimaryPlaylistId",
				table: "Videos");

			migrationBuilder.DropColumn(
				name: "PlexSeasonIndex",
				table: "Videos");

			migrationBuilder.DropColumn(
				name: "PlexEpisodeIndex",
				table: "Videos");

			migrationBuilder.DropColumn(
				name: "PlexIndexLocked",
				table: "Videos");
		}
	}
}

