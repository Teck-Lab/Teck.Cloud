// <copyright file="ILocationTemplateContextRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

public interface ILocationTemplateContextRunner
{
    ValueTask<LocationTemplateContextSnapshot> ResolveTemplateContextAsync(string locationNodeId, CancellationToken cancellationToken);
}

public sealed record LocationTemplateContextSnapshot(
    string LocationNodeId,
    string? ResolvedTemplateId,
    string TemplateSource,
    int AncestorDepthScanned,
    ResolvedTemplateDesignSnapshot? ResolvedTemplateDesign = null);

public sealed record ResolvedTemplateDesignSnapshot(
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson);
