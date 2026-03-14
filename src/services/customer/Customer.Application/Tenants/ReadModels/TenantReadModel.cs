// <copyright file="TenantReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Domain;

namespace Customer.Application.Tenants.ReadModels;

/// <summary>
/// Read model for Tenant entities, optimized for query operations.
/// </summary>
public sealed class TenantReadModel : ReadModelBase<Guid>
{
    /// <summary>
    /// Gets or sets the tenant identifier (slug).
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant plan name.
    /// </summary>
    public string Plan { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Keycloak organization identifier.
    /// </summary>
    public string? KeycloakOrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the tenant database strategy name.
    /// </summary>
    public string DatabaseStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database provider name.
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; set; }
}
