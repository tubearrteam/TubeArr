using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddPlaylistVideos_RemoveVideoPlaylistId : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "PlaylistVideos",
				columns: table => new
				{
					PlaylistId = table.Column<int>(type: "INTEGER", nullable: false),
					VideoId = table.Column<int>(type: "INTEGER", nullable: false),
					PlaylistItemId = table.Column<string>(type: "TEXT", nullable: true),
					Position = table.Column<int>(type: "INTEGER", nullable: true),
					AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_PlaylistVideos", x => new { x.PlaylistId, x.VideoId });
					table.ForeignKey(
						name: "FK_PlaylistVideos_Playlists_PlaylistId",
						column: x => x.PlaylistId,
						principalTable: "Playlists",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "FK_PlaylistVideos_Videos_VideoId",
						column: x => x.VideoId,
						principalTable: "Videos",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_PlaylistVideos_PlaylistId",
				table: "PlaylistVideos",
				column: "PlaylistId");

			migrationBuilder.CreateIndex(
				name: "IX_PlaylistVideos_VideoId",
				table: "PlaylistVideos",
				column: "VideoId");

			migrationBuilder.Sql(
				"""
				INSERT INTO PlaylistVideos (PlaylistId, VideoId, PlaylistItemId, Position, AddedAt)
				SELECT PlaylistId, Id, NULL, NULL, NULL
				FROM Videos
				WHERE PlaylistId IS NOT NULL
				""");

			migrationBuilder.DropIndex(
				name: "IX_Videos_PlaylistId",
				table: "Videos");

			migrationBuilder.DropColumn(
				name: "PlaylistId",
				table: "Videos");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "PlaylistId",
				table: "Videos",
				type: "INTEGER",
				nullable: true);

			migrationBuilder.Sql(
				"""
				UPDATE Videos
				SET PlaylistId = (
					SELECT MIN(pv.PlaylistId)
					FROM PlaylistVideos pv
					WHERE pv.VideoId = Videos.Id)
				WHERE EXISTS (
					SELECT 1 FROM PlaylistVideos pv2 WHERE pv2.VideoId = Videos.Id)
				""");

			migrationBuilder.CreateIndex(
				name: "IX_Videos_PlaylistId",
				table: "Videos",
				column: "PlaylistId");

			migrationBuilder.DropTable(
				name: "PlaylistVideos");
		}
	}
}
