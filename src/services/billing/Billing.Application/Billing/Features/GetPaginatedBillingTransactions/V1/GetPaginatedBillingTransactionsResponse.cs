// <copyright file="GetPaginatedBillingTransactionsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;

/// <summary>
/// Response model for a single billing transaction in a paginated list.
/// </summary>
public sealed class GetPaginatedBillingTransactionsResponse
{
    /// <summary>Gets or sets the transaction identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the amount charged or refunded.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment method identifier.</summary>
    public string? PaymentMethodId { get; set; }

    /// <summary>Gets or sets the external charge identifier from the payment provider.</summary>
    public string? ExternalChargeId { get; set; }

    /// <summary>Gets or sets the transaction status name.</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>Gets or sets the transaction description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last-updated timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
