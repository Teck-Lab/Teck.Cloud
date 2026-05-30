// <copyright file="RenderFontOptions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Configuration options for tenant-scoped runtime font resolution.
/// </summary>
public sealed class RenderFontOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string Section = "RenderFonts";

    /// <summary>
    /// Gets a value indicating whether tenant custom font loading is enabled.
    /// </summary>
    public bool EnableTenantFonts { get; init; } = true;

    /// <summary>
    /// Gets the storage connection string used by FluentStorage for tenant fonts.
    /// Example values include provider specific connection strings (S3/Azure blob) accepted by FluentStorage.
    /// If empty, local directory storage is used instead.
    /// </summary>
    public string TenantFontStorageConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Gets the local blob storage root used when <see cref="TenantFontStorageConnectionString"/> is empty.
    /// </summary>
    public string TenantFontStorageLocalDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "teck-cloud", "image-generator", "tenant-font-storage");

    /// <summary>
    /// Gets the blob object key template used for tenant fonts.
    /// Supports <c>{tenantId}</c> and <c>{fontKey}</c> placeholders.
    /// </summary>
    public string TenantFontObjectKeyTemplate { get; init; } = "tenant-fonts/{tenantId}/{fontKey}";

    /// <summary>
    /// Gets a value indicating whether FluentStorage object reads should be attempted before HTTP template download.
    /// </summary>
    public bool PreferStorageOverHttpTemplate { get; init; } = true;

    /// <summary>
    /// Gets the URI template used to download tenant fonts as a fallback.
    /// Supports <c>{tenantId}</c> and <c>{fontKey}</c> placeholders.
    /// </summary>
    public string TenantFontTemplate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the local cache directory for downloaded tenant font binaries.
    /// </summary>
    public string LocalFontCacheDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "teck-cloud", "image-generator", "fonts");

    /// <summary>
    /// Gets the HTTP timeout in seconds used for downloading font binaries.
    /// </summary>
    public int DownloadTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Gets the maximum allowed font payload size in bytes.
    /// </summary>
    public int MaxFontBytes { get; init; } = 5242880;
}
