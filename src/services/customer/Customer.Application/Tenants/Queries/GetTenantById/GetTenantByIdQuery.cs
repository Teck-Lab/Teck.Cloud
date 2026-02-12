using Customer.Application.Tenants.DTOs;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Queries.GetTenantById;

/// <summary>
/// Query to get a tenant by ID.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
public record GetTenantByIdQuery(Guid TenantId) : IQuery<ErrorOr<TenantDto>>;
