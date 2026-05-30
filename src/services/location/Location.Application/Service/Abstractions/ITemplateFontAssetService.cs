// <copyright file="ITemplateFontAssetService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Location.Application.Service.Abstractions;

public interface ITemplateFontAssetService
{
    ValueTask<ErrorOr<TemplateFontUploadResponse>> UploadAsync(
        string tenantId,
        string templateId,
        string fontKey,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    ValueTask<ErrorOr<TemplateFontListResponse>> ListAsync(
        string tenantId,
        string templateId,
        CancellationToken cancellationToken);

    ValueTask<ErrorOr<Deleted>> DeleteAsync(
        string tenantId,
        string templateId,
        string fontKey,
        CancellationToken cancellationToken);
}

public sealed record TemplateFontUploadResponse
{
    public required string TenantId { get; init; }

    public required string TemplateId { get; init; }

    public required string FontKey { get; init; }

    public required string FontFamilyToken { get; init; }

    public required string ContentType { get; init; }

    public required string ObjectKey { get; init; }

    public required string OriginalFileName { get; init; }

    public required long SizeBytes { get; init; }

    public required string ChecksumSha256 { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record TemplateFontListResponse
{
    public required string TenantId { get; init; }

    public required string TemplateId { get; init; }

    public required IReadOnlyList<TemplateFontItemResponse> Fonts { get; init; }
}

public sealed record TemplateFontItemResponse
{
    public required string FontKey { get; init; }

    public required string FontFamilyToken { get; init; }

    public required string ContentType { get; init; }

    public required string ObjectKey { get; init; }

    public required string OriginalFileName { get; init; }

    public required long SizeBytes { get; init; }

    public required string ChecksumSha256 { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
