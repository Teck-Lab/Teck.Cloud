// <copyright file="TemplateFontAssetService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using ErrorOr;
using FluentStorage;
using FluentStorage.Blobs;
using Location.Application.Service.Abstractions;
using Location.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Location.Infrastructure.Service;

internal sealed class TemplateFontAssetService(
    IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory,
    IOptions<TemplateFontStorageOptions> options,
    ILogger<TemplateFontAssetService> logger)
    : ITemplateFontAssetService
{
    private const string TenantFontPrefix = "tenant-font:";

    private readonly IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory = dbContextFactory;
    private readonly ILogger<TemplateFontAssetService> logger = logger;
    private readonly TemplateFontStorageOptions options = options.Value;

    private readonly IBlobStorage blobStorage = CreateBlobStorage(options.Value);

    public async ValueTask<ErrorOr<TemplateFontUploadResponse>> UploadAsync(
        string tenantId,
        string templateId,
        string fontKey,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.TemplateFonts.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Error.Validation("Location.TemplateFonts.TemplateIdRequired", "TemplateId is required.");
        }

        if (!TryNormalizeFontKey(fontKey, out string normalizedFontKey))
        {
            return Error.Validation("Location.TemplateFonts.FontKeyInvalid", "FontKey is required and must not contain empty segments.");
        }

        await using MemoryStream payload = new();
        await content.CopyToAsync(payload, cancellationToken).ConfigureAwait(false);

        if (payload.Length == 0)
        {
            return Error.Validation("Location.TemplateFonts.EmptyFile", "Font file content is empty.");
        }

        if (payload.Length > Math.Max(1, this.options.MaxFontBytes))
        {
            return Error.Validation(
                "Location.TemplateFonts.FileTooLarge",
                $"Font file exceeds maximum size of {this.options.MaxFontBytes} bytes.");
        }

        payload.Position = 0;

        string normalizedTenantId = tenantId.Trim();
        string normalizedTemplateId = templateId.Trim();
        string normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "font/ttf"
            : contentType.Trim();

        string objectKey = BuildObjectKey(this.options.ObjectKeyTemplate, normalizedTenantId, normalizedFontKey);

        bool objectAlreadyExists = await this.blobStorage.ExistsAsync(objectKey, cancellationToken).ConfigureAwait(false);
        if (objectAlreadyExists)
        {
            await this.blobStorage.DeleteAsync(objectKey, cancellationToken).ConfigureAwait(false);
        }

        payload.Position = 0;

        await this.blobStorage
            .WriteAsync(objectKey, payload, true, cancellationToken)
            .ConfigureAwait(false);

        string checksumSha256 = Convert.ToHexString(SHA256.HashData(payload.ToArray()));

        DateTimeOffset updatedAtUtc = DateTimeOffset.UtcNow;

        await using TemplateFontMetadataDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TemplateFontMetadataRecord? metadataRecord = await dbContext.TemplateFonts
            .SingleOrDefaultAsync(
                record => record.TenantId == normalizedTenantId
                    && record.TemplateId == normalizedTemplateId
                    && record.FontKey == normalizedFontKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (metadataRecord is null)
        {
            metadataRecord = new TemplateFontMetadataRecord
            {
                Id = Guid.NewGuid(),
                TenantId = normalizedTenantId,
                TemplateId = normalizedTemplateId,
                FontKey = normalizedFontKey,
            };

            dbContext.TemplateFonts.Add(metadataRecord);
        }

        metadataRecord.ContentType = normalizedContentType;
        metadataRecord.ObjectKey = objectKey;
        metadataRecord.OriginalFileName = string.IsNullOrWhiteSpace(fileName) ? normalizedFontKey : fileName.Trim();
        metadataRecord.SizeBytes = payload.Length;
        metadataRecord.ChecksumSha256 = checksumSha256;
        metadataRecord.UpdatedAtUtc = updatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        TemplateFontUploadResponse result = new()
        {
            TenantId = normalizedTenantId,
            TemplateId = normalizedTemplateId,
            FontKey = normalizedFontKey,
            FontFamilyToken = $"{TenantFontPrefix}{normalizedFontKey}",
            ContentType = normalizedContentType,
            ObjectKey = objectKey,
            OriginalFileName = metadataRecord.OriginalFileName,
            SizeBytes = payload.Length,
            ChecksumSha256 = checksumSha256,
            UpdatedAtUtc = updatedAtUtc,
        };

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation(
                "Template font uploaded. TenantId={TenantId} TemplateId={TemplateId} FontKey={FontKey} ObjectKey={ObjectKey} SizeBytes={SizeBytes}",
                normalizedTenantId,
                normalizedTemplateId,
                normalizedFontKey,
                objectKey,
                payload.Length);
        }

        return result;
    }

    public async ValueTask<ErrorOr<TemplateFontListResponse>> ListAsync(
        string tenantId,
        string templateId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.TemplateFonts.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Error.Validation("Location.TemplateFonts.TemplateIdRequired", "TemplateId is required.");
        }

        string normalizedTenantId = tenantId.Trim();
        string normalizedTemplateId = templateId.Trim();

        await using TemplateFontMetadataDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TemplateFontItemResponse[] fontItems = await dbContext.TemplateFonts
            .Where(font => font.TenantId == normalizedTenantId && font.TemplateId == normalizedTemplateId)
            .OrderBy(font => font.FontKey)
            .Select(font => new TemplateFontItemResponse
            {
                FontKey = font.FontKey,
                FontFamilyToken = TenantFontPrefix + font.FontKey,
                ContentType = font.ContentType,
                ObjectKey = font.ObjectKey,
                OriginalFileName = font.OriginalFileName,
                SizeBytes = font.SizeBytes,
                ChecksumSha256 = font.ChecksumSha256,
                UpdatedAtUtc = font.UpdatedAtUtc,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        TemplateFontListResponse response = new()
        {
            TenantId = normalizedTenantId,
            TemplateId = normalizedTemplateId,
            Fonts = fontItems,
        };

        return response;
    }

    public async ValueTask<ErrorOr<Deleted>> DeleteAsync(
        string tenantId,
        string templateId,
        string fontKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Error.Validation("Location.TemplateFonts.TenantIdRequired", "Header 'X-TenantId' is required.");
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Error.Validation("Location.TemplateFonts.TemplateIdRequired", "TemplateId is required.");
        }

        if (!TryNormalizeFontKey(fontKey, out string normalizedFontKey))
        {
            return Error.Validation("Location.TemplateFonts.FontKeyInvalid", "FontKey is required and must not contain empty segments.");
        }

        string normalizedTenantId = tenantId.Trim();
        string normalizedTemplateId = templateId.Trim();

        await using TemplateFontMetadataDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TemplateFontMetadataRecord? metadataRecord = await dbContext.TemplateFonts
            .SingleOrDefaultAsync(
                font => font.TenantId == normalizedTenantId
                    && font.TemplateId == normalizedTemplateId
                    && font.FontKey == normalizedFontKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (metadataRecord is null)
        {
            return Error.NotFound(
                "Location.TemplateFonts.NotFound",
                $"No font mapping found for template '{normalizedTemplateId}' and key '{normalizedFontKey}'.");
        }

        string objectKey = metadataRecord.ObjectKey;

        dbContext.TemplateFonts.Remove(metadataRecord);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        bool sameFontStillReferenced = await dbContext.TemplateFonts
            .AnyAsync(
                font => font.TenantId == normalizedTenantId && font.FontKey == normalizedFontKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (!sameFontStillReferenced)
        {
            bool blobExists = await this.blobStorage.ExistsAsync(objectKey, cancellationToken).ConfigureAwait(false);
            if (blobExists)
            {
                await this.blobStorage.DeleteAsync(objectKey, cancellationToken).ConfigureAwait(false);
            }
        }

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation(
                "Template font deleted. TenantId={TenantId} TemplateId={TemplateId} FontKey={FontKey}",
                normalizedTenantId,
                normalizedTemplateId,
                normalizedFontKey);
        }

        return Result.Deleted;
    }

    private static IBlobStorage CreateBlobStorage(TemplateFontStorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return StorageFactory.Blobs.FromConnectionString(options.ConnectionString);
        }

        string localDirectory = string.IsNullOrWhiteSpace(options.LocalDirectory)
            ? Path.Combine(Path.GetTempPath(), "teck-cloud", "location", "template-fonts")
            : options.LocalDirectory;

        Directory.CreateDirectory(localDirectory);
        return StorageFactory.Blobs.DirectoryFiles(localDirectory);
    }

    private static bool TryNormalizeFontKey(string? fontKey, out string normalizedFontKey)
    {
        normalizedFontKey = string.Empty;

        if (string.IsNullOrWhiteSpace(fontKey))
        {
            return false;
        }

        string[] segments = fontKey
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || segments.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        normalizedFontKey = string.Join('/', segments);
        return true;
    }

    private static string BuildObjectKey(string template, string tenantId, string fontKey)
    {
        string tenantToken = Uri.EscapeDataString(tenantId.Trim());
        string fontToken = string.Join(
            '/',
            fontKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => Uri.EscapeDataString(segment.Trim())));

        string resolved = template
            .Replace("{tenantId}", tenantToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{fontKey}", fontToken, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException("Template font object key template resolved to an empty value.");
        }

        return resolved.TrimStart('/');
    }
}
