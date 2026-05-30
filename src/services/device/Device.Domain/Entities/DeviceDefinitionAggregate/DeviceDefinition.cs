// <copyright file="DeviceDefinition.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Domain;

namespace Device.Domain.Entities.DeviceDefinitionAggregate;

/// <summary>
/// Represents a hardware model definition for an ESL display.
/// This is a global (non-tenant-scoped) aggregate root.
/// </summary>
public sealed class DeviceDefinition : BaseEntity, IAggregateRoot
{
    private DeviceDefinition()
    {
    }

    /// <summary>
    /// Gets the unique supplier model code (e.g. "HS-SE2130R").
    /// Must be unique across all definitions.
    /// </summary>
    public string ModelId { get; private set; } = default!;

    /// <summary>
    /// Gets the human-readable display name for this model.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the physical screen width in pixels, if known.
    /// </summary>
    public int? WidthPx { get; private set; }

    /// <summary>
    /// Gets the physical screen height in pixels, if known.
    /// </summary>
    public int? HeightPx { get; private set; }

    /// <summary>
    /// Gets the ink colours supported by this panel as a bitmask.
    /// </summary>
    public DisplayInkColor SupportedColors { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this model supports NFC.
    /// </summary>
    public bool SupportsNfc { get; private set; }

    /// <summary>
    /// Gets the ESL vendor integration driver for this model.
    /// </summary>
    public EslProvider EslProvider { get; private set; } = EslProvider.Unknown;

    /// <summary>
    /// Gets the optional soft-link to the manufacturer entity in the Catalog service.
    /// </summary>
    public Guid? CatalogManufacturerId { get; private set; }

    /// <summary>
    /// Gets the optional soft-link to the supplier entity in the Catalog service.
    /// </summary>
    public Guid? CatalogSupplierId { get; private set; }

    /// <summary>
    /// Gets the optional soft-link to the specific product in the Catalog service.
    /// </summary>
    public Guid? CatalogProductId { get; private set; }

    /// <summary>
    /// Registers a new device definition hardware model.
    /// </summary>
    /// <param name="modelId">Unique supplier model code.</param>
    /// <param name="name">Human-readable model name.</param>
    /// <param name="eslProvider">ESL vendor integration driver.</param>
    /// <param name="supportedColors">Supported ink colour bitmask.</param>
    /// <param name="supportsNfc">Whether the model supports NFC.</param>
    /// <param name="widthPx">Optional screen width in pixels.</param>
    /// <param name="heightPx">Optional screen height in pixels.</param>
    /// <param name="catalogManufacturerId">Optional Catalog manufacturer soft-link.</param>
    /// <param name="catalogSupplierId">Optional Catalog supplier soft-link.</param>
    /// <param name="catalogProductId">Optional Catalog product soft-link.</param>
    /// <returns>The created <see cref="DeviceDefinition"/> or validation errors.</returns>
    public static ErrorOr<DeviceDefinition> Create(
        string modelId,
        string name,
        EslProvider eslProvider,
        DisplayInkColor supportedColors,
        bool supportsNfc,
        int? widthPx,
        int? heightPx,
        Guid? catalogManufacturerId,
        Guid? catalogSupplierId,
        Guid? catalogProductId)
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(modelId))
        {
            errors.Add(Error.Validation("DeviceDefinition.ModelIdRequired", "Model ID is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(Error.Validation("DeviceDefinition.NameRequired", "Name is required."));
        }

        if (widthPx.HasValue && widthPx.Value <= 0)
        {
            errors.Add(Error.Validation("DeviceDefinition.InvalidWidthPx", "Screen width must be a positive integer."));
        }

        if (heightPx.HasValue && heightPx.Value <= 0)
        {
            errors.Add(Error.Validation("DeviceDefinition.InvalidHeightPx", "Screen height must be a positive integer."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        DeviceDefinition definition = new()
        {
            ModelId = modelId.Trim(),
            Name = name.Trim(),
            EslProvider = eslProvider,
            SupportedColors = supportedColors,
            SupportsNfc = supportsNfc,
            WidthPx = widthPx,
            HeightPx = heightPx,
            CatalogManufacturerId = catalogManufacturerId,
            CatalogSupplierId = catalogSupplierId,
            CatalogProductId = catalogProductId,
        };

        definition.AddDomainEvent(new DeviceDefinitionCreatedEvent(definition.Id, definition.ModelId));

        return definition;
    }

    /// <summary>
    /// Updates the optional Catalog soft-links for this device definition.
    /// </summary>
    /// <param name="catalogManufacturerId">New Catalog manufacturer ID, or null to clear.</param>
    /// <param name="catalogSupplierId">New Catalog supplier ID, or null to clear.</param>
    /// <param name="catalogProductId">New Catalog product ID, or null to clear.</param>
    public void UpdateCatalogLinks(
        Guid? catalogManufacturerId,
        Guid? catalogSupplierId,
        Guid? catalogProductId)
    {
        CatalogManufacturerId = catalogManufacturerId;
        CatalogSupplierId = catalogSupplierId;
        CatalogProductId = catalogProductId;
    }
}
