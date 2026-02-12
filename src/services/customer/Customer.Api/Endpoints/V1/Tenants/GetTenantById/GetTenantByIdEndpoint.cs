using Customer.Application.Tenants.DTOs;
using Customer.Application.Tenants.Queries.GetTenantById;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Customer.Api.Endpoints.V1.Tenants.GetTenantById;

/// <summary>
/// The get tenant by id endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetTenantByIdEndpoint"/> class.
/// </remarks>
/// <param name="mediator">The mediator.</param>
internal class GetTenantByIdEndpoint(ISender mediator) : Endpoint<GetTenantByIdRequest, TenantDto>
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
        Get("/Tenants/{Id}");
        Options(ep => ep.RequireProtectedResource("tenant", "read"));
        Version(1);
    }

    /// <summary>
    /// Handle the request.
    /// </summary>
    /// <param name="req">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task.</returns>
    public override async Task HandleAsync(GetTenantByIdRequest req, CancellationToken ct)
    {
        GetTenantByIdQuery query = new(req.Id);
        ErrorOr<TenantDto> queryResponse = await _mediator.Send(query, ct);
        await this.SendAsync(queryResponse, ct);
    }
}
