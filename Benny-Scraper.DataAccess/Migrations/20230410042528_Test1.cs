using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Test1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "first_chapter",
                table: "novel",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(144)",
                oldMaxLength: 144);

            migrationBuilder.AlterColumn<string>(
                name: "current_chapter",
                table: "novel",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(144)",
                oldMaxLength: 144);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "first_chapter",
                table: "novel",
                type: "nvarchar(144)",
                maxLength: 144,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "current_chapter",
                table: "novel",
                type: "nvarchar(144)",
                maxLength: 144,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);
        }
    }
}
