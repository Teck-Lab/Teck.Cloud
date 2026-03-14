// <copyright file="GetCustomerServiceVersionCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;

/// <summary>
/// Requests the current Customer service version from a remote handler server.
/// </summary>
public sealed class GetCustomerServiceVersionCommand : ICommand<ServiceVersionRpcResult>
{
}
