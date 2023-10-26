using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameNovelColums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SavedFileIsSplit",
                table: "novel",
                newName: "saved_file_is_split");

            migrationBuilder.RenameColumn(
                name: "FileType",
                table: "novel",
                newName: "file_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "saved_file_is_split",
                table: "novel",
                newName: "SavedFileIsSplit");

            migrationBuilder.RenameColumn(
                name: "file_type",
                table: "novel",
                newName: "FileType");
        }
    }
}
