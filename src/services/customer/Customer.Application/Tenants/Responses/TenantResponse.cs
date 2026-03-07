// <copyright file="TenantResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Responses;

/// <summary>
/// Response model for Tenant.
/// </summary>
public record TenantResponse
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the tenant identifier (unique name/slug).
    /// </summary>
    public string Identifier { get; init; } = default!;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Gets the tenant plan.
    /// </summary>
    public string Plan { get; init; } = default!;

    /// <summary>
    /// Gets the associated Keycloak organization identifier.
    /// </summary>
    public string? KeycloakOrganizationId { get; init; }

    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public string DatabaseStrategy { get; init; } = default!;

    /// <summary>
    /// Gets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the database metadata for each service.
    /// </summary>
    public IReadOnlyCollection<TenantDatabaseMetadataResponse> Databases { get; init; } = Array.Empty<TenantDatabaseMetadataResponse>();

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; init; }
}
