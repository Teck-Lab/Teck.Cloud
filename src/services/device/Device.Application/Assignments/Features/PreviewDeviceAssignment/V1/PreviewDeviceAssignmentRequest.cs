// <copyright file="PreviewDeviceAssignmentRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;

public sealed record PreviewDeviceAssignmentRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public string LocationNodeId { get; init; } = string.Empty;

    public string? TemplateId { get; init; }

    public IReadOnlyList<PreviewDeviceAssignmentZoneRequest> Zones { get; init; } = [];
}

public sealed record PreviewDeviceAssignmentZoneRequest
{
    public int ZoneIndex { get; init; }

    public string ProductId { get; init; } = string.Empty;
}
