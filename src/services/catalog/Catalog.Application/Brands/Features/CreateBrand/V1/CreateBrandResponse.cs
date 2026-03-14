// <copyright file="CreateBrandResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Brands.Features.CreateBrand.V1;

/// <summary>
/// Response DTO for create brand feature.
/// </summary>
[Serializable]
public class CreateBrandResponse
{
    /// <summary>
    /// Gets or sets the brand identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the brand name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the brand description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the brand logo URL.
    /// </summary>
    public Uri? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the brand website URL.
    /// </summary>
    public Uri? WebsiteUrl { get; set; }
}
