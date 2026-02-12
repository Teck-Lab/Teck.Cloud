using Customer.Application.Tenants.DTOs;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Queries.GetTenantDatabaseInfo;

/// <summary>
/// Query to get database information for a specific service.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="ServiceName">The service name.</param>
public record GetTenantDatabaseInfoQuery(Guid TenantId, string ServiceName) : IQuery<ErrorOr<ServiceDatabaseInfoDto>>;
