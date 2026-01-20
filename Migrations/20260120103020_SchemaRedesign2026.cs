using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
    /// <inheritdoc />
    public partial class SchemaRedesign2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "revenue_entries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Quarter",
                table: "revenue_entries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadDate",
                table: "revenue_entries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "revenue_entries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtistColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DspColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrossColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerritoryColumn",
                table: "import_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "assets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "assets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "Quarter",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "UploadDate",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "ArtistColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "CollectionColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "CurrencyColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "DspColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "GrossColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "LabelColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "NetColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "StoreColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "TerritoryColumn",
                table: "import_templates");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "assets");
        }
    }
}
