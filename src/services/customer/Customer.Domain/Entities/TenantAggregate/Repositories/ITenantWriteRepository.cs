// <copyright file="ITenantWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.TenantAggregate.Repositories;

/// <summary>
/// Repository interface for Tenant write operations.
/// </summary>
public interface ITenantWriteRepository
{
    /// <summary>
    /// Adds a new tenant.
    /// </summary>
    /// <param name="tenant">The tenant to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing tenant.
    /// </summary>
    /// <param name="tenant">The tenant to update.</param>
    void Update(Tenant tenant);

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="id">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant or null if not found.</returns>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a tenant by Keycloak organization identifier.
    /// </summary>
    /// <param name="identifier">The Keycloak organization identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant or null if not found.</returns>
    Task<Tenant?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a tenant with the given identifier exists.
    /// </summary>
    /// <param name="identifier">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tenant exists, false otherwise.</returns>
    Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a tenant.
    /// </summary>
    /// <param name="tenant">The tenant to delete.</param>
    void Delete(Tenant tenant);
}
