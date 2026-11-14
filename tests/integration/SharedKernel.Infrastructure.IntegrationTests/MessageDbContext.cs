using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Service-owned write model, independent of PostgreSQL model types.</summary>
/// <param name="options">Provider options.</param>
/// <param name="operation">Established scope.</param>
/// <param name="outbox">Official scoped Wolverine outbox.</param>
/// <param name="messaging">Active scoped message context.</param>
public sealed class MessageDbContext(DbContextOptions<MessageDbContext> options, OperationContext operation, IDbContextOutbox outbox, IMessageContext messaging)
    : WriteDbContext(options, operation, outbox, messaging)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<StoredRecord>().ToTable("Records");
        _ = modelBuilder.Entity<Delivery>().ToTable("Deliveries");
        _ = modelBuilder.Entity<TenantRecord>().ToTable("TenantRecords");
        _ = modelBuilder.Entity<TenantReceipt>().ToTable("TenantReceipts");
        base.OnModelCreating(modelBuilder);
    }
}
