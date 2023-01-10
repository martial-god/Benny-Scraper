using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NovelsList_Novels_NovelId",
                table: "NovelsList");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NovelsList",
                table: "NovelsList");

            migrationBuilder.RenameTable(
                name: "NovelsList",
                newName: "NovelLists");

            migrationBuilder.RenameIndex(
                name: "IX_NovelsList_NovelId",
                table: "NovelLists",
                newName: "IX_NovelLists_NovelId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NovelLists",
                table: "NovelLists",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NovelLists_Novels_NovelId",
                table: "NovelLists",
                column: "NovelId",
                principalTable: "Novels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NovelLists_Novels_NovelId",
                table: "NovelLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NovelLists",
                table: "NovelLists");

            migrationBuilder.RenameTable(
                name: "NovelLists",
                newName: "NovelsList");

            migrationBuilder.RenameIndex(
                name: "IX_NovelLists_NovelId",
                table: "NovelsList",
                newName: "IX_NovelsList_NovelId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NovelsList",
                table: "NovelsList",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NovelsList_Novels_NovelId",
                table: "NovelsList",
                column: "NovelId",
                principalTable: "Novels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
