// <copyright file="FluentImageStorage.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentStorage.Blobs;

namespace Image.Generator.Application.Storage;

/// <summary>
/// Implements <see cref="IImageStorage"/> using FluentStorage with S3/MinIO backend.
/// </summary>
internal sealed class FluentImageStorage : IImageStorage
{
    private readonly IBlobStorage _blobStorage;
    private readonly string _baseUri;

    public FluentImageStorage(IBlobStorage blobStorage, string baseUri)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
    }

    public async ValueTask<Uri> SaveAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        string fullPath = $"images/{path}";

        await _blobStorage.WriteAsync(fullPath, content, false, cancellationToken).ConfigureAwait(false);

        string uriString = _baseUri.EndsWith('/')
            ? $"{_baseUri}{fullPath}"
            : $"{_baseUri}/{fullPath}";

        return new Uri(uriString);
    }

    public async ValueTask<Stream> GetAsync(Uri imageUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageUri);

        string path = GetBlobPath(imageUri);
        return await _blobStorage.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(Uri imageUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageUri);

        string path = GetBlobPath(imageUri);
        await _blobStorage.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private string GetBlobPath(Uri imageUri)
    {
        string basePath = _baseUri.EndsWith('/')
            ? _baseUri
            : $"{_baseUri}/";

        string uriString = imageUri.ToString();

        if (uriString.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            return uriString[basePath.Length..];
        }

        // Fallback: assume path after last segment
        return $"images/{imageUri.Segments[^1].Trim('/')}";
    }
}
