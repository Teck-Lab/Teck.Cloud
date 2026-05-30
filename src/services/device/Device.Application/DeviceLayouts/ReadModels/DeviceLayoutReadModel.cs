// <copyright file="DeviceLayoutReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceLayouts.ReadModels;

/// <summary>
/// EF Core read model for querying device layout rows.
/// Mapped by Infrastructure read configuration; not tenant-scoped.
/// </summary>
public sealed class DeviceLayoutReadModel
{
    /// <summary>Gets or sets the device layout identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the parent device definition identifier.</summary>
    public Guid DeviceDefinitionId { get; set; }

    /// <summary>Gets or sets the layout name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum number of zones supported by this layout.</summary>
    public int MaxZoneCount { get; set; }
}
