using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "novel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    author = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    sitename = table.Column<string>(name: "site_name", type: "TEXT", maxLength: 50, nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    genre = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    firstchapter = table.Column<string>(name: "first_chapter", type: "TEXT", maxLength: 255, nullable: false),
                    currentchapter = table.Column<string>(name: "current_chapter", type: "TEXT", maxLength: 255, nullable: false),
                    currentchapterurl = table.Column<string>(name: "current_chapter_url", type: "TEXT", nullable: false),
                    totalchapters = table.Column<int>(name: "total_chapters", type: "INTEGER", nullable: true),
                    datecreated = table.Column<DateTime>(name: "date_created", type: "TEXT", nullable: false),
                    datelastmodified = table.Column<DateTime>(name: "date_last_modified", type: "TEXT", nullable: false),
                    lastchapter = table.Column<bool>(name: "last_chapter", type: "INTEGER", nullable: false),
                    lasttableofcontentsurl = table.Column<string>(name: "last_table_of_contents_url", type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    savelocation = table.Column<string>(name: "save_location", type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novel", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chapter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    novelid = table.Column<Guid>(name: "novel_id", type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    datecreated = table.Column<DateTime>(name: "date_created", type: "TEXT", nullable: false),
                    datelastmodified = table.Column<DateTime>(name: "date_last_modified", type: "TEXT", nullable: false),
                    number = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter", x => x.id);
                    table.ForeignKey(
                        name: "FK_chapter_novel_novel_id",
                        column: x => x.novelid,
                        principalTable: "novel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NovelLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NovelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NovelLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NovelLists_novel_NovelId",
                        column: x => x.NovelId,
                        principalTable: "novel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "page",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    chapterid = table.Column<Guid>(name: "chapter_id", type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    image = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page", x => x.id);
                    table.ForeignKey(
                        name: "FK_page_chapter_chapter_id",
                        column: x => x.chapterid,
                        principalTable: "chapter",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chapter_novel_id",
                table: "chapter",
                column: "novel_id");

            migrationBuilder.CreateIndex(
                name: "IX_NovelLists_NovelId",
                table: "NovelLists",
                column: "NovelId");

            migrationBuilder.CreateIndex(
                name: "IX_page_chapter_id",
                table: "page",
                column: "chapter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NovelLists");

            migrationBuilder.DropTable(
                name: "page");

            migrationBuilder.DropTable(
                name: "chapter");

            migrationBuilder.DropTable(
                name: "novel");
        }
    }
}
