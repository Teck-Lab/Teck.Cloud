using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Location.Infrastructure.Migrations.PostgreSQL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationNodesPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "location_nodes",
                columns: table => new
                {
                    LocationNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentLocationNodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TemplateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_nodes", x => x.LocationNodeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_location_nodes_ParentLocationNodeId",
                table: "location_nodes",
                column: "ParentLocationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_location_nodes_TemplateId",
                table: "location_nodes",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_nodes");
        }
    }
}
