using FastEndpoints;

namespace Web.BFF.Endpoints;

public sealed record ServiceVersionItem(string Service, string Version);

public sealed record ServiceVersionsResponse(IReadOnlyList<ServiceVersionItem> Services);

internal sealed record DownstreamServiceVersionResponse(string Service, string Version);

public sealed class ServiceVersionsEndpoint(IHttpClientFactory httpClientFactory) : EndpointWithoutRequest<ServiceVersionsResponse>
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public override void Configure()
    {
        Get("/Services/Versions");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Task<ServiceVersionItem> catalogTask = GetServiceVersionAsync("CatalogApi", "catalog", ct);
        Task<ServiceVersionItem> customerTask = GetServiceVersionAsync("CustomerApi", "customer", ct);

        await Task.WhenAll(catalogTask, customerTask);

        var response = new ServiceVersionsResponse([catalogTask.Result, customerTask.Result]);
        await Send.OkAsync(response, ct);
    }

    private async Task<ServiceVersionItem> GetServiceVersionAsync(string clientName, string fallbackServiceName, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(clientName);

        try
        {
            DownstreamServiceVersionResponse? versionResponse = await client.GetFromJsonAsync<DownstreamServiceVersionResponse>(
                "/api/v1/service/version",
                ct);

            if (versionResponse is null)
            {
                return new ServiceVersionItem(fallbackServiceName, "unavailable");
            }

            return new ServiceVersionItem(versionResponse.Service, versionResponse.Version);
        }
        catch
        {
            return new ServiceVersionItem(fallbackServiceName, "unavailable");
        }
    }
}