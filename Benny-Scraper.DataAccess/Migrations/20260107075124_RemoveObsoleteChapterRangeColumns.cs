using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteChapterRangeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chapter_range_begin",
                table: "novel");

            migrationBuilder.DropColumn(
                name: "chapter_range_end",
                table: "novel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
