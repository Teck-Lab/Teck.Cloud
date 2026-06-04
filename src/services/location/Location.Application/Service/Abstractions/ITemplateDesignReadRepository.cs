// <copyright file="ITemplateDesignReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for read operations on template designs.
/// </summary>
public interface ITemplateDesignReadRepository
{
    /// <summary>
    /// Gets a template design by template identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template design snapshot when found; otherwise <see langword="null"/>.</returns>
    ValueTask<TemplateDesignSnapshot?> GetByTemplateIdAsync(string tenantId, string templateId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists template designs for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of template design snapshots.</returns>
    ValueTask<IReadOnlyList<TemplateDesignSnapshot>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot model for a template design.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Name">The template name.</param>
/// <param name="Width">The template width.</param>
/// <param name="Height">The template height.</param>
/// <param name="BackgroundColor">The template background color.</param>
/// <param name="ElementsJson">The serialized element definitions.</param>
/// <param name="DefaultsJson">The serialized default values.</param>
public sealed record TemplateDesignSnapshot(
    string TenantId,
    string TemplateId,
    string Name,
    int Width,
    int Height,
    string BackgroundColor,
    string ElementsJson,
    string DefaultsJson);
