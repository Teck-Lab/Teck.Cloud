// <copyright file="CustomerReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Customer.Application.Tenants.ReadModels;
using Customer.Infrastructure.Persistence.ReadModels;
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
    public DbSet<TenantReadModel> Tenants { get; set; } = null!;

    /// <summary>
    /// Gets or sets tenant database metadata rows.
    /// </summary>
    public DbSet<TenantDatabaseMetadataReadModel> TenantDatabaseMetadata { get; set; } = null!;

    /// <summary>
    /// On model creating.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection and may require unreferenced code.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerReadDbContext).Assembly, ReadConfigFilter);

        base.OnModelCreating(modelBuilder);
    }

    private static bool ReadConfigFilter(Type type) =>
        type.FullName?.Contains("Config.Read", StringComparison.Ordinal) ?? false;
}
