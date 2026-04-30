// <copyright file="UpdateTenantProfileCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Customer.Application.Tenants.Features.UpdateTenantProfile.V1;

/// <summary>
/// Command to update mutable tenant profile fields.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Name">Optional updated display name.</param>
/// <param name="Plan">Optional updated plan.</param>
public sealed record UpdateTenantProfileCommand(Guid TenantId, string? Name, string? Plan) : ICommand<ErrorOr<TenantResponse>>;

/// <summary>
/// Handler for tenant profile updates.
/// </summary>
public sealed class UpdateTenantProfileCommandHandler(
    ITenantWriteRepository tenantRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateTenantProfileCommand, ErrorOr<TenantResponse>>
{
    private static readonly Dictionary<string, int> PlanRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Free"] = 0,
        ["Starter"] = 1,
        ["Basic"] = 1,
        ["Business"] = 2,
        ["Pro"] = 2,
        ["Enterprise"] = 3,
    };

    private readonly ITenantWriteRepository tenantRepository = tenantRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantResponse>> Handle(UpdateTenantProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await this.tenantRepository
            .GetByIdAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", $"Tenant with ID '{command.TenantId}' not found");
        }

        if (IsPlanDowngrade(tenant.Plan, command.Plan))
        {
            return Error.Validation("Tenant.Plan.DowngradeNotAllowed", "Tenant plan downgrades require a dedicated workflow");
        }

        ErrorOr<Updated> updateResult = tenant.UpdateProfile(command.Name, command.Plan);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        this.tenantRepository.Update(tenant);
        _ = await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Map(tenant);
    }

    private static bool IsPlanDowngrade(string currentPlan, string? requestedPlan)
    {
        if (string.IsNullOrWhiteSpace(requestedPlan))
        {
            return false;
        }

        if (!PlanRank.TryGetValue(currentPlan, out int currentRank) ||
            !PlanRank.TryGetValue(requestedPlan, out int requestedRank))
        {
            return false;
        }

        return requestedRank < currentRank;
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
