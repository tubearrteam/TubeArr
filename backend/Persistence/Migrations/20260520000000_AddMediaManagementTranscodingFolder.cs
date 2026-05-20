using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TubeArr.Backend.Data;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	[DbContext(typeof(TubeArrDbContext))]
	[Migration("20260520000000_AddMediaManagementTranscodingFolder")]
	public partial class AddMediaManagementTranscodingFolder : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "TranscodingFolder",
				table: "MediaManagementConfig",
				type: "TEXT",
				nullable: false,
				defaultValue: "");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "TranscodingFolder",
				table: "MediaManagementConfig");
		}
	}
}
