using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TubeArr.Backend.Persistence.Migrations;

/// <inheritdoc />
public partial class AddChannelTagsJunction : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "ChannelTags",
			columns: table => new
			{
				ChannelId = table.Column<int>(type: "INTEGER", nullable: false),
				TagId = table.Column<int>(type: "INTEGER", nullable: false)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ChannelTags", x => new { x.ChannelId, x.TagId });
				table.ForeignKey(
					name: "FK_ChannelTags_Channels_ChannelId",
					column: x => x.ChannelId,
					principalTable: "Channels",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
				table.ForeignKey(
					name: "FK_ChannelTags_Tags_TagId",
					column: x => x.TagId,
					principalTable: "Tags",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateIndex(
			name: "IX_ChannelTags_TagId",
			table: "ChannelTags",
			column: "TagId");

		migrationBuilder.Sql("""
			INSERT OR IGNORE INTO "ChannelTags" ("ChannelId", "TagId")
			WITH RECURSIVE split("ChannelId", piece, rest) AS (
				SELECT "Id",
					trim(substr("Tags" || ',', 1, instr("Tags" || ',', ',') - 1)),
					substr("Tags" || ',', instr("Tags" || ',', ',') + 1)
				FROM "Channels" WHERE "Tags" IS NOT NULL AND length(trim("Tags")) > 0
				UNION ALL
				SELECT "ChannelId",
					trim(substr(rest, 1, instr(rest, ',') - 1)),
					substr(rest, instr(rest, ',') + 1)
				FROM split WHERE length(trim(rest)) > 0
			)
			SELECT s."ChannelId", CAST(s.piece AS INTEGER)
			FROM split s
			INNER JOIN "Tags" t ON t."Id" = CAST(s.piece AS INTEGER)
			WHERE length(s.piece) > 0
				AND s.piece NOT GLOB '*[^0-9]*';
			""");

		migrationBuilder.DropColumn(
			name: "Tags",
			table: "Channels");
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder) =>
		throw new System.NotSupportedException("AddChannelTagsJunction cannot be reverted automatically.");
}
