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
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Uri? LogoUrl { get; set; }

    public Uri? WebsiteUrl { get; set; }
}
