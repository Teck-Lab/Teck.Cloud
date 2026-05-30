// <copyright file="Display.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.Domain;

namespace Device.Domain.Entities.DisplayAggregate;

/// <summary>
/// Represents a Hanshow ESL display registered to a tenant.
/// </summary>
public sealed class Display : BaseEntity, IAggregateRoot
{
    private Display()
    {
    }

    /// <summary>
    /// Gets the short (4-byte) serial number in <c>XX-XX-XX-XX</c> format.
    /// </summary>
    public string ShortSerial { get; private set; } = default!;

    /// <summary>
    /// Gets the long (decimal) serial number, populated after the first Hanshow heartbeat.
    /// </summary>
    public long? LongSerial { get; private set; }

    /// <summary>
    /// Gets the location node this display is assigned to.
    /// </summary>
    public string LocationNodeId { get; private set; } = default!;

    /// <summary>
    /// Gets the optional device definition (model) identifier.
    /// </summary>
    public Guid? DeviceDefinitionId { get; private set; }

    /// <summary>
    /// Gets the optional device layout identifier.
    /// </summary>
    public Guid? DeviceLayoutId { get; private set; }

    /// <summary>
    /// Gets the identifier of the <see cref="DisplayAssignmentAggregate.DisplayAssignment"/> currently bound to this display, if any.
    /// </summary>
    public Guid? CurrentAssignmentId { get; private set; }

    /// <summary>
    /// Creates a new <see cref="Display"/> from a manually entered short serial.
    /// </summary>
    /// <param name="shortSerial">Short serial in <c>XX-XX-XX-XX</c> format.</param>
    /// <param name="locationNodeId">Location node the display belongs to.</param>
    /// <param name="deviceDefinitionId">Optional device definition identifier.</param>
    /// <param name="deviceLayoutId">Optional device layout identifier.</param>
    /// <returns>The created display or validation errors.</returns>
    public static ErrorOr<Display> Create(
        string shortSerial,
        string locationNodeId,
        Guid? deviceDefinitionId,
        Guid? deviceLayoutId = null)
    {
        if (string.IsNullOrWhiteSpace(shortSerial))
        {
            return Error.Validation("Display.ShortSerialRequired", "Short serial is required.");
        }

        if (string.IsNullOrWhiteSpace(locationNodeId))
        {
            return Error.Validation("Display.LocationNodeIdRequired", "Location node ID is required.");
        }

        Display display = new()
        {
            ShortSerial = shortSerial.Trim().ToUpperInvariant(),
            LocationNodeId = locationNodeId.Trim(),
            DeviceDefinitionId = deviceDefinitionId,
            DeviceLayoutId = deviceLayoutId,
        };

        return display;
    }

    /// <summary>
    /// Associates a long serial number received from a Hanshow heartbeat.
    /// </summary>
    /// <param name="longSerial">The decimal serial number from the heartbeat.</param>
    public void SetLongSerial(long longSerial) => LongSerial = longSerial;

    /// <summary>
    /// Binds this display to the given assignment as its currently active assignment.
    /// </summary>
    /// <param name="assignmentId">The assignment now active on this display.</param>
    /// <returns>Success or a validation error.</returns>
    public ErrorOr<Success> AssignCurrent(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
        {
            return Error.Validation("Display.AssignmentIdRequired", "AssignmentId is required.");
        }

        CurrentAssignmentId = assignmentId;
        return Result.Success;
    }

    /// <summary>
    /// Clears the currently bound assignment from this display.
    /// </summary>
    public void ClearCurrentAssignment() => CurrentAssignmentId = null;
}
