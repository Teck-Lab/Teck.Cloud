// <copyright file="GetProductsByBrandRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.GetProductsByBrand.V1;

/// <summary>
/// Request for fetching products by brand.
/// </summary>
/// <param name="BrandId">Brand identifier.</param>
public sealed record GetProductsByBrandRequest(Guid BrandId);
