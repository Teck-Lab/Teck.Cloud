// <copyright file="HttpLocationNodeResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1512

namespace Device.Infrastructure.AccessPoints;

internal sealed partial class HttpLocationNodeResolver(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpLocationNodeResolver> logger)
    : global::Device.Application.AccessPoints.ILocationNodeResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<HttpLocationNodeResolver> logger = logger;

    public async ValueTask<IReadOnlyList<string>> GetAncestorChainAsync(string locationNodeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationNodeId);

        using HttpClient client = httpClientFactory.CreateClient("location");
        using HttpResponseMessage response = await client
            .GetAsync(new Uri($"v1/Service/LocationNodes/{Uri.EscapeDataString(locationNodeId)}/Ancestors", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogLocationServiceError(this.logger, locationNodeId, (int)response.StatusCode, errorBody);
            return [];
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        List<string>? ancestors = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        if (ancestors is null)
        {
            LogDeserializeError(this.logger, locationNodeId);
            return [];
        }

        return ancestors;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "[HttpLocationNodeResolver] Location service returned non-success for {LocationNodeId}: {StatusCode} - {ErrorBody}")]
    private static partial void LogLocationServiceError(ILogger logger, string locationNodeId, int statusCode, string errorBody);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "[HttpLocationNodeResolver] Failed to deserialize ancestors response for {LocationNodeId}")]
    private static partial void LogDeserializeError(ILogger logger, string locationNodeId);
}
