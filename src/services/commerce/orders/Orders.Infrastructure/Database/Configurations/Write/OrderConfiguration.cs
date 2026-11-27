using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for the authoritative order.</summary>
public sealed class OrderConfiguration : IWriteModelConfiguration<Order>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("Orders", "orders");
        _ = builder.HasKey(order => order.Id);
        _ = builder.Property(order => order.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(order => order.SubjectId).HasMaxLength(128).IsRequired();
        _ = builder.Property(order => order.OfferId).HasMaxLength(64).IsRequired();
        _ = builder.Property(order => order.AddOnCodesCsv).HasMaxLength(1024).IsRequired();
        _ = builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();
        _ = builder.Property(order => order.Revision).IsConcurrencyToken();
        _ = builder.Property(order => order.SortKey).HasMaxLength(36).IsRequired();
        _ = builder.Property(order => order.ProviderIdempotencyKey).HasMaxLength(128).IsRequired();
        _ = builder.Property(order => order.CheckoutProviderSessionId).HasMaxLength(256);
        _ = builder.Property(order => order.CheckoutUrl).HasMaxLength(2048);
        _ = builder.Property(order => order.CheckoutLastErrorCategory).HasMaxLength(64);
        _ = builder.Property(order => order.CheckoutProviderMode).HasMaxLength(16);
        _ = builder.Property(order => order.ProviderAccountId).HasMaxLength(128);
        _ = builder.Property(order => order.ProviderSubscriptionId).HasMaxLength(256);
        _ = builder.Property(order => order.ProviderInvoiceId).HasMaxLength(256);
        _ = builder.Property(order => order.ProviderChargeId).HasMaxLength(256);
        _ = builder.Property(order => order.ProviderPaymentIntentId).HasMaxLength(256);
        _ = builder.Property(order => order.ProviderMode).HasMaxLength(16);
        _ = builder.Property(order => order.VerifiedPriceKey).HasMaxLength(2048);
        _ = builder.Property(order => order.CatalogPriceSnapshot).HasMaxLength(4096).IsRequired();
        _ = builder.Property(order => order.VerifiedPriceSnapshot).HasMaxLength(4096);
        _ = builder.Property(order => order.ApprovalActorId).HasMaxLength(128);
        _ = builder.Property(order => order.ApprovalReason).HasMaxLength(512);
        _ = builder.Property(order => order.EvidencePlacementId).HasMaxLength(64);
        _ = builder.Property(order => order.EvidenceReference).HasMaxLength(512);
        _ = builder.Property(order => order.ProofPlacementId).HasMaxLength(64);
        _ = builder.Property(order => order.DesiredAddOnCodesCsv).HasMaxLength(1024).IsRequired();
        _ = builder.Property(order => order.LicenseKeyId).HasMaxLength(32);
        _ = builder.HasIndex(order => new { order.TenantId, order.SubjectId, order.UpdatedOn, order.SortKey });
        _ = builder.HasIndex(order => new { order.TenantId, order.CheckoutProviderSessionId });
        _ = builder.HasIndex(order => new { order.TenantId, order.ProviderSubscriptionId });
        _ = builder.HasIndex(order => new { order.TenantId, order.ProviderInvoiceId });
        _ = builder.HasIndex(order => new { order.TenantId, order.ProviderChargeId });
        _ = builder.HasIndex(order => new { order.TenantId, order.ProviderPaymentIntentId });
        _ = builder.HasIndex(order => order.DesiredEnabled);
        _ = builder.Ignore(order => order.AddOnCodes);
    }
}
