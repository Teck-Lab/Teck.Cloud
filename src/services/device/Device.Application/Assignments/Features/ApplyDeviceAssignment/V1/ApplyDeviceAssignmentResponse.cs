// <copyright file="ApplyDeviceAssignmentResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;

public sealed record ApplyDeviceAssignmentResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string LocationNodeId { get; init; } = string.Empty;

    public string ResolvedTemplateId { get; init; } = string.Empty;

    public string TemplateSource { get; init; } = "Request";

    public int ZoneCount { get; init; }

    public bool DuplicateProductsAllowed { get; init; }

    public Guid RenderJobId { get; init; }

    public string RenderJobStatus { get; init; } = string.Empty;

    public Guid AssignmentId { get; init; }

    public int AssignmentVersion { get; init; }
}
