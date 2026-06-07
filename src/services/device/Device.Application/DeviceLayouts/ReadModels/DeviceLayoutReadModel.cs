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

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the creator identifier.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the last update timestamp.</summary>
    public DateTimeOffset? UpdatedOn { get; set; }

    /// <summary>Gets or sets the last updater identifier.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Gets or sets the soft-delete timestamp.</summary>
    public DateTimeOffset? DeletedOn { get; set; }

    /// <summary>Gets or sets the soft-delete user identifier.</summary>
    public string? DeletedBy { get; set; }

    /// <summary>Gets or sets a value indicating whether this record is soft-deleted.</summary>
    public bool IsDeleted { get; set; }
}
