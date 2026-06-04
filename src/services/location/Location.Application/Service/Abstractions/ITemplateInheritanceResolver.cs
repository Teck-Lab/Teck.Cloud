// <copyright file="ITemplateInheritanceResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Resolves effective template settings by walking the inheritance chain:
/// Tenant → Location Group (optional) → Location → Location Ancestors.
/// </summary>
public interface ITemplateInheritanceResolver
{
    /// <summary>
    /// Resolves the effective template for a given location node.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationNodeId">The location node to resolve for.</param>
    /// <param name="explicitTemplateId">Optional explicitly requested template id (overrides resolved).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved template context with inheritance metadata.</returns>
    ValueTask<ResolvedTemplateContext> ResolveAsync(
        string tenantId,
        string locationNodeId,
        string? explicitTemplateId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The fully resolved template context including effective values and inheritance metadata.
/// </summary>
public sealed record ResolvedTemplateContext
{
    /// <summary>
    /// Gets the location node identifier used for resolution.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved template identifier.
    /// </summary>
    public string? ResolvedTemplateId { get; init; }

    /// <summary>
    /// Gets the source of the resolved template.
    /// </summary>
    public string TemplateSource { get; init; } = "None";

    /// <summary>
    /// Gets the resolved template design snapshot.
    /// </summary>
    public TemplateDesignSnapshot? ResolvedTemplateDesign { get; init; }

    /// <summary>
    /// Gets effective setting values by setting name.
    /// </summary>
    public IReadOnlyDictionary<string, EffectiveSettingValue> EffectiveSettings { get; init; }
        = new Dictionary<string, EffectiveSettingValue>();

    /// <summary>
    /// Gets the inheritance chain used for resolution.
    /// </summary>
    public IReadOnlyList<InheritanceSource> InheritanceChain { get; init; }
        = new List<InheritanceSource>();
}

/// <summary>
/// A single effective setting value with its source provenance.
/// </summary>
/// <param name="SettingName">The setting name.</param>
/// <param name="Value">The effective value.</param>
/// <param name="SourceScopeType">The source scope type.</param>
/// <param name="SourceScopeKey">The source scope key.</param>
/// <param name="Mode">The effective mode.</param>
public sealed record EffectiveSettingValue(
    string SettingName,
    object? Value,
    string SourceScopeType,
    string SourceScopeKey,
    string Mode);

/// <summary>
/// Describes one level in the inheritance chain.
/// </summary>
/// <param name="ScopeType">The scope type.</param>
/// <param name="ScopeKey">The scope key.</param>
/// <param name="ScopeName">The scope name.</param>
/// <param name="Settings">The settings at the scope.</param>
public sealed record InheritanceSource(
    string ScopeType,
    string ScopeKey,
    string ScopeName,
    IReadOnlyDictionary<string, ScopedSetting> Settings);

/// <summary>
/// A setting value within a specific scope.
/// </summary>
/// <param name="Mode">The setting mode.</param>
/// <param name="Value">The setting value.</param>
public sealed record ScopedSetting(
    string Mode,
    object? Value);
