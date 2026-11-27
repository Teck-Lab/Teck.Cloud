using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for the current per-order probe work row.</summary>
public sealed class PlacementProbeWorkConfiguration : IWriteModelConfiguration<PlacementProbeWork>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PlacementProbeWork> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("PlacementProbeWork", "orders");
        _ = builder.HasKey(work => work.Id);
        _ = builder.Property(work => work.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(work => work.PlacementId).HasMaxLength(64).IsRequired();
        _ = builder.HasIndex(work => new { work.TenantId, work.OrderId }).IsUnique();
        _ = builder.HasIndex(work => new { work.Terminal, work.DueAt });
    }
}
