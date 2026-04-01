using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddPlaylistMultiMatchStrategyOrder : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "PlaylistMultiMatchStrategyOrder",
				table: "Channels",
				type: "TEXT",
				nullable: false,
				defaultValue: "0123");

			migrationBuilder.Sql("""
				UPDATE "Channels" SET "PlaylistMultiMatchStrategyOrder" = CASE "PlaylistMultiMatchStrategy"
					WHEN 0 THEN '0123'
					WHEN 1 THEN '1023'
					WHEN 2 THEN '2013'
					WHEN 3 THEN '3012'
					ELSE '0123'
				END
				""");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "PlaylistMultiMatchStrategyOrder",
				table: "Channels");
		}
	}
}
