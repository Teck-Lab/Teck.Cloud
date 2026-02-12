namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

/// <summary>
/// Request to check if a service is ready for a tenant.
/// </summary>
/// <param name="TenantId">The tenant id.</param>
/// <param name="ServiceName">The service name.</param>
internal record CheckServiceReadinessRequest(Guid TenantId, string ServiceName);
