using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddedChapterContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChapterName",
                table: "Novels");

            migrationBuilder.RenameColumn(
                name: "ChapterNumber",
                table: "Novels",
                newName: "TotalChapters");

            migrationBuilder.AlterColumn<string>(
                name: "SiteName",
                table: "Novels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentChapter",
                table: "Novels",
                type: "nvarchar(144)",
                maxLength: 144,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstChapter",
                table: "Novels",
                type: "nvarchar(144)",
                maxLength: 144,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NovelStatus",
                table: "Novels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Chapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Number = table.Column<int>(type: "int", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NovelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chapters_Novels_NovelId",
                        column: x => x.NovelId,
                        principalTable: "Novels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_NovelId",
                table: "Chapters",
                column: "NovelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chapters");

            migrationBuilder.DropColumn(
                name: "CurrentChapter",
                table: "Novels");

            migrationBuilder.DropColumn(
                name: "FirstChapter",
                table: "Novels");

            migrationBuilder.DropColumn(
                name: "NovelStatus",
                table: "Novels");

            migrationBuilder.RenameColumn(
                name: "TotalChapters",
                table: "Novels",
                newName: "ChapterNumber");

            migrationBuilder.AlterColumn<string>(
                name: "SiteName",
                table: "Novels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "ChapterName",
                table: "Novels",
                type: "nvarchar(144)",
                maxLength: 144,
                nullable: true);
        }
    }
}
