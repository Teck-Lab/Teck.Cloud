// <copyright file="TenantDatabaseMetadataResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Responses;

/// <summary>
/// Response model for tenant database metadata.
/// </summary>
public record TenantDatabaseMetadataResponse
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = default!;

    /// <summary>
    /// Gets the environment variable key for write database connection string.
    /// Example: ConnectionStrings__Tenants__{tenantId}__Write.
    /// </summary>
    public string WriteEnvVarKey { get; init; } = default!;

    /// <summary>
    /// Gets the environment variable key for read database connection string.
    /// </summary>
    public string? ReadEnvVarKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}
