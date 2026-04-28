// <copyright file="ITenantReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using SharedKernel.Core.Pagination;

namespace Customer.Application.Tenants.Repositories;

/// <summary>
/// Repository interface for Tenant read operations.
/// </summary>
public interface ITenantReadRepository
{
    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="id">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant read model if found; otherwise, null.</returns>
    Task<TenantReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets tenant database info for gRPC consumers.
    /// </summary>
    /// <param name="id">The tenant ID.</param>
    /// <param name="serviceName">The optional service name used to resolve replica settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant database info if found; otherwise, null.</returns>
    Task<TenantDatabaseInfoReadModel?> GetDatabaseInfoByIdAsync(Guid id, string? serviceName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets paginated tenants with optional filtering.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="size">Page size.</param>
    /// <param name="keyword">Optional keyword filter for identifier or name.</param>
    /// <param name="plan">Optional plan filter.</param>
    /// <param name="isActive">Optional active-state filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged list of tenant read models.</returns>
    Task<PagedList<TenantReadModel>> GetPagedTenantsAsync(
        int page,
        int size,
        string? keyword,
        string? plan,
        bool? isActive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists active tenants for message persistence bootstrap.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active tenant seed records.</returns>
    Task<IReadOnlyList<TenantConnectionSeedReadModel>> ListConnectionSeedsAsync(CancellationToken cancellationToken);
}
