// <copyright file="GetTenantByIdQueryHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Repositories;
using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.GetTenantById.V1;

/// <summary>
/// Handler for GetTenantByIdQuery.
/// </summary>
public class GetTenantByIdQueryHandler : IQueryHandler<GetTenantByIdQuery, ErrorOr<TenantResponse>>
{
    private readonly ITenantReadRepository tenantReadRepository;
    private readonly ITenantWriteRepository tenantRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTenantByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="tenantReadRepository">The tenant read repository.</param>
    /// <param name="tenantRepository">The tenant repository.</param>
    public GetTenantByIdQueryHandler(
        ITenantReadRepository tenantReadRepository,
        ITenantWriteRepository tenantRepository)
    {
        this.tenantReadRepository = tenantReadRepository;
        this.tenantRepository = tenantRepository;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantResponse>> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenant = await this.tenantReadRepository.GetByIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{query.TenantId}' not found");
        }

        var tenantEntity = await this.tenantRepository.GetByIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);

        var response = new TenantResponse
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Plan = tenant.Plan,
            KeycloakOrganizationId = tenant.KeycloakOrganizationId,
            DatabaseStrategy = tenant.DatabaseStrategy,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedOn = tenant.UpdatedOn,
            Databases = tenantEntity?.Databases
                .Select(database => new TenantDatabaseMetadataResponse
                {
                    ServiceName = database.ServiceName,
                    WriteEnvVarKey = database.WriteEnvVarKey,
                    ReadEnvVarKey = database.ReadEnvVarKey,
                    HasSeparateReadDatabase = database.ReadDatabaseMode == Domain.Entities.TenantAggregate.ReadDatabaseMode.SeparateRead,
                })
                .ToArray() ?? Array.Empty<TenantDatabaseMetadataResponse>(),
        };

        return response;
    }
}
