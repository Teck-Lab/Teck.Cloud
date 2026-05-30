// <copyright file="ITenantFontAssetStore.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Defines tenant font asset resolution and local availability operations.
/// </summary>
public interface ITenantFontAssetStore
{
    /// <summary>
    /// Ensures tenant-scoped font assets are available locally for rendering.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="fontFamilies">The requested font families.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A mapping from resolved tenant font keys to local file paths.
    /// </returns>
    ValueTask<IReadOnlyDictionary<string, string>> EnsureFontsAvailableAsync(
        string tenantId,
        IReadOnlyCollection<string> fontFamilies,
        CancellationToken cancellationToken);
}
