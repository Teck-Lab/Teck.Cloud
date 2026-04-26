// <copyright file="RemoteTenantDatabaseStrategyResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;
using Grpc.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

namespace Web.Public.Gateway.Services;

/// <summary>
/// Resolves tenant database strategy by invoking the Customer handler server via FE remote command bus.
/// </summary>
internal sealed class RemoteTenantDatabaseStrategyResolver(ILogger<RemoteTenantDatabaseStrategyResolver> logger) : ITenantDatabaseStrategyResolver
{
    private readonly ILogger<RemoteTenantDatabaseStrategyResolver> logger = logger;

    /// <inheritdoc/>
    public async Task<TenantDatabaseStrategyLookupResult> ResolveAsync(string tenantId, string? serviceName, CancellationToken cancellationToken)
    {
        try
        {
            TenantDatabaseInfoRpcResult reply = await new GetTenantDatabaseInfoCommand
            {
                TenantId = tenantId,
                ServiceName = serviceName ?? string.Empty,
            }.RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (reply is null)
            {
                return new TenantDatabaseStrategyLookupResult(
                    false,
                    null,
                    StatusCodes.Status503ServiceUnavailable,
                    "tenant.lookup.unavailable",
                    "Tenant lookup service is unavailable.");
            }

            if (!reply.Found)
            {
                bool tenantNotFound = !string.IsNullOrWhiteSpace(reply.ErrorDetail)
                    && reply.ErrorDetail.Contains("not found", StringComparison.OrdinalIgnoreCase);

                return new TenantDatabaseStrategyLookupResult(
                    false,
                    null,
                    tenantNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
                    tenantNotFound ? "tenant.not_found" : "tenant.lookup.invalid",
                    string.IsNullOrWhiteSpace(reply.ErrorDetail) ? "Unable to resolve tenant database strategy." : reply.ErrorDetail);
            }

            if (string.IsNullOrWhiteSpace(reply.DatabaseStrategy))
            {
                return new TenantDatabaseStrategyLookupResult(
                    false,
                    null,
                    StatusCodes.Status503ServiceUnavailable,
                    "tenant.dbstrategy.missing",
                    "Customer service returned no database strategy for tenant.");
            }

            return new TenantDatabaseStrategyLookupResult(true, reply.DatabaseStrategy, null, null, null);
        }
        catch (RpcException exception)
        {
            logger.LogWarning(
                exception,
                "Tenant lookup RPC failed. TenantId={TenantId}; ServiceName={ServiceName}; StatusCode={StatusCode}; Detail={Detail}",
                tenantId,
                serviceName,
                exception.StatusCode,
                exception.Status.Detail);

            string detail = string.IsNullOrWhiteSpace(exception.Status.Detail)
                ? "Tenant lookup service is unavailable."
                : $"Tenant lookup service is unavailable: {exception.Status.Detail}";

            return new TenantDatabaseStrategyLookupResult(
                false,
                null,
                StatusCodes.Status503ServiceUnavailable,
                "tenant.lookup.unavailable",
                detail);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected tenant lookup failure. TenantId={TenantId}; ServiceName={ServiceName}",
                tenantId,
                serviceName);

            return new TenantDatabaseStrategyLookupResult(
                false,
                null,
                StatusCodes.Status503ServiceUnavailable,
                "tenant.lookup.unavailable",
                "Tenant lookup service is unavailable.");
        }
    }
}
