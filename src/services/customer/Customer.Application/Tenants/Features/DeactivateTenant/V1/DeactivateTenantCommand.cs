// <copyright file="DeactivateTenantCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Customer.Application.Tenants.Features.DeactivateTenant.V1;

/// <summary>
/// Command to deactivate a tenant.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
public sealed record DeactivateTenantCommand(Guid TenantId) : ICommand<ErrorOr<TenantResponse>>;

/// <summary>
/// Handler for tenant deactivation.
/// </summary>
public sealed class DeactivateTenantCommandHandler(
    ITenantWriteRepository tenantRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeactivateTenantCommand, ErrorOr<TenantResponse>>
{
    private readonly ITenantWriteRepository tenantRepository = tenantRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantResponse>> Handle(DeactivateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await this.tenantRepository
            .GetByIdAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{command.TenantId}' not found");
        }

        tenant.Deactivate();
        this.tenantRepository.Update(tenant);
        _ = await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Map(tenant);
    }

    private static TenantResponse Map(Domain.Entities.TenantAggregate.Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Plan = tenant.Plan,
            KeycloakOrganizationId = tenant.KeycloakOrganizationId,
            DatabaseStrategy = tenant.DatabaseStrategy.Name,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedOn = tenant.UpdatedOn,
            Databases = tenant.Databases
                .Select(database => new TenantDatabaseMetadataResponse
                {
                    ServiceName = database.ServiceName,
                    WriteEnvVarKey = database.WriteEnvVarKey,
                    ReadEnvVarKey = database.ReadEnvVarKey,
                    HasSeparateReadDatabase = database.HasSeparateReadDatabase,
                })
                .ToArray(),
        };
    }
}
