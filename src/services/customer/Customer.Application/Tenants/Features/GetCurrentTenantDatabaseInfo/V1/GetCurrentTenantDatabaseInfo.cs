// <copyright file="GetCurrentTenantDatabaseInfo.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;

/// <summary>
/// Query for database metadata of the current tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="ServiceName">Optional service name used for replica metadata resolution.</param>
public sealed record GetCurrentTenantDatabaseInfoQuery(Guid TenantId, string? ServiceName)
    : IQuery<ErrorOr<GetCurrentTenantDatabaseInfoResponse>>;

/// <summary>
/// Handler for current tenant database metadata queries.
/// </summary>
public sealed class GetCurrentTenantDatabaseInfoQueryHandler(ITenantReadRepository tenantReadRepository)
    : IQueryHandler<GetCurrentTenantDatabaseInfoQuery, ErrorOr<GetCurrentTenantDatabaseInfoResponse>>
{
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<GetCurrentTenantDatabaseInfoResponse>> Handle(
        GetCurrentTenantDatabaseInfoQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        string? serviceName = string.IsNullOrWhiteSpace(query.ServiceName)
            ? null
            : query.ServiceName.Trim();

        var tenantDatabaseInfo = await this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(query.TenantId, serviceName, cancellationToken)
            .ConfigureAwait(false);

        if (tenantDatabaseInfo is null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{query.TenantId}' not found");
        }

        return new GetCurrentTenantDatabaseInfoResponse
        {
            TenantId = tenantDatabaseInfo.TenantId,
            Identifier = tenantDatabaseInfo.Identifier,
            DatabaseStrategy = tenantDatabaseInfo.DatabaseStrategy,
            DatabaseProvider = tenantDatabaseInfo.DatabaseProvider,
            HasReadReplicas = tenantDatabaseInfo.HasReadReplicas,
            ServiceName = serviceName,
        };
    }
}
