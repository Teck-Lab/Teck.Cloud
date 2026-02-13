using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using Microsoft.Extensions.Configuration;
using SharedKernel.Core;
using SharedKernel.Core.CQRS;

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

        // Determine readiness by checking if tenant has a DB entry for the service and if the DSN env var is present.
        var dbMetadata = tenant.Databases.FirstOrDefault(metadata => metadata.ServiceName == query.ServiceName);
        if (dbMetadata == null)
        {
            return Error.NotFound("Tenant.DatabaseMetadataNotFound", $"Database metadata for service '{query.ServiceName}' not found");
        }

        // Attempt to resolve the write DSN env var for the tenant/service.
        try
        {
            var dsn = TenantConnectionProvider.GetTenantConnection(new ConfigurationBuilder().AddEnvironmentVariables().Build(), tenant.Identifier, readOnly: false);
            return !string.IsNullOrWhiteSpace(dsn);
        }
        catch (Exception exception)
        {
            return Error.Unexpected("Tenant.DsnResolutionFailed", exception.ToString());
        }
    }
}
