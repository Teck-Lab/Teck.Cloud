// <copyright file="IPaymentGateway.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Billing.Application.Common.Interfaces;

/// <summary>
/// Abstraction for the external payment provider (e.g., Stripe).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Charges a payment method for the given amount.
    /// </summary>
    /// <param name="paymentMethodId">The payment method to charge.</param>
    /// <param name="amount">The amount to charge in the smallest currency unit (e.g., cents for USD).</param>
    /// <param name="currency">The 3-letter ISO currency code.</param>
    /// <param name="description">Human-readable description of the charge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The external charge identifier on success.</returns>
    Task<string> ChargeAsync(
        string paymentMethodId,
        decimal amount,
        string currency,
        string description,
        CancellationToken cancellationToken);
}
