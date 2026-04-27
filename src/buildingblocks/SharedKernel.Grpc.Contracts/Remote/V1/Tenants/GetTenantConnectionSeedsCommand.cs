// <copyright file="GetTenantConnectionSeedsCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

/// <summary>
/// Requests active tenant connection seed data from the Customer service.
/// </summary>
public sealed class GetTenantConnectionSeedsCommand : ICommand<TenantConnectionSeedsRpcResult>
{
    /// <summary>
    /// Gets or sets the downstream service name requesting tenant seed data.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
}
