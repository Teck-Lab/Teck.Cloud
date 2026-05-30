// <copyright file="UpgradeTenantPlanEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.UpgradeTenantPlan;

public sealed class UpgradeTenantPlanEndpoint(ISender sender) : Endpoint<UpgradeTenantPlanRequest, EmptyResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Post("/admin/Tenants/{Id:guid}/plan/upgrade");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    public override async Task HandleAsync(UpgradeTenantPlanRequest request, CancellationToken ct)
    {
        UpgradeTenantPlanCommand command = new(request.Id, request.TargetPlan, request.Currency);
        ErrorOr<Success> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendSuccessAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
