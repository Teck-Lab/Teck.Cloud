// <copyright file="DeleteTemplateFontEndpoint.cs" company="TeckLab">
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
/// Handles delete template font requests.
/// </summary>
public sealed class DeleteTemplateFontEndpoint(ITemplateFontAssetService templateFontAssetService)
    : Endpoint<DeleteTemplateFontEndpoint.DeleteTemplateFontRoute, EmptyResponse>
{
    private readonly ITemplateFontAssetService templateFontAssetService = templateFontAssetService;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Delete("/Service/Templates/{TemplateId}/Fonts/{**FontKey}");
        Version(1);
        AllowAnonymous();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(DeleteTemplateFontRoute request, CancellationToken ct)
    {
        ErrorOr<string> tenantIdResult = ResolveTenantId(this.HttpContext);
        if (tenantIdResult.IsError)
        {
            ErrorOr<Deleted> responseError = tenantIdResult.Errors;
            await this.SendNoContentAsync(responseError, ct).ConfigureAwait(false);
            return;
        }

        ErrorOr<Deleted> result = await this.templateFontAssetService
            .DeleteAsync(tenantIdResult.Value, request.TemplateId, request.FontKey, ct)
            .ConfigureAwait(false);

        await this.SendNoContentAsync(result, ct).ConfigureAwait(false);
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
    /// Represents delete template font data.
    /// </summary>
    public sealed record DeleteTemplateFontRoute(string TemplateId, string FontKey);
}
