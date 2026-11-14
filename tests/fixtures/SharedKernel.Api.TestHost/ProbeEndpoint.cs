using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.Http;
using SharedKernel.Infrastructure.MultiTenancy;

namespace SharedKernel.Api.TestHost;

/// <summary>Uses standard FastEndpoints, generated discovery and typed ErrorOr HTTP mapping.</summary>
public sealed partial class ProbeEndpoint : EndpointWithoutRequest<ProbeEndpoint.ProbeResponse>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/probe");
        AllowAnonymous();
        Options(builder => builder.WithMetadata(OperationScope.Global));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var operation = Resolve<OperationContext>();
        var scopeName = operation.Scope.TenantId is null ? "global" : "tenant";
        ErrorOr<ProbeResponse> result = Query<bool>("reject", isRequired: false)
            ? Error.Validation("input", "Rejected by the fixture")
            : new ProbeResponse(scopeName);
        _ = await Send.ResultAsync(result.ToHttpResult(ProbeJson.Default.ProbeResponse)).ConfigureAwait(false);
    }

    /// <summary>Fixture response.</summary>
    /// <param name="Scope">Established operation ownership.</param>
    public sealed record ProbeResponse(string Scope);

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(ProbeResponse))]
    private sealed partial class ProbeJson : JsonSerializerContext;
}
