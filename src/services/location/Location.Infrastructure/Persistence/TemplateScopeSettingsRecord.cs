// <copyright file="TemplateScopeSettingsRecord.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Infrastructure.Persistence;

/// <summary>
/// Per-scope inheritance settings for a tenant, location group, or location.
/// </summary>
internal sealed class TemplateScopeSettingsRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope type. One of: Tenant, LocationGroup, Location.
    /// </summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope key. For Tenant scope: use "_tenant". For LocationGroup: group id. For Location: location node id.
    /// </summary>
    public string ScopeKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON object where each key is a setting name and value is {"mode":"inherit|override|ignore","value":...}.
    /// </summary>
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
