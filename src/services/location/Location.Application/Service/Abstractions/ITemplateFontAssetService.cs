// <copyright file="ITemplateFontAssetService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Service for managing template font assets.
/// </summary>
public interface ITemplateFontAssetService
{
    /// <summary>
    /// Uploads a template font asset.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="fontKey">The font key.</param>
    /// <param name="fileName">The source file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="content">The font content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result payload or an error.</returns>
    ValueTask<ErrorOr<TemplateFontUploadResponse>> UploadAsync(
        string tenantId,
        string templateId,
        string fontKey,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists template font assets.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Font list payload or an error.</returns>
    ValueTask<ErrorOr<TemplateFontListResponse>> ListAsync(
        string tenantId,
        string templateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a template font asset.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="fontKey">The font key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deletion result or an error.</returns>
    ValueTask<ErrorOr<Deleted>> DeleteAsync(
        string tenantId,
        string templateId,
        string fontKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Response payload for a template font upload operation.
/// </summary>
public sealed record TemplateFontUploadResponse
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the template identifier.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the font key.
    /// </summary>
    public required string FontKey { get; init; }

    /// <summary>
    /// Gets the font family token.
    /// </summary>
    public required string FontFamilyToken { get; init; }

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the object storage key.
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// Gets the original file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the uploaded file size in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// Gets the SHA-256 checksum.
    /// </summary>
    public required string ChecksumSha256 { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of the latest update.
    /// </summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>
/// Response payload for template font list retrieval.
/// </summary>
public sealed record TemplateFontListResponse
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the template identifier.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the listed fonts.
    /// </summary>
    public required IReadOnlyList<TemplateFontItemResponse> Fonts { get; init; }
}

/// <summary>
/// Response payload describing a single template font.
/// </summary>
public sealed record TemplateFontItemResponse
{
    /// <summary>
    /// Gets the font key.
    /// </summary>
    public required string FontKey { get; init; }

    /// <summary>
    /// Gets the font family token.
    /// </summary>
    public required string FontFamilyToken { get; init; }

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Gets the object storage key.
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// Gets the original file name.
    /// </summary>
    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// Gets the SHA-256 checksum.
    /// </summary>
    public required string ChecksumSha256 { get; init; }

    /// <summary>
    /// Gets the UTC timestamp of the latest update.
    /// </summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
