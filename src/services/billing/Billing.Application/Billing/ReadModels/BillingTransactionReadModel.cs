// <copyright file="BillingTransactionReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Domain;

namespace Billing.Application.Billing.ReadModels;

/// <summary>
/// Read model for BillingTransaction entities, optimized for queries.
/// </summary>
[MemoryPackable]
public partial class BillingTransactionReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the tenant identifier this transaction belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID linking this transaction to a saga.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the amount charged or refunded.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency code (e.g., "USD").
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payment method identifier, or null if not applicable.
    /// </summary>
    public string? PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the external payment provider charge identifier.
    /// </summary>
    public string? ExternalChargeId { get; set; }

    /// <summary>
    /// Gets or sets the status name of this transaction.
    /// </summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable description of this transaction.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the transaction was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
