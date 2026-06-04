// <copyright file="ApplyDeviceAssignmentResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;

/// <summary>
/// Response payload for an applied device assignment.
/// </summary>
public sealed record ApplyDeviceAssignmentResponse
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
    /// Gets the resolved template identifier.
    /// </summary>
    public string ResolvedTemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source used to resolve the template.
    /// </summary>
    public string TemplateSource { get; init; } = "Request";

    /// <summary>
    /// Gets the number of assigned zones.
    /// </summary>
    public int ZoneCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether duplicate products are allowed.
    /// </summary>
    public bool DuplicateProductsAllowed { get; init; }

    /// <summary>
    /// Gets the render job identifier.
    /// </summary>
    public Guid RenderJobId { get; init; }

    /// <summary>
    /// Gets the current render job status.
    /// </summary>
    public string RenderJobStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets the created assignment identifier.
    /// </summary>
    public Guid AssignmentId { get; init; }

    /// <summary>
    /// Gets the assignment version.
    /// </summary>
    public int AssignmentVersion { get; init; }
}
