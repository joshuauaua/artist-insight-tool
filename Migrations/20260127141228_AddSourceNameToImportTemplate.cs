using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
  /// <inheritdoc />
  public partial class AddSourceNameToImportTemplate : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
          name: "SourceName",
          table: "import_templates",
          type: "TEXT",
          nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropForeignKey(
          name: "FK_revenue_entries_import_templates_ImportTemplateId",
          table: "revenue_entries");

      migrationBuilder.DropColumn(
          name: "AssetTypeColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "CategoryColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "CustomerEmailColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "EventStatusColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "IsrcColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "QuantityColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "SkuColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "SourceName",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "SourcePlatformColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "TicketClassColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "TransactionDateColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "TransactionIdColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "UpcColumn",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "VenueNameColumn",
          table: "import_templates");

      migrationBuilder.AddForeignKey(
          name: "FK_revenue_entries_import_templates_ImportTemplateId",
          table: "revenue_entries",
          column: "ImportTemplateId",
          principalTable: "import_templates",
          principalColumn: "Id");
    }
  }
}
