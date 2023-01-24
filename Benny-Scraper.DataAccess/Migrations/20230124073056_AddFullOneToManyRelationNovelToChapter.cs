using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFullOneToManyRelationNovelToChapter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter");

            migrationBuilder.AlterColumn<Guid>(
                name: "NovelId",
                table: "chapter",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter",
                column: "NovelId",
                principalTable: "novel",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter");

            migrationBuilder.AlterColumn<Guid>(
                name: "NovelId",
                table: "chapter",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_novel_NovelId",
                table: "chapter",
                column: "NovelId",
                principalTable: "novel",
                principalColumn: "id");
        }
    }
}
