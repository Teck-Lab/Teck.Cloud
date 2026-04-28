// <copyright file="GetTenantDatabaseInfoCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Repositories;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

namespace Customer.Api.Grpc.V1;

/// <summary>
/// Handles remote tenant database metadata lookups.
/// </summary>
/// <param name="tenantReadRepository">The tenant read repository.</param>
internal sealed class GetTenantDatabaseInfoCommandHandler(ITenantReadRepository tenantReadRepository)
    : FastEndpoints.ICommandHandler<GetTenantDatabaseInfoCommand, TenantDatabaseInfoRpcResult>
{
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;

    /// <inheritdoc/>
    public async Task<TenantDatabaseInfoRpcResult> ExecuteAsync(GetTenantDatabaseInfoCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Guid.TryParse(command.TenantId, out Guid tenantId))
        {
            return new TenantDatabaseInfoRpcResult
            {
                Found = false,
                ErrorDetail = "tenant_id must be a valid GUID.",
            };
        }

        var tenant = await this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, command.ServiceName, ct)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return new TenantDatabaseInfoRpcResult
            {
                Found = false,
                TenantId = command.TenantId,
                ErrorDetail = $"Tenant '{command.TenantId}' was not found.",
            };
        }

        return new TenantDatabaseInfoRpcResult
        {
            Found = true,
            TenantId = tenant.TenantId.ToString(),
            Identifier = tenant.Identifier,
            DatabaseStrategy = tenant.DatabaseStrategy,
            DatabaseProvider = tenant.DatabaseProvider,
            HasReadReplicas = tenant.HasReadReplicas,
        };
    }
}
