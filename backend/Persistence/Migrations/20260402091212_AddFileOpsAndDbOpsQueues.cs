using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileOpsAndDbOpsQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DbOpsHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: true),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbOpsHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbOpsQueue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbOpsQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileOpsHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: true),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileOpsHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileOpsQueue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandQueueJobId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AcquisitionMethodsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileOpsQueue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsHistory_ChannelId",
                table: "DbOpsHistory",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsHistory_CommandId",
                table: "DbOpsHistory",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsHistory_CommandQueueJobId",
                table: "DbOpsHistory",
                column: "CommandQueueJobId");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsHistory_EndedAtUtc",
                table: "DbOpsHistory",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsQueue_ChannelId",
                table: "DbOpsQueue",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsQueue_CommandId",
                table: "DbOpsQueue",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsQueue_CommandQueueJobId",
                table: "DbOpsQueue",
                column: "CommandQueueJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DbOpsQueue_Status",
                table: "DbOpsQueue",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsHistory_ChannelId",
                table: "FileOpsHistory",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsHistory_CommandId",
                table: "FileOpsHistory",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsHistory_CommandQueueJobId",
                table: "FileOpsHistory",
                column: "CommandQueueJobId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsHistory_EndedAtUtc",
                table: "FileOpsHistory",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsQueue_ChannelId",
                table: "FileOpsQueue",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsQueue_CommandId",
                table: "FileOpsQueue",
                column: "CommandId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsQueue_CommandQueueJobId",
                table: "FileOpsQueue",
                column: "CommandQueueJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileOpsQueue_Status",
                table: "FileOpsQueue",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbOpsHistory");

            migrationBuilder.DropTable(
                name: "DbOpsQueue");

            migrationBuilder.DropTable(
                name: "FileOpsHistory");

            migrationBuilder.DropTable(
                name: "FileOpsQueue");
        }
    }
}
