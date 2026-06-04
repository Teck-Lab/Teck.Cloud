// <copyright file="IncreaseLicenseLimitsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Customer.Application.Licenses.Features.IncreaseLicenseLimits.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Licenses.IncreaseLicenseLimits;

/// <summary>
/// Handles increase license limits requests.
/// </summary>
public sealed class IncreaseLicenseLimitsEndpoint(ISender sender) : Endpoint<IncreaseLicenseLimitsRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Tenants/{TenantId:guid}/licenses/{LicenseId:guid}/limits");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("license", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(IncreaseLicenseLimitsRequest request, CancellationToken ct)
    {
        IncreaseLicenseLimitsCommand command = new(request.TenantId, request.LicenseId, request.FeatureKey, request.NewLimit, request.Currency);
        ErrorOr<Success> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendNoContentAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
