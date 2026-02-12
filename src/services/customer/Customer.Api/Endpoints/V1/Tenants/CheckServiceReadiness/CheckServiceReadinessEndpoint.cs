using Customer.Application.Tenants.Queries.CheckServiceReadiness;
using ErrorOr;
using FastEndpoints;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

/// <summary>
/// The check service readiness endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CheckServiceReadinessEndpoint"/> class.
/// </remarks>
/// <param name="mediator">The mediator.</param>
internal class CheckServiceReadinessEndpoint(ISender mediator) : Endpoint<CheckServiceReadinessRequest, ServiceReadinessResponse>
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
        Get("/Tenants/{TenantId}/services/{ServiceName}/ready");
        AllowAnonymous();
        Version(1);
    }

    /// <summary>
    /// Handle the request.
    /// </summary>
    /// <param name="req">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task.</returns>
    public override async Task HandleAsync(CheckServiceReadinessRequest req, CancellationToken ct)
    {
        CheckServiceReadinessQuery query = new(req.TenantId, req.ServiceName);
        ErrorOr<bool> queryResponse = await _mediator.Send(query, ct);

        var response = queryResponse.Match<ErrorOr<ServiceReadinessResponse>>(
            value => new ServiceReadinessResponse(value),
            errors => errors);

        await this.SendAsync(response, ct);
    }
}
