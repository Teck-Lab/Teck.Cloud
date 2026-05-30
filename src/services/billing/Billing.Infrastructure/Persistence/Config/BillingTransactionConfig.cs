// <copyright file="BillingTransactionConfig.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Domain.Entities.BillingTransactionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Infrastructure.Persistence.Config;

/// <summary>
/// Provides Entity Framework configuration for the <see cref="BillingTransaction"/> entity.
/// </summary>
public sealed class BillingTransactionConfig : IEntityTypeConfiguration<BillingTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BillingTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BillingTransactions");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.TenantId)
            .IsRequired();

        builder.Property(transaction => transaction.CorrelationId)
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(transaction => transaction.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(transaction => transaction.PaymentMethodId)
            .HasMaxLength(255);

        builder.Property(transaction => transaction.ExternalChargeId)
            .HasMaxLength(255);

        builder.Property(transaction => transaction.Status)
            .HasConversion(
                status => status.Name,
                name => BillingTransactionStatus.FromName(name, false))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(transaction => transaction.UpdatedAt)
            .IsRequired();
    }
}
