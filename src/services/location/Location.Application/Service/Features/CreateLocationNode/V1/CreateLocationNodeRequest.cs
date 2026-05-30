// <copyright file="CreateLocationNodeRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.CreateLocationNode.V1;

public sealed record CreateLocationNodeRequest
{
    public string Name { get; init; } = string.Empty;

    public string? ParentLocationNodeId { get; init; }
}
