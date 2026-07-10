using API.Schema.MangaContext;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations.Manga;

[DbContext(typeof(MangaContext))]
[Migration("20260710193000_AddContentKind")]
public partial class AddContentKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("ContentKind", "MangaConnector", "integer", nullable: false,
            defaultValue: (int)ContentKind.Manga);
        migrationBuilder.AddColumn<int>("ContentKind", "Mangas", "integer", nullable: false,
            defaultValue: (int)ContentKind.Manga);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ContentKind", "MangaConnector");
        migrationBuilder.DropColumn("ContentKind", "Mangas");
    }
}
