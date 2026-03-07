// <copyright file="GetByIdBrandResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Brands.Features.GetBrandById.V1;

/// <summary>
/// Response DTO for get brand by id feature.
/// </summary>
[Serializable]
public class GetByIdBrandResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Uri? LogoUrl { get; set; }

    public Uri? WebsiteUrl { get; set; }
}
