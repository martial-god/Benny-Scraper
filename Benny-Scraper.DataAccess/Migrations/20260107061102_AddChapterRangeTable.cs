using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations;

/// <inheritdoc />
public partial class AddChapterRangeTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_partial_download",
            table: "novel");

        migrationBuilder.CreateTable(
            name: "chapter_range",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                novel_id = table.Column<Guid>(type: "TEXT", nullable: false),
                begin = table.Column<int>(type: "INTEGER", nullable: false),
                end = table.Column<int>(type: "INTEGER", nullable: false),
                date_created = table.Column<DateTime>(type: "TEXT", nullable: false),
                volume_name = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_chapter_range", x => x.id);
                table.ForeignKey(
                    name: "FK_chapter_range_novel_novel_id",
                    column: x => x.novel_id,
                    principalTable: "novel",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_chapter_range_novel_id",
            table: "chapter_range",
            column: "novel_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "chapter_range");

        migrationBuilder.AddColumn<bool>(
            name: "is_partial_download",
            table: "novel",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }
}