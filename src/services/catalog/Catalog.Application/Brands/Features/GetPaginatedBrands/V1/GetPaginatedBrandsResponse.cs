// <copyright file="GetPaginatedBrandsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1;

/// <summary>
/// Response DTO for get paginated brands feature.
/// </summary>
[Serializable]
public class GetPaginatedBrandsResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Uri? LogoUrl { get; set; }

    public Uri? WebsiteUrl { get; set; }
}
