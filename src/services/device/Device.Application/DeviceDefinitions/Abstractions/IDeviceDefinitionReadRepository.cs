// <copyright file="IDeviceDefinitionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Pagination;

namespace Device.Application.DeviceDefinitions.Abstractions;

/// <summary>
/// Read repository for device definitions.
/// </summary>
public interface IDeviceDefinitionReadRepository
{
    /// <summary>
    /// Gets a device definition snapshot by model ID.
    /// </summary>
    /// <param name="modelId">The unique supplier model code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot if found, otherwise null.</returns>
    ValueTask<DeviceDefinitionSnapshot?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a device definition snapshot by its primary key.
    /// </summary>
    /// <param name="id">The device definition identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot if found, otherwise null.</returns>
    ValueTask<DeviceDefinitionSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a paginated list of device definitions.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="size">Page size.</param>
    /// <param name="sortBy">Column to sort by: modelId, name, eslProvider.</param>
    /// <param name="sortDescending">Sort direction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of device definition snapshots.</returns>
    Task<PagedList<DeviceDefinitionSnapshot>> GetPagedAsync(int page, int size, string? sortBy, bool sortDescending, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot of a device definition used across read queries.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="ModelId">Unique supplier model code.</param>
/// <param name="Name">Human-readable model name.</param>
/// <param name="WidthPx">Screen width in pixels, if known.</param>
/// <param name="HeightPx">Screen height in pixels, if known.</param>
/// <param name="SupportedColors">Supported ink colour bitmask.</param>
/// <param name="SupportsNfc">Whether this model supports NFC.</param>
/// <param name="EslProvider">ESL vendor integration driver.</param>
/// <param name="CatalogManufacturerId">Optional soft-link to Catalog manufacturer.</param>
/// <param name="CatalogSupplierId">Optional soft-link to Catalog supplier.</param>
/// <param name="CatalogProductId">Optional soft-link to Catalog product.</param>
public sealed record DeviceDefinitionSnapshot(
    Guid Id,
    string ModelId,
    string Name,
    int? WidthPx,
    int? HeightPx,
    DisplayInkColor SupportedColors,
    bool SupportsNfc,
    EslProvider EslProvider,
    Guid? CatalogManufacturerId,
    Guid? CatalogSupplierId,
    Guid? CatalogProductId);
