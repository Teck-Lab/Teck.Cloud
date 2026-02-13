using SharedKernel.Core.Database;

namespace Customer.Api.Endpoints.V1.Tenants.CreateTenant;

/// <summary>
/// Request to create a new tenant.
/// </summary>
/// <param name="Identifier">The unique identifier for the tenant.</param>
/// <param name="Name">The tenant name.</param>
/// <param name="Plan">The subscription plan.</param>
/// <param name="DatabaseStrategy">The database strategy (Shared, Dedicated, External).</param>
/// <param name="DatabaseProvider">The database provider (PostgreSQL, SqlServer, MySQL).</param>
/// <param name="CustomCredentials">Optional custom credentials for External strategy.</param>
internal record CreateTenantRequest(
    string Identifier,
    string Name,
    string Plan,
    string DatabaseStrategy,
    string DatabaseProvider,
    DatabaseCredentials? CustomCredentials);
