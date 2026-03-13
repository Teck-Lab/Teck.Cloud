// <copyright file="ApplicationWriteDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Domain.Entities.PromotionAggregate;
using Catalog.Domain.Entities.SupplierAggregate;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
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
    /// <param name="tenantAccessor">The tenant context accessor used for resolving the current tenant.</param>
    public ApplicationWriteDbContext(
        DbContextOptions<ApplicationWriteDbContext> options,
        IMultiTenantContextAccessor<TenantDetails>? tenantAccessor = null)
        : base(options, tenantAccessor: tenantAccessor)
    {
    }

    /// <summary>
    /// Gets or sets the brands.
    /// </summary>
    public DbSet<Brand> Brands { get; set; } = null!;

    /// <summary>
    /// Gets or sets the products.
    /// </summary>
    public DbSet<Product> Products { get; set; } = null!;

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
    public DbSet<Category> Categories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the product prices.
    /// </summary>
    public DbSet<ProductPrice> ProductPrices { get; set; } = null!;

    /// <summary>
    /// Gets or sets the product price types.
    /// </summary>
    public DbSet<ProductPriceType> ProductPriceTypes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the promotions.
    /// </summary>
    public DbSet<Promotion> Promotions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the suppliers.
    /// </summary>
    public DbSet<Supplier> Suppliers { get; set; } = null!;

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    [RequiresUnreferencedCode()]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationWriteDbContext).Assembly, WriteConfigFilter);
    }

    private static bool WriteConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false;
}
