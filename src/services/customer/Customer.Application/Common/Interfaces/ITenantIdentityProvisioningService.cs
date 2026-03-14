// <copyright file="ITenantIdentityProvisioningService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Common.Interfaces;

/// <summary>
/// Provisions tenant organizations in external identity providers.
/// </summary>
public interface ITenantIdentityProvisioningService
{
    /// <summary>
    /// Creates an organization for the tenant and returns the provider organization identifier.
    /// </summary>
    /// <param name="tenantIdentifier">Tenant identifier.</param>
    /// <param name="tenantName">Tenant display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identity provider organization identifier.</returns>
    Task<string> CreateOrganizationAsync(
        string tenantIdentifier,
        string tenantName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an organization from the identity provider.
    /// </summary>
    /// <param name="organizationId">Identity provider organization identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteOrganizationAsync(string organizationId, CancellationToken cancellationToken);
}
