// <copyright file="ILocationTemplateContextRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Resolves inherited template context for a location node.
/// </summary>
public interface ILocationTemplateContextRunner
{
    /// <summary>
    /// Resolves template context for the provided location node.
    /// </summary>
    /// <param name="locationNodeId">The location node identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved template context snapshot.</returns>
    ValueTask<LocationTemplateContextSnapshot> ResolveTemplateContextAsync(string locationNodeId, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot of resolved template context for a location node.
/// </summary>
/// <param name="LocationNodeId">The requested location node identifier.</param>
/// <param name="ResolvedTemplateId">The resolved template identifier, if any.</param>
/// <param name="TemplateSource">The source of the resolved template.</param>
/// <param name="AncestorDepthScanned">Number of ancestor levels scanned while resolving inheritance.</param>
/// <param name="ResolvedTemplateDesign">The resolved template design snapshot, when available.</param>
public sealed record LocationTemplateContextSnapshot(
    string LocationNodeId,
    string? ResolvedTemplateId,
    string TemplateSource,
    int AncestorDepthScanned,
    ResolvedTemplateDesignSnapshot? ResolvedTemplateDesign = null);

/// <summary>
/// Snapshot of template design data used by assignment and rendering flows.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Name">The template display name.</param>
/// <param name="Width">The template width in pixels.</param>
/// <param name="Height">The template height in pixels.</param>
/// <param name="BackgroundColor">The template background color.</param>
/// <param name="ElementsJson">Serialized template element payload.</param>
/// <param name="DefaultsJson">Serialized template default values payload.</param>
public sealed record ResolvedTemplateDesignSnapshot(
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson);
