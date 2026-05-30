using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Location.Infrastructure.Migrations.MySql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLocationMetadataMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "display_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DisplayModelId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_models", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "location_nodes",
                columns: table => new
                {
                    LocationNodeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ParentLocationNodeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    TemplateId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_nodes", x => x.LocationNodeId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "template_font_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    TemplateId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    FontKey = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ObjectKey = table.Column<string>(type: "varchar(800)", maxLength: 800, nullable: false),
                    OriginalFileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_font_assets", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_display_models_TenantId",
                table: "display_models",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_display_models_TenantId_DisplayModelId",
                table: "display_models",
                columns: new[] { "TenantId", "DisplayModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_location_nodes_ParentLocationNodeId",
                table: "location_nodes",
                column: "ParentLocationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_location_nodes_TemplateId",
                table: "location_nodes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_template_font_assets_TenantId_FontKey",
                table: "template_font_assets",
                columns: new[] { "TenantId", "FontKey" });

            migrationBuilder.CreateIndex(
                name: "IX_template_font_assets_TenantId_TemplateId_FontKey",
                table: "template_font_assets",
                columns: new[] { "TenantId", "TemplateId", "FontKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "display_models");

            migrationBuilder.DropTable(
                name: "location_nodes");

            migrationBuilder.DropTable(
                name: "template_font_assets");
        }
    }
}
