// <copyright file="GetLicensesByTenantIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;
using Customer.Application.Licenses.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Licenses.GetLicensesByTenantId;

public sealed class GetLicensesByTenantIdEndpoint(ISender sender) : Endpoint<GetLicensesByTenantIdRequest, IReadOnlyList<LicenseResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Licenses");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("license", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetLicensesByTenantIdRequest request, CancellationToken ct)
    {
        GetLicensesByTenantIdQuery query = new(request.TenantId);
        ErrorOr<IReadOnlyList<LicenseResponse>> response = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
