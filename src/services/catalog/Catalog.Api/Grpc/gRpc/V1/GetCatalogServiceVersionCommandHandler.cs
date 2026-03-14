// <copyright file="GetCatalogServiceVersionCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;

namespace Catalog.Api.Grpc.V1;

/// <summary>
/// Handles remote service version requests for the Catalog service.
/// </summary>
internal sealed class GetCatalogServiceVersionCommandHandler : FastEndpoints.ICommandHandler<GetCatalogServiceVersionCommand, ServiceVersionRpcResult>
{
    /// <inheritdoc/>
    public Task<ServiceVersionRpcResult> ExecuteAsync(GetCatalogServiceVersionCommand command, CancellationToken ct)
    {
        _ = command;
        _ = ct;

        Assembly assembly = typeof(GetCatalogServiceVersionCommandHandler).Assembly;
        string version =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        ServiceVersionRpcResult response = new()
        {
            Service = "catalog",
            Version = version,
        };

        return Task.FromResult(response);
    }
}
