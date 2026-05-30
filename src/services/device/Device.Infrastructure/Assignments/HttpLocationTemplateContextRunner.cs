// <copyright file="HttpLocationTemplateContextRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;

namespace Device.Infrastructure.Assignments;

/// <summary>
/// Calls the Location service HTTP endpoint to resolve template context via the inheritance engine.
/// </summary>
internal sealed partial class HttpLocationTemplateContextRunner(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpLocationTemplateContextRunner> logger)
    : ILocationTemplateContextRunner
{
    private readonly ILogger<HttpLocationTemplateContextRunner> logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async ValueTask<LocationTemplateContextSnapshot> ResolveTemplateContextAsync(
        string locationNodeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationNodeId);

        using HttpClient client = httpClientFactory.CreateClient("location");

        var requestBody = new { locationNodeId, explicitTemplateId = (string?)null };
        string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client
            .PostAsync(new Uri("v1/Service/ResolvedTemplate", UriKind.Relative), content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogLocationServiceError(this.logger, locationNodeId, (int)response.StatusCode, errorBody);
            return new LocationTemplateContextSnapshot(
                LocationNodeId: locationNodeId,
                ResolvedTemplateId: null,
                TemplateSource: "None",
                AncestorDepthScanned: 0);
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        ResolvedTemplateResponse? result = JsonSerializer.Deserialize<ResolvedTemplateResponse>(json, JsonOptions);

        if (result is null)
        {
            LogDeserializeError(this.logger, locationNodeId);
            return new LocationTemplateContextSnapshot(
                LocationNodeId: locationNodeId,
                ResolvedTemplateId: null,
                TemplateSource: "None",
                AncestorDepthScanned: 0);
        }

        ResolvedTemplateDesignSnapshot? design = result.TemplateDesign is null
            ? null
            : new ResolvedTemplateDesignSnapshot(
                result.TemplateDesign.TemplateId,
                result.TemplateDesign.Name,
                result.TemplateDesign.Width,
                result.TemplateDesign.Height,
                result.TemplateDesign.BackgroundColor,
                result.TemplateDesign.ElementsJson,
                result.TemplateDesign.DefaultsJson);

        return new LocationTemplateContextSnapshot(
            LocationNodeId: result.LocationNodeId,
            ResolvedTemplateId: result.ResolvedTemplateId,
            TemplateSource: result.TemplateSource,
            AncestorDepthScanned: result.InheritanceChain?.Count ?? 0,
            ResolvedTemplateDesign: design);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "[HttpLocationTemplateContext] Location service returned non-success for {LocationNodeId}: {StatusCode} - {ErrorBody}")]
    private static partial void LogLocationServiceError(ILogger logger, string locationNodeId, int statusCode, string errorBody);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "[HttpLocationTemplateContext] Failed to deserialize response for {LocationNodeId}")]
    private static partial void LogDeserializeError(ILogger logger, string locationNodeId);

#pragma warning disable S3459, S1144
    private sealed record ResolvedTemplateResponse
    {
        public string LocationNodeId { get; set; } = string.Empty;

        public string? ResolvedTemplateId { get; set; }

        public string TemplateSource { get; set; } = "None";

        public TemplateDesignResponse? TemplateDesign { get; set; }

        public List<InheritanceSourceResponse>? InheritanceChain { get; set; }
    }

    private sealed record TemplateDesignResponse
    {
        public string TemplateId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public string BackgroundColor { get; set; } = string.Empty;

        public string ElementsJson { get; set; } = string.Empty;

        public string DefaultsJson { get; set; } = "{}";
    }

    private sealed record InheritanceSourceResponse
    {
        public string ScopeType { get; set; } = string.Empty;

        public string ScopeKey { get; set; } = string.Empty;

        public string ScopeName { get; set; } = string.Empty;
    }
#pragma warning restore S3459, S1144
}
