using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddChannelCustomPlaylists : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "ChannelCustomPlaylists",
				columns: table => new
				{
					Id = table.Column<int>(type: "INTEGER", nullable: false)
						.Annotation("Sqlite:Autoincrement", true),
					ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
					Name = table.Column<string>(type: "TEXT", nullable: false),
					Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
					Priority = table.Column<int>(type: "INTEGER", nullable: false),
					MatchType = table.Column<int>(type: "INTEGER", nullable: false),
					RulesJson = table.Column<string>(type: "TEXT", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ChannelCustomPlaylists", x => x.Id);
					table.ForeignKey(
						name: "FK_ChannelCustomPlaylists_Channels_ChannelId",
						column: x => x.ChannelId,
						principalTable: "Channels",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ChannelCustomPlaylists_ChannelId",
				table: "ChannelCustomPlaylists",
				column: "ChannelId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "ChannelCustomPlaylists");
		}
	}
}
