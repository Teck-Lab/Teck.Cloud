// <copyright file="UploadTemplateFontEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591,CA1034
using ErrorOr;
using FastEndpoints;
using Location.Application.Service.Abstractions;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Location.Api.Endpoints.V1.Service;

public sealed class UploadTemplateFontEndpoint(ITemplateFontAssetService templateFontAssetService)
    : Endpoint<UploadTemplateFontEndpoint.UploadTemplateFontInput, TemplateFontUploadResponse>
{
    private readonly ITemplateFontAssetService templateFontAssetService = templateFontAssetService;

    public override void Configure()
    {
        Post("/Service/Templates/{TemplateId}/Fonts/{**FontKey}");
        Version(1);
        AllowAnonymous();
        AllowFileUploads();
        Options(endpoint => endpoint.WithMetadata(new OpenApiAudienceMetadata("public")));
    }

    public override async Task HandleAsync(UploadTemplateFontInput request, CancellationToken ct)
    {
        ErrorOr<string> tenantIdResult = ResolveTenantId(this.HttpContext);
        if (tenantIdResult.IsError)
        {
            ErrorOr<TemplateFontUploadResponse> responseError = tenantIdResult.Errors;
            await this.SendAsync(responseError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (request.File is null || request.File.Length <= 0)
        {
            ErrorOr<TemplateFontUploadResponse> validationError = Error.Validation(
                "Location.TemplateFonts.FileRequired",
                "A font file must be provided in multipart form-data field 'File'.");
            await this.SendAsync(validationError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await using Stream stream = request.File.OpenReadStream();

        ErrorOr<TemplateFontUploadResponse> result = await this.templateFontAssetService
            .UploadAsync(
                tenantIdResult.Value,
                request.TemplateId,
                request.FontKey,
                request.File.FileName,
                request.File.ContentType,
                stream,
                ct)
            .ConfigureAwait(false);

        await this.SendAsync(result, StatusCodes.Status201Created, ct).ConfigureAwait(false);
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

    public sealed class UploadTemplateFontInput
    {
        public string TemplateId { get; init; } = string.Empty;

        public string FontKey { get; init; } = string.Empty;

        public IFormFile? File { get; init; }
    }
}
