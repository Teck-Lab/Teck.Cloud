using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Migration.Models;

namespace Customer.Application.Tenants.Queries.CheckServiceReadiness;

/// <summary>
/// Handler for CheckServiceReadinessQuery.
/// </summary>
public class CheckServiceReadinessQueryHandler : IQueryHandler<CheckServiceReadinessQuery, ErrorOr<bool>>
{
    private readonly ITenantWriteRepository _tenantRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckServiceReadinessQueryHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    public CheckServiceReadinessQueryHandler(ITenantWriteRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<bool>> Handle(CheckServiceReadinessQuery query, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(query.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{query.TenantId}' not found");
        }

        var migrationStatus = tenant.MigrationStatuses.FirstOrDefault(status => status.ServiceName == query.ServiceName);
        if (migrationStatus == null)
        {
            return Error.NotFound("Tenant.MigrationStatusNotFound", $"Migration status for service '{query.ServiceName}' not found");
        }

        var isReady = migrationStatus.Status == MigrationStatus.Completed;
        return isReady;
    }
}
