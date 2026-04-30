using Microsoft.EntityFrameworkCore.Migrations;

namespace TubeArr.Backend.Data.Migrations;

public partial class AddDownloadQueueByteProgress : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<long>(
			name: "DownloadedBytes",
			table: "DownloadQueue",
			type: "INTEGER",
			nullable: true);

		migrationBuilder.AddColumn<long>(
			name: "TotalBytes",
			table: "DownloadQueue",
			type: "INTEGER",
			nullable: true);

		migrationBuilder.AddColumn<long>(
			name: "SpeedBytesPerSecond",
			table: "DownloadQueue",
			type: "INTEGER",
			nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "DownloadedBytes", table: "DownloadQueue");
		migrationBuilder.DropColumn(name: "TotalBytes", table: "DownloadQueue");
		migrationBuilder.DropColumn(name: "SpeedBytesPerSecond", table: "DownloadQueue");
	}
}

