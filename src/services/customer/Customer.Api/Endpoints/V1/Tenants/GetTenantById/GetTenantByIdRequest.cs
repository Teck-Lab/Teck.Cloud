namespace Customer.Api.Endpoints.V1.Tenants.GetTenantById;

/// <summary>
/// Request to get a tenant by id.
/// </summary>
/// <param name="Id">The tenant id.</param>
internal record GetTenantByIdRequest(Guid Id);
