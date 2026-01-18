using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using Catalog.Domain.Entities.SupplierAggregate;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Represents the application's database context for write operations.
/// </summary>
public class ApplicationWriteDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationWriteDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public ApplicationWriteDbContext(DbContextOptions<ApplicationWriteDbContext> options)
        : base(options)
    { }

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationWriteDbContext).Assembly, WriteConfigFilter);
    }

    /// <summary>
    /// Gets or sets the brands.
    /// </summary>
    public DbSet<Brand> Brands { get; set; }

    /// <summary>
    /// Gets or sets the products.
    /// </summary>
    public DbSet<Product> Products { get; set; }

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
    public DbSet<Category> Categories { get; set; }

    /// <summary>
    /// Gets or sets the product prices.
    /// </summary>
    public DbSet<ProductPrice> ProductPrices { get; set; }

    /// <summary>
    /// Gets or sets the product price types.
    /// </summary>
    public DbSet<ProductPriceType> ProductPriceTypes { get; set; }

    /// <summary>
    /// Gets or sets the promotions.
    /// </summary>
    public DbSet<Promotion> Promotions { get; set; }

    /// <summary>
    /// Gets or sets the suppliers.
    /// </summary>
    public DbSet<Supplier> Suppliers { get; set; }

    private static bool WriteConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false;
}
