// <copyright file="ITemplateDesignWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for write operations on template designs.
/// </summary>
public interface ITemplateDesignWriteRepository
{
    /// <summary>
    /// Creates or updates a template design snapshot.
    /// </summary>
    /// <param name="snapshot">The template design snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask UpsertAsync(TemplateDesignSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a template design.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask DeleteAsync(string tenantId, string templateId, CancellationToken cancellationToken);
}
