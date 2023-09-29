using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddedConfigurationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuration",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    auto_update = table.Column<bool>(type: "INTEGER", nullable: false),
                    concurrency_limit = table.Column<int>(type: "INTEGER", nullable: false),
                    save_location = table.Column<string>(type: "TEXT", nullable: true),
                    novel_save_location = table.Column<string>(type: "TEXT", nullable: true),
                    manga_save_location = table.Column<string>(type: "TEXT", nullable: true),
                    log_location = table.Column<string>(type: "TEXT", nullable: true),
                    database_locatoin = table.Column<string>(type: "TEXT", nullable: true),
                    database_file_name = table.Column<string>(type: "TEXT", nullable: true),
                    default_manga_file_extension = table.Column<int>(type: "INTEGER", nullable: false),
                    default_log_level = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration");
        }
    }
}
