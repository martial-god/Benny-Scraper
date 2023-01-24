using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddedFluentAPI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_Novels_NovelId",
                table: "Chapters");

            migrationBuilder.DropForeignKey(
                name: "FK_NovelLists_Novels_NovelId",
                table: "NovelLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Novels",
                table: "Novels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chapters",
                table: "Chapters");

            migrationBuilder.RenameTable(
                name: "Novels",
                newName: "novel");

            migrationBuilder.RenameTable(
                name: "Chapters",
                newName: "chapter");

            migrationBuilder.RenameIndex(
                name: "IX_Chapters_NovelId",
                table: "chapter",
                newName: "IX_chapter_NovelId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_novel",
                table: "novel",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_chapter",
                table: "chapter",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter",
                column: "NovelId",
                principalTable: "novel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NovelLists_novel_NovelId",
                table: "NovelLists",
                column: "NovelId",
                principalTable: "novel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter");

            migrationBuilder.DropForeignKey(
                name: "FK_NovelLists_novel_NovelId",
                table: "NovelLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_novel",
                table: "novel");

            migrationBuilder.DropPrimaryKey(
                name: "PK_chapter",
                table: "chapter");

            migrationBuilder.RenameTable(
                name: "novel",
                newName: "Novels");

            migrationBuilder.RenameTable(
                name: "chapter",
                newName: "Chapters");

            migrationBuilder.RenameIndex(
                name: "IX_chapter_NovelId",
                table: "Chapters",
                newName: "IX_Chapters_NovelId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Novels",
                table: "Novels",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chapters",
                table: "Chapters",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_Novels_NovelId",
                table: "Chapters",
                column: "NovelId",
                principalTable: "Novels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NovelLists_Novels_NovelId",
                table: "NovelLists",
                column: "NovelId",
                principalTable: "Novels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
