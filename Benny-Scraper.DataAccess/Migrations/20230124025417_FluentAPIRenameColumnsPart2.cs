using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class FluentAPIRenameColumnsPart2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Url",
                table: "novel",
                newName: "url");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "novel",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "NovelStatus",
                table: "novel",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Genre",
                table: "novel",
                newName: "genre");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "novel",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Author",
                table: "novel",
                newName: "author");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "novel",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TotalChapters",
                table: "novel",
                newName: "total_chapters");

            migrationBuilder.RenameColumn(
                name: "SiteName",
                table: "novel",
                newName: "site_name");

            migrationBuilder.RenameColumn(
                name: "SaveLocation",
                table: "novel",
                newName: "save_location");

            migrationBuilder.RenameColumn(
                name: "IsNovelCompleted",
                table: "novel",
                newName: "last_chapter");

            migrationBuilder.RenameColumn(
                name: "FirstChapter",
                table: "novel",
                newName: "first_chapter");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "novel",
                newName: "date_created");

            migrationBuilder.RenameColumn(
                name: "CurrentChapter",
                table: "novel",
                newName: "current_chapter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "url",
                table: "novel",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "novel",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "novel",
                newName: "NovelStatus");

            migrationBuilder.RenameColumn(
                name: "genre",
                table: "novel",
                newName: "Genre");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "novel",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "author",
                table: "novel",
                newName: "Author");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "novel",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "total_chapters",
                table: "novel",
                newName: "TotalChapters");

            migrationBuilder.RenameColumn(
                name: "site_name",
                table: "novel",
                newName: "SiteName");

            migrationBuilder.RenameColumn(
                name: "save_location",
                table: "novel",
                newName: "SaveLocation");

            migrationBuilder.RenameColumn(
                name: "last_chapter",
                table: "novel",
                newName: "IsNovelCompleted");

            migrationBuilder.RenameColumn(
                name: "first_chapter",
                table: "novel",
                newName: "FirstChapter");

            migrationBuilder.RenameColumn(
                name: "date_created",
                table: "novel",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "current_chapter",
                table: "novel",
                newName: "CurrentChapter");
        }
    }
}
