using SharedKernel.Migration.Models;

namespace Customer.Api.Endpoints.V1.Tenants.UpdateMigrationStatus;

/// <summary>
/// Request to update tenant migration status.
/// </summary>
/// <param name="TenantId">The tenant id.</param>
/// <param name="ServiceName">The service name.</param>
/// <param name="Status">The migration status.</param>
/// <param name="LastMigrationVersion">The last migration version applied.</param>
/// <param name="ErrorMessage">The error message if failed.</param>
internal record UpdateMigrationStatusRequest(
    Guid TenantId,
    string ServiceName,
    MigrationStatus Status,
    string? LastMigrationVersion,
    string? ErrorMessage);
