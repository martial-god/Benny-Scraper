using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToNovelAddedCurrentChapterUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "current_chapter_url",
                table: "novel",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "chapter",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_chapter_url",
                table: "novel");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "chapter",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
