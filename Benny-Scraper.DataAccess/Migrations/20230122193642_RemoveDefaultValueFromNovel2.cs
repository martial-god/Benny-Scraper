using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultValueFromNovel2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNovelCompleted",
                table: "Novels",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNovelCompleted",
                table: "Novels");
        }
    }
}
