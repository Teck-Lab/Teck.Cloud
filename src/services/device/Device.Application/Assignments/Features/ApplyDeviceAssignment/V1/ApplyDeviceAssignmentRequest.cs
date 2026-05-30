// <copyright file="ApplyDeviceAssignmentRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;

public sealed record ApplyDeviceAssignmentRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public string LocationNodeId { get; init; } = string.Empty;

    public string? TemplateId { get; init; }

    public IReadOnlyList<ApplyDeviceAssignmentZoneRequest> Zones { get; init; } = [];
}

public sealed record ApplyDeviceAssignmentZoneRequest
{
    public int ZoneIndex { get; init; }

    public string ProductId { get; init; } = string.Empty;
}
