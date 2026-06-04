// <copyright file="CreateTenantEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Customer.Application.Tenants.Features.CreateTenant.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Persistence.Database;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

/// <summary>
/// Handles create tenant requests.
/// </summary>
public sealed class CreateTenantEndpoint(ISender sender, IConfiguration configuration) : Endpoint<CreateTenantRequest, TenantResponse>
{
    private readonly ISender sender = sender;
    private readonly IConfiguration configuration = configuration;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Post("/admin/Tenants");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "create");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
        Validator<CreateTenantValidator>();
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(CreateTenantRequest request, CancellationToken ct)
    {
        CreateTenantCommand command = BuildCreateTenantCommand(request);
        ErrorOr<TenantResponse> commandResponse = await sender.Send(command, ct).ConfigureAwait(false);

        await this
            .SendCreatedAsync(commandResponse, value => $"/customer/v1/admin/Tenants/{value.Id}", ct)
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
