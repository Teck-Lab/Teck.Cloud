using System;
using Device.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Device.Infrastructure.Migrations.PostgreSQL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DeviceWriteDbContext))]
    [Migration("20260519230000_AddAssignmentSnapshots")]
    public partial class AddAssignmentSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateSnapshot",
                table: "display_assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDataSnapshot",
                table: "display_assignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateSnapshot",
                table: "display_assignments");

            migrationBuilder.DropColumn(
                name: "ProductDataSnapshot",
                table: "display_assignments");
        }
    }
}
