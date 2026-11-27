using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Database.Configurations;

/// <summary>Maps the immutable per-generation grant snapshot shared by read and write models.</summary>
public sealed class LicenseGrantConfiguration : ISharedModelConfiguration<LicenseGrant>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LicenseGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("license_grants", "licensing");
        _ = builder.HasKey(grant => grant.Id);
        _ = builder.Property(grant => grant.Id).HasColumnName("id").ValueGeneratedNever().IsRequired();
        _ = builder.Property(grant => grant.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        _ = builder.Property(grant => grant.OrderId).HasColumnName("order_id").IsRequired();
        _ = builder.Property(grant => grant.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        _ = builder.Property(grant => grant.DesiredRevision).HasColumnName("desired_revision").HasConversion(value => (long)value, value => (uint)value).IsRequired();
        _ = builder.Property(grant => grant.LicenseRevision).HasColumnName("license_revision").HasConversion(
            value => value.HasValue ? (long?)value.Value : null,
            value => value.HasValue ? (uint?)value.Value : null);
        _ = builder.Property(grant => grant.Tier).HasColumnName("tier").HasConversion(
            tier => tier == LicenseTier.Shared ? "shared" : "dedicated",
            value => value == "shared" ? LicenseTier.Shared : LicenseTier.Dedicated).HasMaxLength(16).IsRequired();
        _ = builder.Property(grant => grant.State).HasColumnName("state").HasConversion(
            state => ToStoredState(state),
            value => FromStoredState(value))
            .HasMaxLength(16).IsRequired();
        _ = builder.Property(grant => grant.CanonicalAddOnCodes).HasColumnName("canonical_add_on_codes").HasMaxLength(1400).IsRequired();
        _ = builder.Property(grant => grant.KeyId).HasColumnName("key_id").HasMaxLength(64);
        _ = builder.Property(grant => grant.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        _ = builder.Property(grant => grant.Document).HasColumnName("document").HasColumnType("text");
        _ = builder.Property(grant => grant.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamp with time zone").IsRequired();
        _ = builder.Property(grant => grant.SourceMessageId).HasColumnName("source_message_id").IsRequired();
        _ = builder.Property(grant => grant.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(value: false).IsRequired();

        _ = builder.Ignore(grant => grant.AddOnCodes);
        _ = builder.Ignore(grant => grant.CreatedAt);
        _ = builder.Ignore(grant => grant.CreatedBy);
        _ = builder.Ignore(grant => grant.UpdatedOn);
        _ = builder.Ignore(grant => grant.UpdatedBy);
        _ = builder.Ignore(grant => grant.DeletedOn);
        _ = builder.Ignore(grant => grant.DeletedBy);

        _ = builder.HasIndex(grant => new { grant.TenantId, grant.OrderId, grant.SubscriptionId, grant.DesiredRevision })
            .IsUnique()
            .HasDatabaseName("ux_license_grants_binding_generation");
        _ = builder.HasIndex(grant => new { grant.TenantId, grant.OrderId, grant.SubscriptionId, grant.LicenseRevision })
            .IsUnique()
            .HasDatabaseName("ux_license_grants_binding_license_revision");
        _ = builder.HasIndex(grant => new { grant.TenantId, grant.OrderId, grant.SubscriptionId, grant.State })
            .HasDatabaseName("ix_license_grants_binding_state");
    }

    private static string ToStoredState(LicenseGrantState state) => state switch
    {
        LicenseGrantState.Active => "active",
        LicenseGrantState.Inactive => "inactive",
        LicenseGrantState.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static LicenseGrantState FromStoredState(string value) => value switch
    {
        "active" => LicenseGrantState.Active,
        "inactive" => LicenseGrantState.Inactive,
        "superseded" => LicenseGrantState.Superseded,
        _ => throw new InvalidOperationException("Unrecognized stored license grant state."),
    };
}
