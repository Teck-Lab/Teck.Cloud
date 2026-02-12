using Customer.Application.Tenants.Commands.UpdateMigrationStatus;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;

namespace Customer.Api.Endpoints.V1.Tenants.UpdateMigrationStatus;

/// <summary>
/// The update migration status endpoint.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UpdateMigrationStatusEndpoint"/> class.
/// </remarks>
/// <param name="mediator">The mediator.</param>
internal class UpdateMigrationStatusEndpoint(ISender mediator) : Endpoint<UpdateMigrationStatusRequest>
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
        Put("/Tenants/{TenantId}/services/{ServiceName}/migration-status");
        Options(ep => ep.RequireProtectedResource("tenant", "update"));
        Version(1);
    }

    /// <summary>
    /// Handle the request.
    /// </summary>
    /// <param name="req">The request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task.</returns>
    public override async Task HandleAsync(UpdateMigrationStatusRequest req, CancellationToken ct)
    {
        UpdateMigrationStatusCommand command = new(
            req.TenantId,
            req.ServiceName,
            req.Status,
            req.LastMigrationVersion,
            req.ErrorMessage);

        ErrorOr<Updated> commandResponse = await _mediator.Send(command, ct);
        await this.SendNoContentResponseAsync(commandResponse, cancellation: ct);
    }
}
