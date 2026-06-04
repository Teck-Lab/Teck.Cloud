// <copyright file="StripeWebhookEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.OpenApi;

namespace Billing.Api.Endpoints.V1.Webhooks;

/// <summary>
/// Endpoint that receives Stripe webhook event notifications.
/// </summary>
public sealed class StripeWebhookEndpoint(ILogger<StripeWebhookEndpoint> logger)
    : Endpoint<StripeWebhookRequest, EmptyResponse>
{
    private readonly ILogger<StripeWebhookEndpoint> logger = logger;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/Billing/Webhooks/Stripe");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("internal")));
    }

    /// <inheritdoc/>
    public override Task HandleAsync(StripeWebhookRequest request, CancellationToken ct)
    {
        this.logger.LogInformation("Stripe webhook received");

        // Stripe-Signature verification and event dispatching are deferred until the Stripe SDK is wired.
        this.HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }
}
