using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database.Migrations;

/// <summary>Creates the tenant-scoped grant, generation-fence and soft-delete schema.</summary>
[DbContext(typeof(LicensingWriteDbContext))]
[Migration("20261127120000_InitialLicensing")]
public sealed class InitialLicensing : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.EnsureSchema(name: "licensing");

        CreateLicenseGenerationsTable(migrationBuilder);
        CreateLicenseGrantsTable(migrationBuilder);
        CreateLicenseGrantIndexes(migrationBuilder);
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(name: "license_grants", schema: "licensing");
        _ = migrationBuilder.DropTable(name: "license_generations", schema: "licensing");
        _ = migrationBuilder.DropSchema(name: "licensing");
    }

    private static void CreateLicenseGenerationsTable(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "license_generations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                desired_revision = table.Column<long>(type: "bigint", nullable: false),
                license_revision = table.Column<long>(type: "bigint", nullable: false),
                fence = table.Column<long>(type: "bigint", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
            },
            schema: "licensing",
            constraints: table => table.PrimaryKey("PK_license_generations", x => x.id));
    }

    private static void CreateLicenseGrantsTable(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "license_grants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                desired_revision = table.Column<long>(type: "bigint", nullable: false),
                license_revision = table.Column<long>(type: "bigint", nullable: true),
                tier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                canonical_add_on_codes = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                key_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                document = table.Column<string>(type: "text", nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                source_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
            },
            schema: "licensing",
            constraints: table => table.PrimaryKey("PK_license_grants", x => x.id));
    }

    private static void CreateLicenseGrantIndexes(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateIndex(
            name: "ux_license_generations_binding",
            table: "license_generations",
            columns: ["tenant_id", "order_id", "subscription_id"],
            schema: "licensing",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "ux_license_grants_binding_generation",
            table: "license_grants",
            columns: ["tenant_id", "order_id", "subscription_id", "desired_revision"],
            schema: "licensing",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "ux_license_grants_binding_license_revision",
            table: "license_grants",
            columns: ["tenant_id", "order_id", "subscription_id", "license_revision"],
            schema: "licensing",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "ix_license_grants_binding_state",
            table: "license_grants",
            columns: ["tenant_id", "order_id", "subscription_id", "state"],
            schema: "licensing");
    }
}
