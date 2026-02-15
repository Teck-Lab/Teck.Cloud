using System.Reflection;
using FastEndpoints;

namespace Catalog.Api.Endpoints.Service.V1;

internal sealed record ServiceVersionResponse(string Service, string Version);

internal sealed class GetServiceVersionEndpoint : EndpointWithoutRequest<ServiceVersionResponse>
{
    public override void Configure()
    {
        Get("/Service/Version");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var assembly = typeof(GetServiceVersionEndpoint).Assembly;
        var version =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        await Send.OkAsync(new ServiceVersionResponse("catalog", version), ct);
    }
}
