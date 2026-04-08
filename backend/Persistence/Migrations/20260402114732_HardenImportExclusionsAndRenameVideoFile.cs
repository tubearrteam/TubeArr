using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenImportExclusionsAndRenameVideoFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ImportExclusions"
                WHERE "YoutubeChannelId" IS NULL OR trim("YoutubeChannelId") = '';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Channels_ChannelId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Playlists_PlaylistId",
                table: "VideoFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFiles_Videos_VideoId",
                table: "VideoFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VideoFiles",
                table: "VideoFiles");

            migrationBuilder.RenameTable(
                name: "VideoFiles",
                newName: "VideoFile");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFiles_VideoId",
                table: "VideoFile",
                newName: "IX_VideoFile_VideoId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFiles_PlaylistId",
                table: "VideoFile",
                newName: "IX_VideoFile_PlaylistId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFiles_ChannelId",
                table: "VideoFile",
                newName: "IX_VideoFile_ChannelId");

            migrationBuilder.AlterColumn<string>(
                name: "YoutubeChannelId",
                table: "ImportExclusions",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "ImportExclusions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ImportExclusions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "ImportExclusions",
                type: "TEXT",
                nullable: false,
                defaultValue: "channel");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VideoFile",
                table: "VideoFile",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFile_Channels_ChannelId",
                table: "VideoFile",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFile_Playlists_PlaylistId",
                table: "VideoFile",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoFile_Videos_VideoId",
                table: "VideoFile",
                column: "VideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                UPDATE "ImportExclusions"
                SET "CreatedAtUtc" = '2020-01-01T00:00:00.0000000+00:00'
                WHERE "CreatedAtUtc" = '0001-01-01T00:00:00.0000000+00:00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoFile_Channels_ChannelId",
                table: "VideoFile");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFile_Playlists_PlaylistId",
                table: "VideoFile");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoFile_Videos_VideoId",
                table: "VideoFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VideoFile",
                table: "VideoFile");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ImportExclusions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ImportExclusions");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "ImportExclusions");

            migrationBuilder.RenameTable(
                name: "VideoFile",
                newName: "VideoFiles");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFile_VideoId",
                table: "VideoFiles",
                newName: "IX_VideoFiles_VideoId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFile_PlaylistId",
                table: "VideoFiles",
                newName: "IX_VideoFiles_PlaylistId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoFile_ChannelId",
                table: "VideoFiles",
                newName: "IX_VideoFiles_ChannelId");

            migrationBuilder.AlterColumn<string>(
                name: "YoutubeChannelId",
                table: "ImportExclusions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VideoFiles",
                table: "VideoFiles",
                column: "Id");

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
    }
}
