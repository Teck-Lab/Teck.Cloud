// <copyright file="GetProductsByCategoryRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Catalog.Api.Endpoints.V1.Products;

/// <summary>
/// Request for fetching products by category.
/// </summary>
/// <param name="CategoryId">Category identifier.</param>
public sealed record GetProductsByCategoryRequest(Guid CategoryId);
