// <copyright file="GetDeviceDefinitionsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceDefinitions.Features.GetDeviceDefinitions.V1;

/// <summary>
/// A single device definition item in a paged list response.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="ModelId">Unique supplier model code.</param>
/// <param name="Name">Human-readable model name.</param>
/// <param name="WidthPx">Screen width in pixels, if known.</param>
/// <param name="HeightPx">Screen height in pixels, if known.</param>
/// <param name="SupportedColors">Supported ink colour bitmask value.</param>
/// <param name="SupportsNfc">Whether this model supports NFC.</param>
/// <param name="EslProvider">ESL vendor integration driver name.</param>
/// <param name="CatalogManufacturerId">Optional soft-link to Catalog manufacturer.</param>
/// <param name="CatalogSupplierId">Optional soft-link to Catalog supplier.</param>
/// <param name="CatalogProductId">Optional soft-link to Catalog product.</param>
public sealed record GetDeviceDefinitionItemResponse(
    Guid Id,
    string ModelId,
    string Name,
    int? WidthPx,
    int? HeightPx,
    int SupportedColors,
    bool SupportsNfc,
    string EslProvider,
    Guid? CatalogManufacturerId,
    Guid? CatalogSupplierId,
    Guid? CatalogProductId);
