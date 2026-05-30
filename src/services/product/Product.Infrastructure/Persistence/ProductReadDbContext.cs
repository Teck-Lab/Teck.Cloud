// <copyright file="ProductReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore;

namespace Product.Infrastructure.Persistence;

/// <summary>
/// EF Core read context for Product read-side infrastructure.
/// </summary>
public sealed class ProductReadDbContext(
    DbContextOptions<ProductReadDbContext> options,
    IMultiTenantContextAccessor<TenantDetails>? tenantAccessor = null)
    : BaseDbContext(options, tenantAccessor: tenantAccessor)
{
    internal DbSet<Domain.Entities.ProductAggregate.Product> Products => this.Set<Domain.Entities.ProductAggregate.Product>();

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ProductReadDbContext).Assembly,
            type => type.FullName?.Contains("Config.Read", StringComparison.Ordinal) ?? false);
    }
}
