// <copyright file="UpsertTemplateScopeSettingsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertTemplateScopeSettings.V1;

public sealed record UpsertTemplateScopeSettingsRequest
{
    public string ScopeType { get; init; } = string.Empty;

    public string ScopeKey { get; init; } = string.Empty;

    public string SettingsJson { get; init; } = "{}";
}
