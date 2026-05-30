using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Device.Infrastructure.Migrations.MySql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeviceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "device_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ModelId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    WidthPx = table.Column<int>(type: "int", nullable: true),
                    HeightPx = table.Column<int>(type: "int", nullable: true),
                    SupportedColors = table.Column<int>(type: "int", nullable: false),
                    SupportsNfc = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EslProvider = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    CatalogManufacturerId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CatalogSupplierId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CatalogProductId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_definitions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "device_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    MaxZoneCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_layouts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "display_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DisplayId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LocationNodeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ResolvedTemplateId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    TemplateSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AssignmentVersion = table.Column<int>(type: "int", nullable: false),
                    RenderJobId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RenderedImageUri = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true),
                    RenderedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_assignments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "displays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShortSerial = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: false),
                    LongSerial = table.Column<long>(type: "bigint", nullable: true),
                    LocationNodeId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DeviceDefinitionId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DeviceLayoutId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CurrentAssignmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TenantId = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_displays", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "display_assignment_zones",
                columns: table => new
                {
                    ZoneIndex = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AssignmentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_assignment_zones", x => new { x.AssignmentId, x.ZoneIndex });
                    table.ForeignKey(
                        name: "FK_display_assignment_zones_display_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "display_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_device_definitions_model_id_unique",
                table: "device_definitions",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_layouts_device_definition_id",
                table: "device_layouts",
                column: "DeviceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "ix_display_assignments_display_id",
                table: "display_assignments",
                column: "DisplayId");

            migrationBuilder.CreateIndex(
                name: "ix_display_assignments_render_job_id",
                table: "display_assignments",
                column: "RenderJobId");

            migrationBuilder.CreateIndex(
                name: "ix_displays_short_serial_unique",
                table: "displays",
                column: "ShortSerial",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_definitions");

            migrationBuilder.DropTable(
                name: "device_layouts");

            migrationBuilder.DropTable(
                name: "display_assignment_zones");

            migrationBuilder.DropTable(
                name: "displays");

            migrationBuilder.DropTable(
                name: "display_assignments");
        }
    }
}
