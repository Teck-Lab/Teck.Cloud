// <copyright file="StripeWebhookRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
namespace Billing.Api.Endpoints.V1.Webhooks;

/// <summary>
/// Request model for incoming Stripe webhook events.
/// </summary>
public sealed class StripeWebhookRequest
{
    /// <summary>
    /// Gets or sets the Stripe-Signature header value used for payload verification.
    /// </summary>
    public string StripeSignature { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw JSON payload from Stripe.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
