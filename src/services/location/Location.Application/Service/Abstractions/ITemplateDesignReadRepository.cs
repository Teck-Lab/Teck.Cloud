// <copyright file="ITemplateDesignReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ITemplateDesignReadRepository
{
    ValueTask<TemplateDesignSnapshot?> GetByTemplateIdAsync(string tenantId, string templateId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<TemplateDesignSnapshot>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken);
}

public sealed record TemplateDesignSnapshot(
    string TenantId,
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson);
