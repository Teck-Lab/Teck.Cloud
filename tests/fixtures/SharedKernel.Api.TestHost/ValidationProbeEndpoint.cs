using FastEndpoints;

namespace SharedKernel.Api.TestHost;

/// <summary>A test-only endpoint proving generated FluentValidation runs in the HTTP pipeline.</summary>
public sealed class ValidationProbeEndpoint : Endpoint<ValidationProbeRequest, ValidationProbeResponse>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("/validation-probe");
        AllowAnonymous();
    }

    /// <inheritdoc />
    public override Task HandleAsync(ValidationProbeRequest req, CancellationToken ct) =>
        Send.OkAsync(new ValidationProbeResponse(req.Name), ct);
}
