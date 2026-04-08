using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class RemoveImportListTables : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(name: "ImportExclusions");
			migrationBuilder.DropTable(name: "ImportListOptionsConfig");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "ImportListOptionsConfig",
				columns: table => new
				{
					Id = table.Column<int>(type: "INTEGER", nullable: false)
						.Annotation("Sqlite:Autoincrement", true),
					ListSyncLevel = table.Column<string>(type: "TEXT", nullable: false),
					ListSyncTag = table.Column<int>(type: "INTEGER", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ImportListOptionsConfig", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "ImportExclusions",
				columns: table => new
				{
					Id = table.Column<int>(type: "INTEGER", nullable: false)
						.Annotation("Sqlite:Autoincrement", true),
					CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
					Reason = table.Column<string>(type: "TEXT", nullable: true),
					TargetType = table.Column<string>(type: "TEXT", nullable: false),
					Title = table.Column<string>(type: "TEXT", nullable: false),
					YoutubeChannelId = table.Column<string>(type: "TEXT", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_ImportExclusions", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_ImportExclusions_YoutubeChannelId",
				table: "ImportExclusions",
				column: "YoutubeChannelId",
				unique: true);
		}
	}
}
