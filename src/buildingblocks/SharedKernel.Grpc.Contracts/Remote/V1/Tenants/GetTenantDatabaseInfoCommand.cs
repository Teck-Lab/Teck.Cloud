// <copyright file="GetTenantDatabaseInfoCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

/// <summary>
/// Requests tenant database metadata from the Customer service.
/// </summary>
public sealed class GetTenantDatabaseInfoCommand : ICommand<TenantDatabaseInfoRpcResult>
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the downstream service name requesting the metadata.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
}
