using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Database.Configurations.Write;

/// <summary>Write mapping for durable leased provider work.</summary>
public sealed class ReconciliationJobConfiguration : IWriteModelConfiguration<ReconciliationJob>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReconciliationJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.ToTable("ReconciliationJobs", "orders");
        _ = builder.HasKey(job => job.Id);
        _ = builder.Property(job => job.TenantId).HasMaxLength(64).IsRequired();
        _ = builder.Property(job => job.LastErrorCategory).HasMaxLength(64);
        _ = builder.HasIndex(job => job.ProviderEventReceiptId);
        _ = builder.HasIndex(job => new { job.TenantId, job.OrderId, job.Kind }).IsUnique().HasFilter("\"Status\" = 1");
        _ = builder.HasIndex(job => new { job.Status, job.DueAt });
    }
}
