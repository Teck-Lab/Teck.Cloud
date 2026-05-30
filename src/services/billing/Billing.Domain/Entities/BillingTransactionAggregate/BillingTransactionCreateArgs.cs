// <copyright file="BillingTransactionCreateArgs.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Billing.Domain.Entities.BillingTransactionAggregate;

/// <summary>
/// Arguments required to create a billing transaction.
/// </summary>
public sealed class BillingTransactionCreateArgs
{
    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Gets the correlation ID linking this transaction to a saga.</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Gets the amount to charge (positive) or refund (negative).</summary>
    public decimal Amount { get; init; }

    /// <summary>Gets the currency code (e.g., "USD").</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Gets the payment method identifier, or null if not yet known.</summary>
    public string? PaymentMethodId { get; init; }

    /// <summary>Gets a human-readable description of this transaction.</summary>
    public string Description { get; init; } = string.Empty;
}
