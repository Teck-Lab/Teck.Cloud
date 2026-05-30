// <copyright file="IImageStorage.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Storage;

/// <summary>
/// Abstraction for persisting rendered label images to object storage.
/// </summary>
public interface IImageStorage
{
    /// <summary>
    /// Saves an image stream to storage and returns the public URI.
    /// </summary>
    /// <param name="path">The storage path (e.g. "tenantId/assignments/assignmentId.png").</param>
    /// <param name="content">The image content stream.</param>
    /// <param name="contentType">The MIME type (e.g. "image/png").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URI of the saved image.</returns>
    ValueTask<Uri> SaveAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads an image from storage by URI.
    /// </summary>
    /// <param name="imageUri">The URI returned by <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The image content stream.</returns>
    ValueTask<Stream> GetAsync(Uri imageUri, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an image from storage.
    /// </summary>
    /// <param name="imageUri">The URI returned by <see cref="SaveAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteAsync(Uri imageUri, CancellationToken cancellationToken);
}
