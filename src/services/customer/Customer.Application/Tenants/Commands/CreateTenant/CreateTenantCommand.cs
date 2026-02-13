using Customer.Application.Tenants.DTOs;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;
using SharedKernel.Core.Models;

namespace Customer.Application.Tenants.Commands.CreateTenant;

/// <summary>
/// Command to create a new tenant.
/// </summary>
/// <param name="Identifier">The tenant identifier (unique name/slug).</param>
/// <param name="Name">The tenant display name.</param>
/// <param name="Plan">The tenant plan.</param>
/// <param name="DatabaseStrategy">The database strategy.</param>
/// <param name="DatabaseProvider">The database provider.</param>
/// <param name="CustomCredentials">Optional custom database credentials (for External strategy).</param>
public record CreateTenantCommand(
    string Identifier,
    string Name,
    string Plan,
    DatabaseStrategy DatabaseStrategy,
    DatabaseProvider DatabaseProvider,
    DatabaseCredentials? CustomCredentials = null
) : ICommand<ErrorOr<TenantDto>>;
