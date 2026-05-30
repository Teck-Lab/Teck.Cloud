// <copyright file="ProductWriteDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Persistence.Database.EFCore;
using Wolverine.EntityFrameworkCore;

namespace Product.Infrastructure.Persistence;

/// <summary>
/// EF Core persistence context for Product write-side infrastructure.
/// </summary>
public sealed class ProductWriteDbContext(
    DbContextOptions<ProductWriteDbContext> options,
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
        modelBuilder.MapWolverineEnvelopeStorage();

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ProductWriteDbContext).Assembly,
            type => type.FullName?.Contains("Config.Write", StringComparison.Ordinal) ?? false);
    }
}
