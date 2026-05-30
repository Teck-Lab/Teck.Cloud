// <copyright file="StripePaymentGateway.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Common.Interfaces;

namespace Billing.Infrastructure.Payment;

/// <summary>
/// Stripe implementation of <see cref="IPaymentGateway"/>.
/// This is a stub — replace with real Stripe SDK calls when the Stripe package is added.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    /// <inheritdoc/>
    public Task<string> ChargeAsync(
        string paymentMethodId,
        decimal amount,
        string currency,
        string description,
        CancellationToken cancellationToken)
    {
        // Stub implementation — returns a deterministic fake charge ID.
        // Replace by injecting StripeClient and calling PaymentIntentService when Stripe.net is added.
        string fakeChargeId = $"ch_fake_{Guid.NewGuid():N}";
        return Task.FromResult(fakeChargeId);
    }
}
