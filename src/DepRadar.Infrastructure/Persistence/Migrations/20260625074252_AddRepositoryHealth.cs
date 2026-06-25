using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DepRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "depradar",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCommitAt",
                schema: "depradar",
                table: "packages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "depradar",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "LastCommitAt",
                schema: "depradar",
                table: "packages");
        }
    }
}
