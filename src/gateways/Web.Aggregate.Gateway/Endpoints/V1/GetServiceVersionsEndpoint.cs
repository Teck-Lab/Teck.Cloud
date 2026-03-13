// <copyright file="GetServiceVersionsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,SA1402,AV1555,AV1580,CA1515,CA1062,CS1591
using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using FastEndpoints;
using Grpc.Core;
using Keycloak.AuthServices.Authorization;
using SharedKernel.Grpc.Contracts.Remote.V1.ServiceVersions;
using SharedKernel.Infrastructure.Endpoints;
using ZiggyCreatures.Caching.Fusion;

namespace Web.Aggregate.Gateway.Endpoints.V1;

public sealed class GetServiceVersionsEndpoint(
    IFusionCache fusionCache) : EndpointWithoutRequest<ServiceVersionsResponse>
{
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromSeconds(30);
    private readonly IFusionCache fusionCache = fusionCache;

    public override void Configure()
    {
        Get("/Services/Versions");
        Version(1);
        Tags("Service");
        Options(endpoint => endpoint.RequireProtectedResource("system", "list"));
    }

    [RequiresDynamicCode()]
    [RequiresUnreferencedCode()]
    public override async Task HandleAsync(CancellationToken ct)
    {
        Task<ErrorOr<ServiceVersionItem>> catalogTask = GetCatalogServiceVersionAsync(ct);
        Task<ErrorOr<ServiceVersionItem>> customerTask = GetCustomerServiceVersionAsync(ct);

        await Task.WhenAll(catalogTask, customerTask).ConfigureAwait(false);

        ErrorOr<ServiceVersionItem> catalogVersionResult = await catalogTask.ConfigureAwait(false);
        ErrorOr<ServiceVersionItem> customerVersionResult = await customerTask.ConfigureAwait(false);

        if (catalogVersionResult.IsError && customerVersionResult.IsError)
        {
            List<Error> errors = [];
            errors.AddRange(catalogVersionResult.Errors);
            errors.AddRange(customerVersionResult.Errors);

            await this.SendAsync((ErrorOr<ServiceVersionsResponse>)errors, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (catalogVersionResult.IsError)
        {
            await this.SendAsync((ErrorOr<ServiceVersionsResponse>)catalogVersionResult.Errors, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (customerVersionResult.IsError)
        {
            await this.SendAsync((ErrorOr<ServiceVersionsResponse>)customerVersionResult.Errors, cancellation: ct).ConfigureAwait(false);
            return;
        }

        ServiceVersionsResponse response = new([catalogVersionResult.Value, customerVersionResult.Value]);
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(response, ct).ConfigureAwait(false);
    }

    private async Task<ErrorOr<ServiceVersionItem>> GetCatalogServiceVersionAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "aggregate:service-version:catalog";

        var cachedResult = await fusionCache.TryGetAsync<ServiceVersionItem>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (cachedResult.HasValue && cachedResult.Value is not null)
        {
            return cachedResult.Value;
        }

        ErrorOr<ServiceVersionItem> fetchResult = await FetchCatalogServiceVersionAsync(cancellationToken).ConfigureAwait(false);
        if (fetchResult.IsError)
        {
            return fetchResult.Errors;
        }

        ServiceVersionItem version = fetchResult.Value;

        await fusionCache.SetAsync(
            cacheKey,
            version,
            options => options
                .SetDuration(SuccessCacheDuration)
                .SetFailSafe(false),
            token: cancellationToken).ConfigureAwait(false);

        return version;
    }

    private async Task<ErrorOr<ServiceVersionItem>> GetCustomerServiceVersionAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "aggregate:service-version:customer";

        var cachedResult = await fusionCache.TryGetAsync<ServiceVersionItem>(cacheKey, token: cancellationToken).ConfigureAwait(false);
        if (cachedResult.HasValue && cachedResult.Value is not null)
        {
            return cachedResult.Value;
        }

        ErrorOr<ServiceVersionItem> fetchResult = await FetchCustomerServiceVersionAsync(cancellationToken).ConfigureAwait(false);
        if (fetchResult.IsError)
        {
            return fetchResult.Errors;
        }

        ServiceVersionItem version = fetchResult.Value;

        await fusionCache.SetAsync(
            cacheKey,
            version,
            options => options
                .SetDuration(SuccessCacheDuration)
                .SetFailSafe(false),
            token: cancellationToken).ConfigureAwait(false);

        return version;
    }

    private static async Task<ErrorOr<ServiceVersionItem>> FetchCatalogServiceVersionAsync(CancellationToken cancellationToken)
    {
        ServiceVersionRpcResult versionResponse;
        try
        {
            versionResponse = await new GetCatalogServiceVersionCommand().RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (RpcException rpcException)
        {
            string detail = string.IsNullOrWhiteSpace(rpcException.Status.Detail)
                ? rpcException.Message
                : rpcException.Status.Detail;

            return Error.Failure(
                "Aggregate.ServiceVersion.DownstreamCallFailed",
                $"Failed to retrieve service version for service 'catalog'. gRPC status: {rpcException.StatusCode}. Detail: {detail}");
        }

        if (versionResponse is null)
        {
            return Error.Failure("Aggregate.ServiceVersion.EmptyResponse", "Service version response was null for service 'catalog'.");
        }

        if (string.IsNullOrWhiteSpace(versionResponse.Service) || string.IsNullOrWhiteSpace(versionResponse.Version))
        {
            return Error.Failure("Aggregate.ServiceVersion.InvalidResponse", "Service version response was invalid for service 'catalog'.");
        }

        return new ServiceVersionItem(versionResponse.Service, versionResponse.Version);
    }

    private static async Task<ErrorOr<ServiceVersionItem>> FetchCustomerServiceVersionAsync(CancellationToken cancellationToken)
    {
        ServiceVersionRpcResult versionResponse;
        try
        {
            versionResponse = await new GetCustomerServiceVersionCommand().RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (RpcException rpcException)
        {
            string detail = string.IsNullOrWhiteSpace(rpcException.Status.Detail)
                ? rpcException.Message
                : rpcException.Status.Detail;

            return Error.Failure(
                "Aggregate.ServiceVersion.DownstreamCallFailed",
                $"Failed to retrieve service version for service 'customer'. gRPC status: {rpcException.StatusCode}. Detail: {detail}");
        }

        if (versionResponse is null)
        {
            return Error.Failure("Aggregate.ServiceVersion.EmptyResponse", "Service version response was null for service 'customer'.");
        }

        if (string.IsNullOrWhiteSpace(versionResponse.Service) || string.IsNullOrWhiteSpace(versionResponse.Version))
        {
            return Error.Failure("Aggregate.ServiceVersion.InvalidResponse", "Service version response was invalid for service 'customer'.");
        }

        return new ServiceVersionItem(versionResponse.Service, versionResponse.Version);
    }
}

public sealed record HealthResponse(string Status);
public sealed record ServiceVersionItem(string Service, string Version);
public sealed record ServiceVersionsResponse(IReadOnlyList<ServiceVersionItem> Services);
