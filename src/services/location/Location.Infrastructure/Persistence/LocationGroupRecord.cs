// <copyright file="LocationGroupRecord.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Infrastructure.Persistence;

/// <summary>
/// A grouping of locations under a tenant for shared template defaults and permissions.
/// </summary>
internal sealed class LocationGroupRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string LocationGroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
