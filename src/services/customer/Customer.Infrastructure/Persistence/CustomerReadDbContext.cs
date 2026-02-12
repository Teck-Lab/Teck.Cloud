using Customer.Application.Tenants.DTOs;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Customer.Infrastructure.Persistence;

/// <summary>
/// Represents the customer service read database context.
/// </summary>
public sealed class CustomerReadDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerReadDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public CustomerReadDbContext(DbContextOptions<CustomerReadDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the tenants.
    /// </summary>
    public DbSet<TenantDto> Tenants { get; set; } = null!;

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerReadDbContext).Assembly, ReadConfigFilter);
    }

    private static bool ReadConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Read", StringComparison.Ordinal) ?? false;
}
