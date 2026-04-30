// <copyright file="GetProductsByCategoryRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Products.Features.GetProductsByCategory.V1;

/// <summary>
/// Request for fetching products by category.
/// </summary>
/// <param name="CategoryId">Category identifier.</param>
public sealed record GetProductsByCategoryRequest(Guid CategoryId);
