using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations;

/// <inheritdoc />
public partial class RemoveNovelList : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NovelLists");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NovelLists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                NovelId = table.Column<Guid>(type: "TEXT", nullable: false),
                Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NovelLists", x => x.Id);
                table.ForeignKey(
                    name: "FK_NovelLists_novel_NovelId",
                    column: x => x.NovelId,
                    principalTable: "novel",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NovelLists_NovelId",
            table: "NovelLists",
            column: "NovelId");
    }
}