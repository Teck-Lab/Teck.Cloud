// <copyright file="GetProductsByBrandRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Catalog.Api.Endpoints.V1.Products;

/// <summary>
/// Request for fetching products by brand.
/// </summary>
/// <param name="BrandId">Brand identifier.</param>
public sealed record GetProductsByBrandRequest(Guid BrandId);
