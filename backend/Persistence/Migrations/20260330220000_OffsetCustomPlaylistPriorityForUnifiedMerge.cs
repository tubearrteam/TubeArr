using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <summary>
	/// Custom playlists used the same numeric priority range as native playlists. Unified merge ordering
	/// requires custom rows to sort after native until the user re-saves order from the UI (drag list).
	/// </summary>
	public partial class OffsetCustomPlaylistPriorityForUnifiedMerge : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
				UPDATE "ChannelCustomPlaylists" SET "Priority" = "Priority" + 100000
				WHERE "Priority" < 100000
				""");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
				UPDATE "ChannelCustomPlaylists" SET "Priority" = "Priority" - 100000
				WHERE "Priority" >= 100000
				""");
		}
	}
}
