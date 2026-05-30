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
    public string LocationNodeId { get; init; } = string.Empty;

    public string? ResolvedTemplateId { get; init; }

    public string TemplateSource { get; init; } = "None";

    public TemplateDesignSnapshot? ResolvedTemplateDesign { get; init; }

    public IReadOnlyDictionary<string, EffectiveSettingValue> EffectiveSettings { get; init; }
        = new Dictionary<string, EffectiveSettingValue>();

    public IReadOnlyList<InheritanceSource> InheritanceChain { get; init; }
        = new List<InheritanceSource>();
}

/// <summary>
/// A single effective setting value with its source provenance.
/// </summary>
public sealed record EffectiveSettingValue(
    string SettingName,
    object? Value,
    string SourceScopeType,
    string SourceScopeKey,
    string Mode);

/// <summary>
/// Describes one level in the inheritance chain.
/// </summary>
public sealed record InheritanceSource(
    string ScopeType,
    string ScopeKey,
    string ScopeName,
    IReadOnlyDictionary<string, ScopedSetting> Settings);

/// <summary>
/// A setting value within a specific scope.
/// </summary>
public sealed record ScopedSetting(
    string Mode,
    object? Value);
