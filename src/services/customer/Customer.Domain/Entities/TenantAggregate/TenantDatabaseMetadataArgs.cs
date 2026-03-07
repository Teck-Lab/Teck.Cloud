// <copyright file="TenantDatabaseMetadataArgs.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Arguments required to add tenant database metadata.
/// </summary>
public sealed class TenantDatabaseMetadataArgs
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the write environment variable key.
    /// </summary>
    public string WriteEnvVarKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the read environment variable key.
    /// </summary>
    public string? ReadEnvVarKey { get; init; }

    /// <summary>
    /// Gets the read database mode.
    /// </summary>
    public ReadDatabaseMode ReadDatabaseMode { get; init; }
}
