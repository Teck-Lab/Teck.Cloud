// <copyright file="DeviceDefinitionReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceDefinitions.ReadModels;

/// <summary>
/// EF Core read model for querying device definition rows.
/// Mapped by Infrastructure read configuration; not tenant-scoped.
/// </summary>
public sealed class DeviceDefinitionReadModel
{
    /// <summary>Gets or sets the device definition identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unique supplier model code (e.g. "HS-SE2130R").</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable model name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional screen width in pixels.</summary>
    public int? WidthPx { get; set; }

    /// <summary>Gets or sets the optional screen height in pixels.</summary>
    public int? HeightPx { get; set; }

    /// <summary>Gets or sets the supported ink colour bitmask stored as an integer.</summary>
    public int SupportedColors { get; set; }

    /// <summary>Gets or sets a value indicating whether this model supports NFC.</summary>
    public bool SupportsNfc { get; set; }

    /// <summary>Gets or sets the ESL provider stored as the SmartEnum name string.</summary>
    public string EslProvider { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional Catalog manufacturer identifier.</summary>
    public Guid? CatalogManufacturerId { get; set; }

    /// <summary>Gets or sets the optional Catalog supplier identifier.</summary>
    public Guid? CatalogSupplierId { get; set; }

    /// <summary>Gets or sets the optional Catalog product identifier.</summary>
    public Guid? CatalogProductId { get; set; }
}
