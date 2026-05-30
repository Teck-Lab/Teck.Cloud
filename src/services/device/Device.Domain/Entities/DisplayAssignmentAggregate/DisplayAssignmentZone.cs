// <copyright file="DisplayAssignmentZone.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.Entities.DisplayAssignmentAggregate;

/// <summary>
/// A single zone-to-product binding inside a <see cref="DisplayAssignment"/>.
/// Modeled as an owned value object on the aggregate root.
/// </summary>
public sealed class DisplayAssignmentZone
{
    private DisplayAssignmentZone()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentZone"/> class.
    /// </summary>
    /// <param name="zoneIndex">The zone index on the display layout.</param>
    /// <param name="productId">The product bound to this zone.</param>
    public DisplayAssignmentZone(int zoneIndex, Guid productId)
    {
        ZoneIndex = zoneIndex;
        ProductId = productId;
    }

    /// <summary>
    /// Gets the zone index on the display layout.
    /// </summary>
    public int ZoneIndex { get; private set; }

    /// <summary>
    /// Gets the product bound to this zone.
    /// </summary>
    public Guid ProductId { get; private set; }
}
