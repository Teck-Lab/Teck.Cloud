// <copyright file="CatalogServiceVersionResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,AV2305,CA1515,CS1591
namespace Catalog.Api.Endpoints.V1.Service;

public sealed record CatalogServiceVersionResponse(string Service, string Version);
