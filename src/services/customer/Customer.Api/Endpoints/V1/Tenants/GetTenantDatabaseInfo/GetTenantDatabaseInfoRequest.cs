namespace Customer.Api.Endpoints.V1.Tenants.GetTenantDatabaseInfo;

/// <summary>
/// Request to get tenant database info for a specific service.
/// </summary>
/// <param name="TenantId">The tenant id.</param>
/// <param name="ServiceName">The service name.</param>
internal record GetTenantDatabaseInfoRequest(Guid TenantId, string ServiceName);
