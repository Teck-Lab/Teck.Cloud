// <copyright file="BillingTransactionStatus.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.SmartEnum;

namespace Billing.Domain.Entities.BillingTransactionAggregate;

/// <summary>
/// Represents the lifecycle status of a billing transaction.
/// </summary>
public sealed class BillingTransactionStatus : SmartEnum<BillingTransactionStatus>
{
    /// <summary>The transaction has been created but not yet processed.</summary>
    public static readonly BillingTransactionStatus Pending = new(nameof(Pending), 0);

    /// <summary>The transaction was successfully processed.</summary>
    public static readonly BillingTransactionStatus Succeeded = new(nameof(Succeeded), 1);

    /// <summary>The transaction failed to process.</summary>
    public static readonly BillingTransactionStatus Failed = new(nameof(Failed), 2);

    private BillingTransactionStatus(string name, int value)
        : base(name, value)
    {
    }
}
