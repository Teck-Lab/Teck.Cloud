// <copyright file="TenantConnectionSeedsRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

/// <summary>
/// Represents a response containing active tenant connection seed data.
/// </summary>
public sealed class TenantConnectionSeedsRpcResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the request succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the tenant seed items.
    /// </summary>
    public IReadOnlyList<TenantConnectionSeedRpcItem> Items { get; set; } = Array.Empty<TenantConnectionSeedRpcItem>();

    /// <summary>
    /// Gets or sets an optional error detail when <see cref="Succeeded"/> is false.
    /// </summary>
    public string? ErrorDetail { get; set; }
}
