using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Queries.GetTenantDatabaseInfo;

/// <summary>
/// Handler for GetTenantDatabaseInfoQuery.
/// </summary>
public class GetTenantDatabaseInfoQueryHandler : IQueryHandler<GetTenantDatabaseInfoQuery, ErrorOr<ServiceDatabaseInfoDto>>
{
    private readonly ITenantWriteRepository _tenantRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTenantDatabaseInfoQueryHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    public GetTenantDatabaseInfoQueryHandler(ITenantWriteRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<ServiceDatabaseInfoDto>> Handle(GetTenantDatabaseInfoQuery query, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(query.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{query.TenantId}' not found");
        }

        var database = tenant.Databases.FirstOrDefault(dbMetadata => dbMetadata.ServiceName == query.ServiceName);
        if (database == null)
        {
            return Error.NotFound("Tenant.DatabaseNotFound", $"Database metadata for service '{query.ServiceName}' not found");
        }

        var dto = new ServiceDatabaseInfoDto
        {
            VaultWritePath = database.VaultWritePath,
            VaultReadPath = database.VaultReadPath,
            HasSeparateReadDatabase = database.HasSeparateReadDatabase
        };

        return dto;
    }
}
