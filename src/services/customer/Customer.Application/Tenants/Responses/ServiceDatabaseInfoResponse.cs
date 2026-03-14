// <copyright file="ServiceDatabaseInfoResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Responses;

/// <summary>
/// Response model for service database information.
/// </summary>
public record ServiceDatabaseInfoResponse
{
    /// <summary>
    /// Gets the environment variable key for write database connection string.
    /// </summary>
    public string WriteEnvVarKey { get; init; } = default!;

    /// <summary>
    /// Gets the environment variable key for read database connection string (if separate).
    /// </summary>
    public string? ReadEnvVarKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}
