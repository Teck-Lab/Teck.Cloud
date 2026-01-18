using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.ProductPriceTypes.ReadModels;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Promotions.ReadModels;
using Catalog.Application.Suppliers.ReadModels;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Represents the application's read database context.
/// </summary>
public sealed class ApplicationReadDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationReadDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public ApplicationReadDbContext(DbContextOptions<ApplicationReadDbContext> options)
        : base(options)
    { }

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationReadDbContext).Assembly, ReadConfigFilter);
    }

    /// <summary>
    /// Gets or sets the brands.
    /// </summary>
    public required DbSet<BrandReadModel> Brands { get; set; }

    /// <summary>
    /// Gets or sets the products.
    /// </summary>
    public required DbSet<ProductReadModel> Products { get; set; }

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
    public required DbSet<CategoryReadModel> Categories { get; set; }

    /// <summary>
    /// Gets or sets the product price types.
    /// </summary>
    public required DbSet<ProductPriceTypeReadModel> ProductPriceTypes { get; set; }

    /// <summary>
    /// Gets or sets the promotions.
    /// </summary>
    public required DbSet<PromotionReadModel> Promotions { get; set; }

    /// <summary>
    /// Gets or sets the suppliers.
    /// </summary>
    public required DbSet<SupplierReadModel> Suppliers { get; set; }

    /// <summary>
    /// Gets or sets the product prices.
    /// </summary>
    public required DbSet<ProductPriceReadModel> ProductPrices { get; set; }

    private static bool ReadConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Read") ?? false;
}
