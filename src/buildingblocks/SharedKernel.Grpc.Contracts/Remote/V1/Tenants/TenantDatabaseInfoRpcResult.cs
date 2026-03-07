// <copyright file="TenantDatabaseInfoRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

/// <summary>
/// Represents tenant database metadata returned by a remote command handler.
/// </summary>
public sealed class TenantDatabaseInfoRpcResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the tenant was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database strategy.
    /// </summary>
    public string DatabaseStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database provider.
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether read replicas are enabled.
    /// </summary>
    public bool HasReadReplicas { get; set; }

    /// <summary>
    /// Gets or sets an optional error detail when <see cref="Found"/> is false.
    /// </summary>
    public string? ErrorDetail { get; set; }
}
