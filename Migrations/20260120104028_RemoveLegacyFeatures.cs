using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtistInsightTool.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_revenue_entries_albums_AlbumId",
                table: "revenue_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_revenue_entries_tracks_TrackId",
                table: "revenue_entries");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropIndex(
                name: "IX_revenue_entries_AlbumId",
                table: "revenue_entries");

            migrationBuilder.DropIndex(
                name: "IX_revenue_entries_TrackId",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "AlbumId",
                table: "revenue_entries");

            migrationBuilder.DropColumn(
                name: "TrackId",
                table: "revenue_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlbumId",
                table: "revenue_entries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackId",
                table: "revenue_entries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReleaseType = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_albums_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tracks_albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tracks_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_revenue_entries_AlbumId",
                table: "revenue_entries",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_entries_TrackId",
                table: "revenue_entries",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_albums_ArtistId",
                table: "albums",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_AlbumId",
                table: "tracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_ArtistId",
                table: "tracks",
                column: "ArtistId");

            migrationBuilder.AddForeignKey(
                name: "FK_revenue_entries_albums_AlbumId",
                table: "revenue_entries",
                column: "AlbumId",
                principalTable: "albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_revenue_entries_tracks_TrackId",
                table: "revenue_entries",
                column: "TrackId",
                principalTable: "tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
