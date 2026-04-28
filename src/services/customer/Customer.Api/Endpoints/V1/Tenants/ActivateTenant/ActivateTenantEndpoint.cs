// <copyright file="ActivateTenantEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.ActivateTenant.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.ActivateTenant;

public sealed class ActivateTenantEndpoint(ISender sender) : Endpoint<ActivateTenantRequest, TenantResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Put("/admin/Tenants/{Id:guid}/activate");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    public override async Task HandleAsync(ActivateTenantRequest request, CancellationToken ct)
    {
        ActivateTenantCommand command = new(request.Id);
        ErrorOr<TenantResponse> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
