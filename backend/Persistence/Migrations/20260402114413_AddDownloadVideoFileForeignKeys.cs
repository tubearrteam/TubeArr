using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadVideoFileForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "DownloadHistory"
                WHERE "VideoId" NOT IN (SELECT "Id" FROM "Videos")
                   OR "ChannelId" NOT IN (SELECT "Id" FROM "Channels")
                   OR ("PlaylistId" IS NOT NULL AND "PlaylistId" NOT IN (SELECT "Id" FROM "Playlists"));
                DELETE FROM "DownloadQueue"
                WHERE "VideoId" NOT IN (SELECT "Id" FROM "Videos")
                   OR "ChannelId" NOT IN (SELECT "Id" FROM "Channels");
                DELETE FROM "VideoFiles"
                WHERE "VideoId" NOT IN (SELECT "Id" FROM "Videos")
                   OR "ChannelId" NOT IN (SELECT "Id" FROM "Channels")
                   OR ("PlaylistId" IS NOT NULL AND "PlaylistId" NOT IN (SELECT "Id" FROM "Playlists"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadQueue_VideoId",
                table: "DownloadQueue",
                column: "VideoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DownloadHistory_Channels_ChannelId",
                table: "DownloadHistory",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DownloadHistory_Playlists_PlaylistId",
                table: "DownloadHistory",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DownloadHistory_Videos_VideoId",
                table: "DownloadHistory",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DownloadQueue_Channels_ChannelId",
                table: "DownloadQueue",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DownloadQueue_Videos_VideoId",
                table: "DownloadQueue",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Channels_ChannelId",
                table: "VideoFiles",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Playlists_PlaylistId",
                table: "VideoFiles",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFiles_Videos_VideoId",
                table: "VideoFiles",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DownloadHistory_Channels_ChannelId",
                table: "DownloadHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_DownloadHistory_Playlists_PlaylistId",
                table: "DownloadHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_DownloadHistory_Videos_VideoId",
                table: "DownloadHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_DownloadQueue_Channels_ChannelId",
                table: "DownloadQueue");

            migrationBuilder.DropForeignKey(
                name: "FK_DownloadQueue_Videos_VideoId",
                table: "DownloadQueue");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Channels_ChannelId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Playlists_PlaylistId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Videos_VideoId",
                table: "VideoFiles");

            migrationBuilder.DropIndex(
                name: "IX_DownloadQueue_VideoId",
                table: "DownloadQueue");
        }
    }
}
