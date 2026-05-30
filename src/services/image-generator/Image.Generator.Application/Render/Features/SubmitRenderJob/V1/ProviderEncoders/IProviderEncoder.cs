// <copyright file="IProviderEncoder.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Encodes a rendered SKImage into a vendor-specific binary format.
/// </summary>
internal interface IProviderEncoder
{
    /// <summary>
    /// Gets vendor name this encoder supports.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Encodes the image to the vendor's preferred format and color constraints.
    /// </summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="profile">The provider profile with color/format constraints.</param>
    /// <returns>Encoded bytes ready for transmission to the ESL gateway.</returns>
    byte[] Encode(SKImage image, ProviderProfile profile);
}
