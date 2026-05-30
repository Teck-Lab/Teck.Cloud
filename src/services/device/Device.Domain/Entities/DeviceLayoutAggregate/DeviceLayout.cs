// <copyright file="DeviceLayout.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Domain;

namespace Device.Domain.Entities.DeviceLayoutAggregate;

/// <summary>
/// Represents a TeckLab-defined zone configuration for a device model.
/// Multiple layouts can be created for one device definition,
/// enabling different template/zone arrangements on the same hardware.
/// This is a global (non-tenant-scoped) aggregate root.
/// </summary>
public sealed class DeviceLayout : BaseEntity, IAggregateRoot
{
    private DeviceLayout()
    {
    }

    /// <summary>
    /// Gets the device definition this layout is configured for.
    /// </summary>
    public Guid DeviceDefinitionId { get; private set; }

    /// <summary>
    /// Gets the human-readable layout name (e.g. "3-zone standard").
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the maximum number of content zones this layout supports.
    /// </summary>
    public int MaxZoneCount { get; private set; }

    /// <summary>
    /// Creates a new device layout for a given device definition.
    /// </summary>
    /// <param name="deviceDefinitionId">The device definition this layout belongs to.</param>
    /// <param name="name">Human-readable layout name.</param>
    /// <param name="maxZoneCount">Maximum number of content zones.</param>
    /// <returns>The created <see cref="DeviceLayout"/> or validation errors.</returns>
    public static ErrorOr<DeviceLayout> Create(
        Guid deviceDefinitionId,
        string name,
        int maxZoneCount)
    {
        List<Error> errors = [];

        if (deviceDefinitionId == Guid.Empty)
        {
            errors.Add(Error.Validation("DeviceLayout.DeviceDefinitionIdRequired", "Device definition ID is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(Error.Validation("DeviceLayout.NameRequired", "Layout name is required."));
        }

        if (maxZoneCount <= 0)
        {
            errors.Add(Error.Validation("DeviceLayout.InvalidMaxZoneCount", "Max zone count must be greater than zero."));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        DeviceLayout layout = new()
        {
            DeviceDefinitionId = deviceDefinitionId,
            Name = name.Trim(),
            MaxZoneCount = maxZoneCount,
        };

        layout.AddDomainEvent(new DeviceLayoutCreatedEvent(layout.Id, layout.DeviceDefinitionId, layout.Name));

        return layout;
    }
}
