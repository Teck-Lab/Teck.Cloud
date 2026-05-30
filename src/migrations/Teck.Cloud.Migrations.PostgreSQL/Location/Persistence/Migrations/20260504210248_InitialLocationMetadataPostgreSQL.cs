using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Location.Infrastructure.Migrations.PostgreSQL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLocationMetadataPostgreSQL : global::Microsoft.EntityFrameworkCore.Migrations.Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "display_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "template_font_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FontKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_font_assets", x => x.Id);
                });

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
                name: "template_font_assets");
        }
    }
}
