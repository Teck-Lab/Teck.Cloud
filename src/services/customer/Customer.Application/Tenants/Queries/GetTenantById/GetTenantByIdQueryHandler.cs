using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Queries.GetTenantById;

/// <summary>
/// Handler for GetTenantByIdQuery.
/// </summary>
public class GetTenantByIdQueryHandler : IQueryHandler<GetTenantByIdQuery, ErrorOr<TenantDto>>
{
    private readonly ITenantWriteRepository _tenantRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTenantByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    public GetTenantByIdQueryHandler(ITenantWriteRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantDto>> Handle(GetTenantByIdQuery query, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(query.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{query.TenantId}' not found");
        }

        var dto = new TenantDto
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Plan = tenant.Plan,
            DatabaseStrategy = tenant.DatabaseStrategy.Name,
            DatabaseProvider = tenant.DatabaseProvider.Name,
            IsActive = tenant.IsActive,
            Databases = tenant.Databases.Select(database => new TenantDatabaseMetadataDto
            {
                ServiceName = database.ServiceName,
                WriteEnvVarKey = database.WriteEnvVarKey,
                ReadEnvVarKey = database.ReadEnvVarKey,
                HasSeparateReadDatabase = database.HasSeparateReadDatabase
            }).ToList(),
            CreatedAt = tenant.CreatedAt,
            UpdatedOn = tenant.UpdatedOn
        };

        return dto;
    }
}
