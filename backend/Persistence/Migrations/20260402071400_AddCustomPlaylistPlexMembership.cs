using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomPlaylistPlexMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomPlaylistMembershipVersion",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChannelCustomPlaylistMembershipApplied",
                columns: table => new
                {
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelCustomPlaylistMembershipApplied", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_ChannelCustomPlaylistMembershipApplied_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelCustomPlaylistVideoMemberships",
                columns: table => new
                {
                    ChannelCustomPlaylistId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelCustomPlaylistVideoMemberships", x => new { x.ChannelCustomPlaylistId, x.VideoId });
                    table.ForeignKey(
                        name: "FK_ChannelCustomPlaylistVideoMemberships_ChannelCustomPlaylists_ChannelCustomPlaylistId",
                        column: x => x.ChannelCustomPlaylistId,
                        principalTable: "ChannelCustomPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelCustomPlaylistVideoMemberships_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelCustomPlaylistVideoMemberships_VideoId",
                table: "ChannelCustomPlaylistVideoMemberships",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelCustomPlaylistMembershipApplied");

            migrationBuilder.DropTable(
                name: "ChannelCustomPlaylistVideoMemberships");

            migrationBuilder.DropColumn(
                name: "CustomPlaylistMembershipVersion",
                table: "Channels");
        }
    }
}
