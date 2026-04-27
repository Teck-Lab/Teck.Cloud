// <copyright file="GetTenantConnectionSeedsCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Repositories;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

namespace Customer.Api.Grpc.V1;

/// <summary>
/// Handles remote bootstrap requests for active tenant connection seed data.
/// </summary>
/// <param name="tenantReadRepository">The tenant read repository.</param>
internal sealed class GetTenantConnectionSeedsCommandHandler(ITenantReadRepository tenantReadRepository)
    : FastEndpoints.ICommandHandler<GetTenantConnectionSeedsCommand, TenantConnectionSeedsRpcResult>
{
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;

    /// <inheritdoc/>
    public async Task<TenantConnectionSeedsRpcResult> ExecuteAsync(GetTenantConnectionSeedsCommand command, CancellationToken ct)
    {
        _ = command;

        var seeds = await this.tenantReadRepository
            .ListConnectionSeedsAsync(ct)
            .ConfigureAwait(false);

        return new TenantConnectionSeedsRpcResult
        {
            Succeeded = true,
            Items = seeds
                .Select(seed => new TenantConnectionSeedRpcItem
                {
                    TenantId = seed.TenantId.ToString("D"),
                    Identifier = seed.Identifier,
                    DatabaseStrategy = seed.DatabaseStrategy,
                })
                .ToList(),
        };
    }
}
