// <copyright file="PreviewDeviceAssignmentResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;

public sealed record PreviewDeviceAssignmentResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string LocationNodeId { get; init; } = string.Empty;

    public string? ResolvedTemplateId { get; init; }

    public string TemplateSource { get; init; } = "Request";

    public int ZoneCount { get; init; }

    public bool DuplicateProductsAllowed { get; init; }
}
