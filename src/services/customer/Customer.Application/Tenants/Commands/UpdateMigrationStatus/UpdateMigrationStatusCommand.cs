using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Migration.Models;

namespace Customer.Application.Tenants.Commands.UpdateMigrationStatus;

/// <summary>
/// Command to update the migration status for a service.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="ServiceName">The service name.</param>
/// <param name="Status">The migration status.</param>
/// <param name="LastMigrationVersion">The last migration version applied.</param>
/// <param name="ErrorMessage">The error message if the migration failed.</param>
public record UpdateMigrationStatusCommand(
    Guid TenantId,
    string ServiceName,
    MigrationStatus Status,
    string? LastMigrationVersion,
    string? ErrorMessage
) : ICommand<ErrorOr<Updated>>;
