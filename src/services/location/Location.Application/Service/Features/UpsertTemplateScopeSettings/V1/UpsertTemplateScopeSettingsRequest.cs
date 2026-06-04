// <copyright file="UpsertTemplateScopeSettingsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertTemplateScopeSettings.V1;

/// <summary>
/// Request payload for creating or updating template scope settings.
/// </summary>
public sealed record UpsertTemplateScopeSettingsRequest
{
    /// <summary>
    /// Gets the scope type.
    /// </summary>
    public string ScopeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the scope key.
    /// </summary>
    public string ScopeKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the serialized settings payload.
    /// </summary>
    public string SettingsJson { get; init; } = "{}";
}
