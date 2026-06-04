// <copyright file="CreateLicenseEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Customer.Application.Licenses.Features.CreateLicense.V1;
using Customer.Application.Licenses.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Licenses.CreateLicense;

/// <summary>
/// Handles create license requests.
/// </summary>
public sealed class CreateLicenseEndpoint(ISender sender) : Endpoint<CreateLicenseRequest, LicenseResponse>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/Licenses");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("license", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(CreateLicenseRequest request, CancellationToken ct)
    {
        CreateLicenseCommand command = new(
            request.TenantId,
            request.LocationId,
            request.Plan,
            request.PaymentMethodId,
            request.PaymentScope);

        ErrorOr<LicenseResponse> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendCreatedAsync(response, value => $"/customer/v1/Licenses/{value.Id}", ct).ConfigureAwait(false);
    }
}
