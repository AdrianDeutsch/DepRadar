using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScanSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scan_snapshots",
                schema: "depradar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RootPackageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    OverallLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Packages = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scan_snapshots_RootPackageId_CreatedAt",
                schema: "depradar",
                table: "scan_snapshots",
                columns: new[] { "RootPackageId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scan_snapshots",
                schema: "depradar");
        }
    }
}
