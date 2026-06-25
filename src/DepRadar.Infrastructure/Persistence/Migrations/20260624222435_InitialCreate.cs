using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DepRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "depradar");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "changelog_chunks",
                schema: "depradar",
                columns: table => new
                {
                    PackageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(256)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_changelog_chunks", x => new { x.PackageId, x.Version, x.Ordinal });
                });

            migrationBuilder.CreateTable(
                name: "dependency_edges",
                schema: "depradar",
                columns: table => new
                {
                    DependentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DependentVersion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DependencyId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DependencyVersion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VersionRange = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDirect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependency_edges", x => new { x.DependentId, x.DependentVersion, x.DependencyId });
                });

            migrationBuilder.CreateTable(
                name: "package_versions",
                schema: "depradar",
                columns: table => new
                {
                    PackageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    License = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_versions", x => new { x.PackageId, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "package_vulnerabilities",
                schema: "depradar",
                columns: table => new
                {
                    PackageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AdvisoryId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_vulnerabilities", x => new { x.PackageId, x.Version, x.AdvisoryId });
                });

            migrationBuilder.CreateTable(
                name: "packages",
                schema: "depradar",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProjectUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SourceRepositoryUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    License = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    LatestStableVersion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scans",
                schema: "depradar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RootPackageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PackagesDiscovered = table.Column<int>(type: "integer", nullable: false),
                    EdgesWritten = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dependency_edges_DependencyId",
                schema: "depradar",
                table: "dependency_edges",
                column: "DependencyId");

            migrationBuilder.CreateIndex(
                name: "IX_package_versions_PackageId",
                schema: "depradar",
                table: "package_versions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_package_vulnerabilities_PackageId_Version",
                schema: "depradar",
                table: "package_vulnerabilities",
                columns: new[] { "PackageId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_scans_Status_RequestedAt",
                schema: "depradar",
                table: "scans",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "changelog_chunks",
                schema: "depradar");

            migrationBuilder.DropTable(
                name: "dependency_edges",
                schema: "depradar");

            migrationBuilder.DropTable(
                name: "package_versions",
                schema: "depradar");

            migrationBuilder.DropTable(
                name: "package_vulnerabilities",
                schema: "depradar");

            migrationBuilder.DropTable(
                name: "packages",
                schema: "depradar");

            migrationBuilder.DropTable(
                name: "scans",
                schema: "depradar");
        }
    }
}
