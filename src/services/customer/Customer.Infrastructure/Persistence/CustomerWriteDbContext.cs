using Customer.Domain.Entities.TenantAggregate;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Customer.Infrastructure.Persistence;

/// <summary>
/// Represents the customer service database context for write operations.
/// </summary>
public class CustomerWriteDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerWriteDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public CustomerWriteDbContext(DbContextOptions<CustomerWriteDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the tenants.
    /// </summary>
    public DbSet<Tenant> Tenants { get; set; } = null!;

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerWriteDbContext).Assembly, WriteConfigFilter);
    }

    private static bool WriteConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false;
}
