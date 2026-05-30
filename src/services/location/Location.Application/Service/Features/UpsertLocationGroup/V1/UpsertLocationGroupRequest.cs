// <copyright file="UpsertLocationGroupRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertLocationGroup.V1;

public sealed record UpsertLocationGroupRequest
{
    public string LocationGroupId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
