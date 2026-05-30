// <copyright file="DeviceDefinitionCreatedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DeviceDefinitionAggregate.Events;

/// <summary>
/// Raised when a new <see cref="DeviceDefinition"/> is registered.
/// </summary>
public sealed class DeviceDefinitionCreatedEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceDefinitionCreatedEvent"/> class.
    /// </summary>
    /// <param name="deviceDefinitionId">The identifier of the created device definition.</param>
    /// <param name="modelId">The unique supplier model code.</param>
    public DeviceDefinitionCreatedEvent(Guid deviceDefinitionId, string modelId)
    {
        DeviceDefinitionId = deviceDefinitionId;
        ModelId = modelId;
    }

    /// <summary>
    /// Gets the identifier of the created device definition.
    /// </summary>
    public Guid DeviceDefinitionId { get; }

    /// <summary>
    /// Gets the unique supplier model code.
    /// </summary>
    public string ModelId { get; }
}
