using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Database;

namespace Identity.TestHost;

/// <summary>Shares actual Finbuckle and operation-scope guards between distinct fixture read/write contexts.</summary>
/// <param name="options">Provider configuration.</param>
/// <param name="operation">Validated request scope.</param>
/// <param name="readOnly">Whether writes are prohibited.</param>
public abstract class ProbeDbContext(DbContextOptions options, OperationContext operation, bool readOnly) : ScopedDbContext(options, operation, readOnly)
{
    /// <summary>Gets tenant-owned rows.</summary>
    public DbSet<ProbeRecord> Records => Set<ProbeRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<ProbeRecord>().ToTable("identity_records").HasKey(record => new { record.TenantId, record.Key });
        base.OnModelCreating(modelBuilder);
    }
}
