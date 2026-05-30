// <copyright file="TenantFontAssetStore.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography;
using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Resolves, downloads, and caches tenant font assets for render operations.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="fontOptions">The render font options.</param>
/// <param name="logger">The logger.</param>
public sealed class TenantFontAssetStore(
    IHttpClientFactory httpClientFactory,
    IOptions<RenderFontOptions> fontOptions,
    ILogger<TenantFontAssetStore> logger)
    : ITenantFontAssetStore
{
    private static readonly IReadOnlyDictionary<string, string> EmptyFontMap = new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadLocks = new(StringComparer.Ordinal);

    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly RenderFontOptions fontOptions = fontOptions.Value;
    private readonly ILogger<TenantFontAssetStore> logger = logger;
    private readonly IBlobStorage blobStorage = CreateBlobStorage(fontOptions.Value);

    /// <summary>
    /// Ensures tenant font files are available in the local cache.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="fontFamilies">The requested font families.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A mapping from tenant font keys to local cache file paths.
    /// </returns>
    public async ValueTask<IReadOnlyDictionary<string, string>> EnsureFontsAvailableAsync(
        string tenantId,
        IReadOnlyCollection<string> fontFamilies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fontFamilies);

        if (fontFamilies.Count == 0
            || string.IsNullOrWhiteSpace(tenantId)
            || !this.fontOptions.EnableTenantFonts)
        {
            return EmptyFontMap;
        }

        Dictionary<string, string> resolved = new(StringComparer.OrdinalIgnoreCase);

        foreach (string fontFamily in fontFamilies)
        {
            if (!TenantFontKeys.TryParseTenantFontKey(fontFamily, out string fontKey))
            {
                continue;
            }

            if (resolved.ContainsKey(fontKey))
            {
                continue;
            }

            try
            {
                string localPath = await EnsureFontFileAsync(tenantId, fontKey, cancellationToken).ConfigureAwait(false);
                resolved[fontKey] = localPath;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    exception,
                    "Unable to resolve tenant font. TenantId={TenantId} FontKey={FontKey}",
                    tenantId,
                    fontKey);
            }
        }

        return resolved;
    }

    private async Task<string> EnsureFontFileAsync(string tenantId, string fontKey, CancellationToken cancellationToken)
    {
        string localPath = BuildLocalCachePath(tenantId, fontKey);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        string lockKey = $"{tenantId}|{fontKey}";
        SemaphoreSlim gate = DownloadLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(localPath))
            {
                return localPath;
            }

            string targetDirectory = Path.GetDirectoryName(localPath)
                ?? throw new InvalidOperationException("Unable to resolve local font cache directory.");

            Directory.CreateDirectory(targetDirectory);

            bool downloaded = false;

            if (this.fontOptions.PreferStorageOverHttpTemplate)
            {
                downloaded = await TryDownloadFromStorageAsync(localPath, tenantId, fontKey, cancellationToken).ConfigureAwait(false);
                if (!downloaded)
                {
                    downloaded = await TryDownloadFromHttpTemplateAsync(localPath, tenantId, fontKey, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                downloaded = await TryDownloadFromHttpTemplateAsync(localPath, tenantId, fontKey, cancellationToken).ConfigureAwait(false);
                if (!downloaded)
                {
                    downloaded = await TryDownloadFromStorageAsync(localPath, tenantId, fontKey, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!downloaded)
            {
                throw new FileNotFoundException($"Font asset '{fontKey}' for tenant '{tenantId}' was not found in configured sources.");
            }

            return localPath;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<bool> TryDownloadFromStorageAsync(
        string localPath,
        string tenantId,
        string fontKey,
        CancellationToken cancellationToken)
    {
        string objectKey = BuildTenantFontObjectKey(tenantId, fontKey);
        bool exists = await this.blobStorage.ExistsAsync(objectKey, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await using Stream input = await this.blobStorage.OpenReadAsync(objectKey, cancellationToken).ConfigureAwait(false);
        await WriteStreamToLocalCacheAsync(localPath, fontKey, input, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<bool> TryDownloadFromHttpTemplateAsync(
        string localPath,
        string tenantId,
        string fontKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.fontOptions.TenantFontTemplate))
        {
            return false;
        }

        Uri requestUri = BuildTenantFontUri(tenantId, fontKey);
        HttpClient client = this.httpClientFactory.CreateClient(nameof(TenantFontAssetStore));
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, this.fontOptions.DownloadTimeoutSeconds));

        using HttpResponseMessage response = await client
            .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength
            && contentLength > Math.Max(1, this.fontOptions.MaxFontBytes))
        {
            throw new InvalidOperationException(
                $"Font asset '{fontKey}' exceeds maximum size {this.fontOptions.MaxFontBytes} bytes.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await WriteStreamToLocalCacheAsync(localPath, fontKey, input, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task WriteStreamToLocalCacheAsync(
        string localPath,
        string fontKey,
        Stream input,
        CancellationToken cancellationToken)
    {
        string tempPath = $"{localPath}.{Guid.NewGuid():N}.download";

        try
        {
            await using FileStream output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

            if (output.Length > Math.Max(1, this.fontOptions.MaxFontBytes))
            {
                throw new InvalidOperationException(
                    $"Font asset '{fontKey}' exceeds maximum size {this.fontOptions.MaxFontBytes} bytes.");
            }

            output.Close();
            File.Move(tempPath, localPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private Uri BuildTenantFontUri(string tenantId, string fontKey)
    {
        string tenantToken = Uri.EscapeDataString(tenantId.Trim());
        string fontToken = string.Join(
            '/',
            fontKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        string resolvedUrl = this.fontOptions.TenantFontTemplate
            .Replace("{tenantId}", tenantToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{fontKey}", fontToken, StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out Uri? resolvedUri))
        {
            throw new InvalidOperationException("Render font URL template produced an invalid absolute URI.");
        }

        return resolvedUri;
    }

    private string BuildTenantFontObjectKey(string tenantId, string fontKey)
    {
        string tenantToken = Uri.EscapeDataString(tenantId.Trim());
        string fontToken = string.Join(
            '/',
            fontKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => Uri.EscapeDataString(segment.Trim())));

        string resolved = this.fontOptions.TenantFontObjectKeyTemplate
            .Replace("{tenantId}", tenantToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{fontKey}", fontToken, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException("Tenant font object key template produced an empty key.");
        }

        return resolved.TrimStart('/');
    }

    private string BuildLocalCachePath(string tenantId, string fontKey)
    {
        string cacheRoot = string.IsNullOrWhiteSpace(this.fontOptions.LocalFontCacheDirectory)
            ? Path.Combine(Path.GetTempPath(), "teck-cloud", "image-generator", "fonts")
            : this.fontOptions.LocalFontCacheDirectory;

        string safeTenantSegment = SanitizePathSegment(tenantId);
        string extension = NormalizeFontExtension(Path.GetExtension(fontKey));
        byte[] keyBytes = Encoding.UTF8.GetBytes(fontKey);
        string keyHash = Convert.ToHexString(SHA256.HashData(keyBytes));

        return Path.Combine(cacheRoot, safeTenantSegment, $"{keyHash}{extension}");
    }

    private static string NormalizeFontExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".ttf";
        }

        string normalized = extension.Trim().ToLowerInvariant();
        return normalized is ".ttf" or ".otf" or ".ttc"
            ? normalized
            : ".ttf";
    }

    private static string SanitizePathSegment(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        string sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "tenant" : sanitized;
    }

    private static IBlobStorage CreateBlobStorage(RenderFontOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TenantFontStorageConnectionString))
        {
            return StorageFactory.Blobs.FromConnectionString(options.TenantFontStorageConnectionString);
        }

        string localDirectory = string.IsNullOrWhiteSpace(options.TenantFontStorageLocalDirectory)
            ? Path.Combine(Path.GetTempPath(), "teck-cloud", "image-generator", "tenant-font-storage")
            : options.TenantFontStorageLocalDirectory;

        Directory.CreateDirectory(localDirectory);
        return StorageFactory.Blobs.DirectoryFiles(localDirectory);
    }
}
