// <copyright file="ILabelRenderJobRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

public interface ILabelRenderJobRunner
{
    ValueTask<LabelRenderJobResult> EnqueueAsync(
        Guid jobId,
        Guid displayId,
        string layoutName,
        IReadOnlyCollection<LabelRenderJobZoneItem> zones,
        ResolvedTemplateDesignSnapshot? templateDesign,
        CancellationToken cancellationToken);
}

public sealed record LabelRenderJobZoneItem(int ZoneIndex, Guid ProductId);

public sealed record LabelRenderJobResult(Guid JobId, string Status);
