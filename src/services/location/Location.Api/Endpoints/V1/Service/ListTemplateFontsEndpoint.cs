// <copyright file="ListTemplateFontsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CA1034
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Abstractions;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

/// <summary>
/// Handles list template fonts requests.
/// </summary>
public sealed class ListTemplateFontsEndpoint(ITemplateFontAssetService templateFontAssetService)
    : Endpoint<ListTemplateFontsEndpoint.ListTemplateFontsRoute, TemplateFontListResponse>
{
    private readonly ITemplateFontAssetService templateFontAssetService = templateFontAssetService;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Service/Templates/{TemplateId}/Fonts");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(ListTemplateFontsRoute request, CancellationToken ct)
    {
        ErrorOr<string> tenantIdResult = ResolveTenantId(this.HttpContext);
        if (tenantIdResult.IsError)
        {
            ErrorOr<TemplateFontListResponse> responseError = tenantIdResult.Errors;
            await this.SendAsync(responseError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        ErrorOr<TemplateFontListResponse> result = await this.templateFontAssetService
            .ListAsync(tenantIdResult.Value, request.TemplateId, ct)
            .ConfigureAwait(false);

        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }

    private static ErrorOr<string> ResolveTenantId(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue("X-TenantId", out var tenantValues))
        {
            return Error.Validation("Location.TemplateFonts.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        string tenantId = tenantValues.ToString().Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.TemplateFonts.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        return tenantId;
    }

    /// <summary>
    /// Represents list template fonts data.
    /// </summary>
    public sealed record ListTemplateFontsRoute(string TemplateId);
}
