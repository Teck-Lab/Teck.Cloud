// <copyright file="GetCustomerServiceVersionCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;

namespace Customer.Api.Grpc.V1;

/// <summary>
/// Handles remote service version requests for the Customer service.
/// </summary>
internal sealed class GetCustomerServiceVersionCommandHandler : FastEndpoints.ICommandHandler<GetCustomerServiceVersionCommand, ServiceVersionRpcResult>
{
    /// <inheritdoc/>
    public Task<ServiceVersionRpcResult> ExecuteAsync(GetCustomerServiceVersionCommand command, CancellationToken ct)
    {
        _ = command;
        _ = ct;

        Assembly assembly = typeof(GetCustomerServiceVersionCommandHandler).Assembly;
        string version =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        ServiceVersionRpcResult response = new()
        {
            Service = "customer",
            Version = version,
        };

        return Task.FromResult(response);
    }
}
