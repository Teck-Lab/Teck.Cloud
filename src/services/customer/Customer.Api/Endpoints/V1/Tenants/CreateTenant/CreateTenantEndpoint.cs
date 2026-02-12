using Customer.Application.Tenants.Commands.CreateTenant;
using Customer.Application.Tenants.DTOs;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

/// <summary>
/// The create tenant endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateTenantEndpoint"/> class.
/// </remarks>
/// <param name="mediator">The mediator.</param>
internal class CreateTenantEndpoint(ISender mediator) : Endpoint<CreateTenantRequest, TenantDto>
{
    /// <summary>
    /// The mediator.
    /// </summary>
    private readonly ISender _mediator = mediator;

    /// <summary>
    /// Configure the endpoint.
    /// </summary>
    public override void Configure()
    {
        Post("/Tenants");
        Options(ep => ep.RequireProtectedResource("tenant", "create"));
        Validator<CreateTenantValidator>();
        Version(1);
    }

    /// <summary>
    /// Handle the request.
    /// </summary>
    /// <param name="req">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task.</returns>
    public override async Task HandleAsync(CreateTenantRequest req, CancellationToken ct)
    {
        CreateTenantCommand command = new(
            req.Identifier,
            req.Name,
            req.Plan,
            SharedKernel.Core.Pricing.DatabaseStrategy.FromName(req.DatabaseStrategy),
            SharedKernel.Core.Pricing.DatabaseProvider.FromName(req.DatabaseProvider),
            req.CustomCredentials);

        ErrorOr<TenantDto> commandResponse = await _mediator.Send(command, ct);
        await this.SendCreatedAtAsync<Customer.Api.Endpoints.V1.Tenants.GetTenantById.GetTenantByIdEndpoint, ErrorOr<TenantDto>>(
            routeValues: new { commandResponse.Value?.Id },
            commandResponse,
            cancellation: ct);
    }
}
