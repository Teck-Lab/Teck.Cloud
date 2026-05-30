// <copyright file="BillingTransaction.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.Domain;

namespace Billing.Domain.Entities.BillingTransactionAggregate;

/// <summary>
/// Aggregate root representing a billing transaction — a charge or refund against a payment method.
/// </summary>
public sealed class BillingTransaction : BaseEntity, IAggregateRoot
{
    private BillingTransaction()
    {
    }

    /// <summary>Gets the tenant identifier this transaction belongs to.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the correlation ID linking this transaction to a saga.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Gets the amount charged or refunded (positive = charge, negative = refund).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the currency code (e.g., "USD").</summary>
    public string Currency { get; private set; } = default!;

    /// <summary>Gets the payment method identifier used for this transaction.</summary>
    public string? PaymentMethodId { get; private set; }

    /// <summary>Gets the external payment provider charge identifier (e.g., Stripe charge ID).</summary>
    public string? ExternalChargeId { get; private set; }

    /// <summary>Gets the current status of this transaction.</summary>
    public BillingTransactionStatus Status { get; private set; } = default!;

    /// <summary>Gets a human-readable description of this transaction.</summary>
    public string Description { get; private set; } = default!;

    /// <summary>Gets the date and time when the transaction was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new pending billing transaction.
    /// </summary>
    /// <param name="args">The creation arguments.</param>
    /// <returns>The created transaction or validation errors.</returns>
    public static ErrorOr<BillingTransaction> Create(BillingTransactionCreateArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        BillingTransaction tx = new()
        {
            TenantId = args.TenantId,
            CorrelationId = args.CorrelationId,
            Amount = args.Amount,
            Currency = args.Currency,
            PaymentMethodId = args.PaymentMethodId,
            Description = args.Description,
            Status = BillingTransactionStatus.Pending,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        return tx;
    }

    /// <summary>
    /// Marks the transaction as succeeded with the external charge identifier.
    /// </summary>
    /// <param name="externalChargeId">The external charge identifier from the payment provider.</param>
    public void MarkSucceeded(string externalChargeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalChargeId);
        this.ExternalChargeId = externalChargeId;
        this.Status = BillingTransactionStatus.Succeeded;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the transaction as failed.
    /// </summary>
    public void MarkFailed()
    {
        this.Status = BillingTransactionStatus.Failed;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
