using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TubeArr.Backend.Data;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    [DbContext(typeof(TubeArrDbContext))]
    [Migration("20260324120000_AddLivestreamFlags")]
    public partial class AddLivestreamFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FilterOutLivestreams",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLivestream",
                table: "Videos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterOutLivestreams",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "IsLivestream",
                table: "Videos");
        }
    }
}
