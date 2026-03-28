using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public class AddYtDlpDownloadQueueParallelWorkers : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "DownloadQueueParallelWorkers",
				table: "YtDlpConfig",
				type: "INTEGER",
				nullable: false,
				defaultValue: 1);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "DownloadQueueParallelWorkers",
				table: "YtDlpConfig");
		}
	}
}
