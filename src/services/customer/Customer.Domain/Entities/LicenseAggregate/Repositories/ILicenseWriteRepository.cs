// <copyright file="ILicenseWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.LicenseAggregate.Repositories;

/// <summary>
/// Repository interface for License write operations.
/// </summary>
public interface ILicenseWriteRepository
{
    /// <summary>
    /// Adds a new license.
    /// </summary>
    /// <param name="license">The license to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(License license, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing license.
    /// </summary>
    /// <param name="license">The license to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(License license, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a license by ID.
    /// </summary>
    /// <param name="id">The license ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The license or null if not found.</returns>
    Task<License?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the active license for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active license or null if not found.</returns>
    Task<License?> GetActiveByTenantIdAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the active license for a location.
    /// </summary>
    /// <param name="locationId">The location ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active license or null if not found.</returns>
    Task<License?> GetActiveByLocationIdAsync(string locationId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all licenses for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All licenses for the tenant.</returns>
    Task<IReadOnlyList<License>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken);
}
