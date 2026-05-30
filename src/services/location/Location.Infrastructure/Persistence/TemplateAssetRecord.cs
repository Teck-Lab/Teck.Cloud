// <copyright file="TemplateAssetRecord.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Infrastructure.Persistence;

/// <summary>
/// Metadata for a template asset (logo, image) stored in object storage.
/// </summary>
internal sealed class TemplateAssetRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the asset type, e.g., logo, background, icon.
    /// </summary>
    public string AssetType { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ChecksumSha256 { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
