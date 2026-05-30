using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Location.Infrastructure.Migrations.PostgreSQL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationNodeNameAndTenantIdPostgreSQL : global::Microsoft.EntityFrameworkCore.Migrations.Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_location_nodes",
                table: "location_nodes");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "location_nodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "location_nodes",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "location_nodes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_location_nodes",
                table: "location_nodes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "ix_location_nodes_tenant_location_node_id",
                table: "location_nodes",
                columns: new[] { "TenantId", "LocationNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_location_nodes",
                table: "location_nodes");

            migrationBuilder.DropIndex(
                name: "ix_location_nodes_tenant_location_node_id",
                table: "location_nodes");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "location_nodes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "location_nodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "location_nodes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_location_nodes",
                table: "location_nodes",
                column: "LocationNodeId");
        }
    }
}
