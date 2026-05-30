// <copyright file="BillingReadDbContext.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Billing.Application.Billing.ReadModels;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// Represents the billing service read-optimised database context.
/// </summary>
public sealed class BillingReadDbContext : BaseDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BillingReadDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
    public BillingReadDbContext(DbContextOptions<BillingReadDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the billing transaction read models.
    /// </summary>
    public DbSet<BillingTransactionReadModel> BillingTransactions { get; set; } = null!;

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Calls ApplyConfigurationsFromAssembly which uses reflection and may require unreferenced code.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<BillingTransactionReadModel>(entity =>
        {
            entity.ToTable("BillingTransactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.TenantId).IsRequired();
            entity.Property(transaction => transaction.CorrelationId).IsRequired();
            entity.Property(transaction => transaction.Amount).HasPrecision(18, 4).IsRequired();
            entity.Property(transaction => transaction.Currency).HasMaxLength(8).IsRequired();
            entity.Property(transaction => transaction.StatusName).HasMaxLength(64).IsRequired();
            entity.Property(transaction => transaction.Description).HasMaxLength(512).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
