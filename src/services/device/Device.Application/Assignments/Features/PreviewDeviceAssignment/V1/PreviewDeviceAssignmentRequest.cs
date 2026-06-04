// <copyright file="PreviewDeviceAssignmentRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;

/// <summary>
/// API request for previewing a device assignment.
/// </summary>
public sealed record PreviewDeviceAssignmentRequest
{
    /// <summary>
    /// Gets the target device identifier.
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location node identifier.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional explicit template identifier.
    /// </summary>
    public string? TemplateId { get; init; }

    /// <summary>
    /// Gets the requested assignment zones.
    /// </summary>
    public IReadOnlyList<PreviewDeviceAssignmentZoneRequest> Zones { get; init; } = [];
}

/// <summary>
/// Zone assignment preview request item.
/// </summary>
public sealed record PreviewDeviceAssignmentZoneRequest
{
    /// <summary>
    /// Gets the zone index within the target layout.
    /// </summary>
    public int ZoneIndex { get; init; }

    /// <summary>
    /// Gets the product identifier to bind to the zone.
    /// </summary>
    public string ProductId { get; init; } = string.Empty;
}
