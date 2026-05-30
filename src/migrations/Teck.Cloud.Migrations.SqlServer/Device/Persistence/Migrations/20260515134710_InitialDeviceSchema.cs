using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Device.Infrastructure.Migrations.SqlServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeviceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WidthPx = table.Column<int>(type: "int", nullable: true),
                    HeightPx = table.Column<int>(type: "int", nullable: true),
                    SupportedColors = table.Column<int>(type: "int", nullable: false),
                    SupportsNfc = table.Column<bool>(type: "bit", nullable: false),
                    EslProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CatalogManufacturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CatalogSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CatalogProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_layouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaxZoneCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_layouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "display_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationNodeId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResolvedTemplateId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignmentVersion = table.Column<int>(type: "int", nullable: false),
                    RenderJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RenderedImageUri = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RenderedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "displays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShortSerial = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    LongSerial = table.Column<long>(type: "bigint", nullable: true),
                    LocationNodeId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceLayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeletedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_displays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "display_assignment_zones",
                columns: table => new
                {
                    ZoneIndex = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                });

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
