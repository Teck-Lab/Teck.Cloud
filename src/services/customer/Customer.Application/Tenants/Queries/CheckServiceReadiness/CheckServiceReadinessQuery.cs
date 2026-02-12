using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Queries.CheckServiceReadiness;

/// <summary>
/// Query to check if a service is ready for a tenant (migration completed).
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="ServiceName">The service name.</param>
public record CheckServiceReadinessQuery(Guid TenantId, string ServiceName) : IQuery<ErrorOr<bool>>;
