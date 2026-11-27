using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Read;

/// <summary>Read mapping for the audit projection.</summary>
public sealed class OrderAuditReadModelConfiguration : IReadModelConfiguration<OrderAuditReadModel>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderAuditReadModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("OrderAudit", "orders");
        _ = builder.HasKey(entry => entry.Id);
        _ = builder.Property(entry => entry.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(entry => entry.ActorId).HasMaxLength(128).IsRequired();
        _ = builder.Property(entry => entry.Reason).HasMaxLength(512);
    }
}
