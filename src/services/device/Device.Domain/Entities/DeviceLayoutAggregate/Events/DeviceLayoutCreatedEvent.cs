// <copyright file="DeviceLayoutCreatedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DeviceLayoutAggregate.Events;

/// <summary>
/// Raised when a new <see cref="DeviceLayout"/> is created.
/// </summary>
public sealed class DeviceLayoutCreatedEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceLayoutCreatedEvent"/> class.
    /// </summary>
    /// <param name="deviceLayoutId">The identifier of the created layout.</param>
    /// <param name="deviceDefinitionId">The device definition this layout belongs to.</param>
    /// <param name="name">The layout name.</param>
    public DeviceLayoutCreatedEvent(Guid deviceLayoutId, Guid deviceDefinitionId, string name)
    {
        DeviceLayoutId = deviceLayoutId;
        DeviceDefinitionId = deviceDefinitionId;
        Name = name;
    }

    /// <summary>Gets the identifier of the created layout.</summary>
    public Guid DeviceLayoutId { get; }

    /// <summary>Gets the device definition this layout belongs to.</summary>
    public Guid DeviceDefinitionId { get; }

    /// <summary>Gets the layout name.</summary>
    public string Name { get; }
}
