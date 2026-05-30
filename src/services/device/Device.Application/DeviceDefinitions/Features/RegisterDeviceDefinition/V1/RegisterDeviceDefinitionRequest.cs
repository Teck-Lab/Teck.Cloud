// <copyright file="RegisterDeviceDefinitionRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;

/// <summary>
/// HTTP request body for registering a device definition.
/// </summary>
public sealed class RegisterDeviceDefinitionRequest
{
    /// <summary>Gets or sets the unique supplier model code (e.g. "HS-SE2130R").</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable model name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ESL vendor integration driver name.</summary>
    public string EslProvider { get; set; } = string.Empty;

    /// <summary>Gets or sets the supported ink colour bitmask value.</summary>
    public int SupportedColors { get; set; }

    /// <summary>Gets or sets a value indicating whether this model supports NFC.</summary>
    public bool SupportsNfc { get; set; }

    /// <summary>Gets or sets the optional screen width in pixels.</summary>
    public int? WidthPx { get; set; }

    /// <summary>Gets or sets the optional screen height in pixels.</summary>
    public int? HeightPx { get; set; }

    /// <summary>Gets or sets the optional Catalog manufacturer ID.</summary>
    public Guid? CatalogManufacturerId { get; set; }

    /// <summary>Gets or sets the optional Catalog supplier ID.</summary>
    public Guid? CatalogSupplierId { get; set; }

    /// <summary>Gets or sets the optional Catalog product ID.</summary>
    public Guid? CatalogProductId { get; set; }
}
