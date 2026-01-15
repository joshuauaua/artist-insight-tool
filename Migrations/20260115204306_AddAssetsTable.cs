using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
  /// <inheritdoc />
  public partial class AddAssetsTable : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {


      migrationBuilder.CreateTable(
          name: "assets",
          columns: table => new
          {
            Id = table.Column<int>(type: "INTEGER", nullable: false)
                  .Annotation("Sqlite:Autoincrement", true),
            Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
            Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
            AmountGenerated = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_assets", x => x.Id);
          });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "assets");

      migrationBuilder.DropColumn(
          name: "JsonData",
          table: "revenue_entries");
    }
  }
}
