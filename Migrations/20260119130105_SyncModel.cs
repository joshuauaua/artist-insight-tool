using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
  /// <inheritdoc />
  public partial class SyncModel : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      /*
      migrationBuilder.DropPrimaryKey(
          name: "PK_ImportTemplates",
          table: "ImportTemplates");

      migrationBuilder.RenameTable(
          name: "ImportTemplates",
          newName: "import_templates");
       

      migrationBuilder.AddColumn<string>(
          name: "ColumnMapping",
          table: "revenue_entries",
          type: "TEXT",
          nullable: true);

      migrationBuilder.AddColumn<int>(
          name: "ImportTemplateId",
          table: "revenue_entries",
          type: "INTEGER",
          nullable: true);

      migrationBuilder.AddColumn<string>(
          name: "Category",
          table: "import_templates",
          type: "TEXT",
          nullable: false,
          defaultValue: "");

      
      migrationBuilder.AddPrimaryKey(
          name: "PK_import_templates",
          table: "import_templates",
          column: "Id");
       

      migrationBuilder.CreateIndex(
          name: "IX_revenue_entries_ImportTemplateId",
          table: "revenue_entries",
          column: "ImportTemplateId");

      migrationBuilder.AddForeignKey(
          name: "FK_revenue_entries_import_templates_ImportTemplateId",
          table: "revenue_entries",
          column: "ImportTemplateId",
          principalTable: "import_templates",
          principalColumn: "Id");
      */
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropForeignKey(
          name: "FK_revenue_entries_import_templates_ImportTemplateId",
          table: "revenue_entries");

      migrationBuilder.DropIndex(
          name: "IX_revenue_entries_ImportTemplateId",
          table: "revenue_entries");

      migrationBuilder.DropPrimaryKey(
          name: "PK_import_templates",
          table: "import_templates");

      migrationBuilder.DropColumn(
          name: "ColumnMapping",
          table: "revenue_entries");

      migrationBuilder.DropColumn(
          name: "ImportTemplateId",
          table: "revenue_entries");

      migrationBuilder.DropColumn(
          name: "Category",
          table: "import_templates");

      migrationBuilder.RenameTable(
          name: "import_templates",
          newName: "ImportTemplates");

      migrationBuilder.AddPrimaryKey(
          name: "PK_ImportTemplates",
          table: "ImportTemplates",
          column: "Id");
    }
  }
}
