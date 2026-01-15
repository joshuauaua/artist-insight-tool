using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetRevenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmountColumn",
                table: "ImportTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetColumn",
                table: "ImportTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "asset_revenues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevenueEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_revenues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_revenues_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_revenues_revenue_entries_RevenueEntryId",
                        column: x => x.RevenueEntryId,
                        principalTable: "revenue_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_revenues_AssetId",
                table: "asset_revenues",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_revenues_RevenueEntryId",
                table: "asset_revenues",
                column: "RevenueEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_revenues");

            migrationBuilder.DropColumn(
                name: "AmountColumn",
                table: "ImportTemplates");

            migrationBuilder.DropColumn(
                name: "AssetColumn",
                table: "ImportTemplates");
        }
    }
}
