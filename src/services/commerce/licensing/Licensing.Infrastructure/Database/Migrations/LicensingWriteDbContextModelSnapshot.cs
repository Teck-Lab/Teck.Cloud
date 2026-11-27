using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

#nullable disable

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database.Migrations;

/// <summary>Design-time snapshot for the initial tenant-scoped licensing schema.</summary>
[DbContext(typeof(LicensingWriteDbContext))]
public sealed class LicensingWriteDbContextModelSnapshot : ModelSnapshot
{
    /// <inheritdoc/>
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        _ = modelBuilder
            .HasAnnotation("ProductVersion", "10.0.15")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        _ = modelBuilder.Entity("Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations.LicenseGeneration", entity =>
        {
            _ = entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            _ = entity.Property<string>("TenantId").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("tenant_id").IsConcurrencyToken();
            _ = entity.Property<Guid>("OrderId").HasColumnType("uuid").HasColumnName("order_id");
            _ = entity.Property<Guid>("SubscriptionId").HasColumnType("uuid").HasColumnName("subscription_id");
            _ = entity.Property<uint>("DesiredRevision").HasColumnType("bigint").HasColumnName("desired_revision");
            _ = entity.Property<uint>("LicenseRevision").HasColumnType("bigint").HasColumnName("license_revision");
            _ = entity.Property<uint>("Fence").HasColumnType("bigint").HasColumnName("fence").IsConcurrencyToken();
            _ = entity.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
            _ = entity.Property<bool>("IsDeleted").HasColumnType("boolean").HasColumnName("is_deleted").HasDefaultValue(value: false);

            _ = entity.HasKey("Id");
            _ = entity.HasIndex("TenantId", "OrderId", "SubscriptionId")
                .IsUnique()
                .HasDatabaseName("ux_license_generations_binding");
            _ = entity.ToTable("license_generations", "licensing");
        });

        _ = modelBuilder.Entity("Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants.LicenseGrant", entity =>
        {
            _ = entity.Property<Guid>("Id").HasColumnType("uuid").HasColumnName("id");
            _ = entity.Property<string>("TenantId").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("tenant_id").IsConcurrencyToken();
            _ = entity.Property<Guid>("OrderId").HasColumnType("uuid").HasColumnName("order_id");
            _ = entity.Property<Guid>("SubscriptionId").HasColumnType("uuid").HasColumnName("subscription_id");
            _ = entity.Property<uint>("DesiredRevision").HasColumnType("bigint").HasColumnName("desired_revision");
            _ = entity.Property<uint?>("LicenseRevision").HasColumnType("bigint").HasColumnName("license_revision");
            _ = entity.Property<LicenseGrantState>("State").HasConversion<string>().HasMaxLength(16).HasColumnType("character varying(16)").HasColumnName("state");
            _ = entity.Property<LicenseTier>("Tier").HasConversion<string>().HasMaxLength(16).HasColumnType("character varying(16)").HasColumnName("tier");
            _ = entity.Property<string>("CanonicalAddOnCodes").IsRequired().HasMaxLength(1400).HasColumnType("character varying(1400)").HasColumnName("canonical_add_on_codes");
            _ = entity.Property<string>("KeyId").HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("key_id");
            _ = entity.Property<DateTimeOffset?>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
            _ = entity.Property<string>("Document").HasColumnType("text").HasColumnName("document");
            _ = entity.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone").HasColumnName("issued_at");
            _ = entity.Property<Guid>("SourceMessageId").HasColumnType("uuid").HasColumnName("source_message_id");
            _ = entity.Property<bool>("IsDeleted").HasColumnType("boolean").HasColumnName("is_deleted").HasDefaultValue(value: false);

            _ = entity.HasKey("Id");
            _ = entity.HasIndex("TenantId", "OrderId", "SubscriptionId", "DesiredRevision")
                .IsUnique()
                .HasDatabaseName("ux_license_grants_binding_generation");
            _ = entity.HasIndex("TenantId", "OrderId", "SubscriptionId", "LicenseRevision")
                .IsUnique()
                .HasDatabaseName("ux_license_grants_binding_license_revision");
            _ = entity.HasIndex("TenantId", "OrderId", "SubscriptionId", "State")
                .HasDatabaseName("ix_license_grants_binding_state");
            _ = entity.ToTable("license_grants", "licensing");
        });
    }
}
