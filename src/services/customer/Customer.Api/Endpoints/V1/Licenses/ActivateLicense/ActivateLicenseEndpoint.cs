// <copyright file="ActivateLicenseEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Licenses.Features.ActivateLicense.V1;
using Customer.Application.Licenses.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Licenses.ActivateLicense;

public sealed class ActivateLicenseEndpoint(ISender sender) : Endpoint<ActivateLicenseRequest, LicenseResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Put("/Licenses/{Id:guid}/activate");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("license", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(ActivateLicenseRequest request, CancellationToken ct)
    {
        ActivateLicenseCommand command = new(request.Id);
        ErrorOr<LicenseResponse> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
