// <copyright file="BillingDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Billing.Domain.Entities.BillingTransactionAggregate;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// Represents the billing service database context.
/// </summary>
public sealed class BillingDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BillingDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the billing transactions.
    /// </summary>
    public DbSet<BillingTransaction> BillingTransactions { get; set; } = null!;

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection and may require unreferenced code.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
