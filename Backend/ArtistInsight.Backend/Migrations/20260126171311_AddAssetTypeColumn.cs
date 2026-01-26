using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsight.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetTypeColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssetTypeColumn",
                table: "import_templates");
        }
    }
}
