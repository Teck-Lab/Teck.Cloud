using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Read;

/// <summary>Read mapping for the order projection.</summary>
public sealed class OrderReadModelConfiguration : IReadModelConfiguration<OrderReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("Orders", "orders");
        _ = builder.HasKey(order => order.Id);
        _ = builder.Property(order => order.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(order => order.SubjectId).HasMaxLength(128).IsRequired();
        _ = builder.Property(order => order.OfferId).HasMaxLength(64).IsRequired();
        _ = builder.Property(order => order.AddOnCodesCsv).HasMaxLength(1024).IsRequired();
        _ = builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();
        _ = builder.Property(order => order.SortKey).HasMaxLength(36).IsRequired();
        _ = builder.Property(order => order.ApprovalActorId).HasMaxLength(128);
        _ = builder.Property(order => order.ApprovalReason).HasMaxLength(512);
        _ = builder.Property(order => order.EvidencePlacementId).HasMaxLength(64);
        _ = builder.Property(order => order.EvidenceReference).HasMaxLength(512);
        _ = builder.Property(order => order.ProofPlacementId).HasMaxLength(64);
        _ = builder.Property(order => order.LicenseKeyId).HasMaxLength(32);
    }
}
