// <copyright file="GetLicenseByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Licenses.Features.GetLicenseById.V1;
using Customer.Application.Licenses.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Licenses.GetLicenseById;

public sealed class GetLicenseByIdEndpoint(ISender sender) : Endpoint<GetLicenseByIdRequest, LicenseResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Licenses/{Id:guid}");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("license", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetLicenseByIdRequest request, CancellationToken ct)
    {
        GetLicenseByIdQuery query = new(request.Id);
        ErrorOr<LicenseResponse> response = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
