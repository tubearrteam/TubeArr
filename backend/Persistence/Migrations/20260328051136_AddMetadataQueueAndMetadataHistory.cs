using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddMetadataQueueAndMetadataHistory : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "MetadataHistory",
				columns: table => new
				{
					Id = table.Column<long>(type: "INTEGER", nullable: false)
						.Annotation("Sqlite:Autoincrement", true),
					AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false),
					ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
					CommandId = table.Column<int>(type: "INTEGER", nullable: true),
					CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: true),
					EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
					JobType = table.Column<string>(type: "TEXT", nullable: false),
					Message = table.Column<string>(type: "TEXT", nullable: true),
					Name = table.Column<string>(type: "TEXT", nullable: false),
					PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
					QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
					ResultStatus = table.Column<string>(type: "TEXT", nullable: false),
					StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_MetadataHistory", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "MetadataQueue",
				columns: table => new
				{
					Id = table.Column<long>(type: "INTEGER", nullable: false)
						.Annotation("Sqlite:Autoincrement", true),
					AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false),
					ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
					CommandId = table.Column<int>(type: "INTEGER", nullable: true),
					CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: false),
					EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
					JobType = table.Column<string>(type: "TEXT", nullable: false),
					LastError = table.Column<string>(type: "TEXT", nullable: true),
					Name = table.Column<string>(type: "TEXT", nullable: false),
					PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
					QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
					StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
					Status = table.Column<string>(type: "TEXT", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_MetadataQueue", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_MetadataHistory_ChannelId",
				table: "MetadataHistory",
				column: "ChannelId");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataHistory_CommandId",
				table: "MetadataHistory",
				column: "CommandId");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataHistory_CommandQueueJobId",
				table: "MetadataHistory",
				column: "CommandQueueJobId");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataHistory_EndedAtUtc",
				table: "MetadataHistory",
				column: "EndedAtUtc");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataQueue_ChannelId",
				table: "MetadataQueue",
				column: "ChannelId");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataQueue_CommandId",
				table: "MetadataQueue",
				column: "CommandId");

			migrationBuilder.CreateIndex(
				name: "IX_MetadataQueue_CommandQueueJobId",
				table: "MetadataQueue",
				column: "CommandQueueJobId",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "IX_MetadataQueue_Status",
				table: "MetadataQueue",
				column: "Status");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "MetadataQueue");

			migrationBuilder.DropTable(
				name: "MetadataHistory");
		}
	}
}
