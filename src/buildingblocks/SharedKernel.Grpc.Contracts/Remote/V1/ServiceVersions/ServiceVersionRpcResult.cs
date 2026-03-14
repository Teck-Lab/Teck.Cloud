// <copyright file="ServiceVersionRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;

/// <summary>
/// Represents a service version payload returned by remote command handlers.
/// </summary>
public sealed class ServiceVersionRpcResult
{
    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic or informational service version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}
