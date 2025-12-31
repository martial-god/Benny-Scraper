using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChapterRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "chapter_range_begin",
                table: "novel",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "chapter_range_end",
                table: "novel",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_partial_download",
                table: "novel",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chapter_range_begin",
                table: "novel");

            migrationBuilder.DropColumn(
                name: "chapter_range_end",
                table: "novel");

            migrationBuilder.DropColumn(
                name: "is_partial_download",
                table: "novel");
        }
    }
}
