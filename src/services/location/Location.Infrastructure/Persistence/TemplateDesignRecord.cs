// <copyright file="TemplateDesignRecord.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Infrastructure.Persistence;

/// <summary>
/// A reusable label template design owned by a tenant.
/// </summary>
internal sealed class TemplateDesignRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON array of template elements.
    /// </summary>
    public string ElementsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON object of default values for bindings (e.g., fontFamily, foregroundColor).
    /// </summary>
    public string DefaultsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
