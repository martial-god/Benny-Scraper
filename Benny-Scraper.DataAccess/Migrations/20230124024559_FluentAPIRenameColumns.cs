using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class FluentAPIRenameColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Url",
                table: "chapter",
                newName: "url");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "chapter",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Number",
                table: "chapter",
                newName: "number");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "chapter",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "chapter",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                table: "chapter",
                newName: "date_created");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "url",
                table: "chapter",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "chapter",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "number",
                table: "chapter",
                newName: "Number");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "chapter",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "chapter",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "date_created",
                table: "chapter",
                newName: "DateCreated");
        }
    }
}
