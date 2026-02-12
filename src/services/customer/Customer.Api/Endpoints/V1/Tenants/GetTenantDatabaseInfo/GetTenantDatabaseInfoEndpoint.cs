using Customer.Application.Tenants.DTOs;
using Customer.Application.Tenants.Queries.GetTenantDatabaseInfo;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Customer.Api.Endpoints.V1.Tenants.GetTenantDatabaseInfo;

/// <summary>
/// The get tenant database info endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetTenantDatabaseInfoEndpoint"/> class.
/// </remarks>
/// <param name="mediator">The mediator.</param>
internal class GetTenantDatabaseInfoEndpoint(ISender mediator) : Endpoint<GetTenantDatabaseInfoRequest, ServiceDatabaseInfoDto>
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
        Get("/Tenants/{TenantId}/services/{ServiceName}/database");
        Options(ep => ep.RequireProtectedResource("tenant", "read"));
        Version(1);
    }

    /// <summary>
    /// Handle the request.
    /// </summary>
    /// <param name="req">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task.</returns>
    public override async Task HandleAsync(GetTenantDatabaseInfoRequest req, CancellationToken ct)
    {
        GetTenantDatabaseInfoQuery query = new(req.TenantId, req.ServiceName);
        ErrorOr<ServiceDatabaseInfoDto> queryResponse = await _mediator.Send(query, ct);
        await this.SendAsync(queryResponse, ct);
    }
}
