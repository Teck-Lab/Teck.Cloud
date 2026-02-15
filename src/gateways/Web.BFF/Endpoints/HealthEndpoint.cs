using ErrorOr;
using FastEndpoints;
using SharedKernel.Infrastructure.Endpoints;


namespace Web.BFF.Endpoints;

public sealed record HealthResponse(string Status);

public sealed class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        ErrorOr<object> result = new HealthResponse("ok");
        await this.SendAsync(result, cancellation: ct);
    }
}