// <copyright file="CreateTenantEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.CreateTenant.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Persistence.Database;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

public sealed class CreateTenantEndpoint(ISender sender, IConfiguration configuration) : Endpoint<CreateTenantRequest, TenantResponse>
{
    private readonly ISender sender = sender;
    private readonly IConfiguration configuration = configuration;

    public override void Configure()
    {
        Post("/Tenants");
        Version(1);
        Options(endpoint => endpoint.RequireProtectedResource("tenant", "create"));
    }

    public override async Task HandleAsync(CreateTenantRequest request, CancellationToken ct)
    {
        CreateTenantCommand command = BuildCreateTenantCommand(request);
        ErrorOr<TenantResponse> commandResponse = await sender.Send(command, ct).ConfigureAwait(false);

        await this
            .SendCreatedAsync(commandResponse, value => $"/customer/v1/Tenants/{value.Id}", ct)
            .ConfigureAwait(false);
    }

    private CreateTenantCommand BuildCreateTenantCommand(CreateTenantRequest request)
    {
        DatabaseStrategy databaseStrategy = DatabaseStrategy.FromName(request.DatabaseStrategy);
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        TenantDatabaseSelection databaseSelection = new()
        {
            DatabaseStrategy = databaseStrategy,
            DatabaseProvider = databaseProvider,
        };

        TenantProfile tenantProfile = new()
        {
            Name = request.Name,
            Plan = request.Plan,
        };

        return new CreateTenantCommand(request.Identifier, tenantProfile, databaseSelection);
    }
}
