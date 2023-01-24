using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFluentAPIColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter");

            migrationBuilder.RenameColumn(
                name: "NovelId",
                table: "chapter",
                newName: "novel_id");

            migrationBuilder.RenameIndex(
                name: "IX_chapter_NovelId",
                table: "chapter",
                newName: "IX_chapter_novel_id");

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_novel_novel_id",
                table: "chapter",
                column: "novel_id",
                principalTable: "novel",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_novel_novel_id",
                table: "chapter");

            migrationBuilder.RenameColumn(
                name: "novel_id",
                table: "chapter",
                newName: "NovelId");

            migrationBuilder.RenameIndex(
                name: "IX_chapter_novel_id",
                table: "chapter",
                newName: "IX_chapter_NovelId");

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter",
                column: "NovelId",
                principalTable: "novel",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
