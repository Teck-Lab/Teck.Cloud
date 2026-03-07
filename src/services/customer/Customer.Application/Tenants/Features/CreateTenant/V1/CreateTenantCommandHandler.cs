// <copyright file="CreateTenantCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#pragma warning disable IDE0005
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.Responses;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Handler for CreateTenantCommand.
/// </summary>
public class CreateTenantCommandHandler : ICommandHandler<CreateTenantCommand, ErrorOr<TenantResponse>>
{
    private static readonly string[] Services = new[] { "catalog", "orders", "customer" };

    private readonly ITenantWriteRepository tenantRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITenantIdentityProvisioningService tenantIdentityProvisioningService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="tenantIdentityProvisioningService">The tenant identity provisioning service.</param>
    public CreateTenantCommandHandler(
        ITenantWriteRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ITenantIdentityProvisioningService tenantIdentityProvisioningService)
    {
        this.tenantRepository = tenantRepository;
        this.unitOfWork = unitOfWork;
        this.tenantIdentityProvisioningService = tenantIdentityProvisioningService;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantResponse>> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ErrorOr<Tenant> tenantResult = await this.PrepareTenantAsync(command, cancellationToken).ConfigureAwait(false);
        if (tenantResult.IsError)
        {
            return tenantResult.Errors;
        }

        ErrorOr<Success> provisioningResult = await this.CompleteProvisioningAsync(command, tenantResult.Value, cancellationToken).ConfigureAwait(false);
        return provisioningResult.IsError ? provisioningResult.Errors : MapToResponse(tenantResult.Value);
    }

    private static ErrorOr<Success> SetupServiceDatabase(
        Tenant tenant,
        string serviceName,
        DatabaseStrategy strategy)
    {
        if (!IsSupportedStrategy(strategy))
        {
            return CreateInvalidStrategyError(strategy);
        }

        ReadDatabaseMode readDatabaseMode = ResolveReadDatabaseMode(strategy);
        TenantDatabaseMetadataArgs metadataArgs = BuildMetadataArgs(tenant, serviceName, readDatabaseMode);
        tenant.AddDatabaseMetadata(metadataArgs);
        return Result.Success;
    }

    private static TenantResponse MapToResponse(Tenant tenant)
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
            Databases = tenant.Databases.Select(database => new TenantDatabaseMetadataResponse
            {
                ServiceName = database.ServiceName,
                WriteEnvVarKey = database.WriteEnvVarKey,
                ReadEnvVarKey = database.ReadEnvVarKey,
                HasSeparateReadDatabase = database.HasSeparateReadDatabase,
            }).ToList(),
            CreatedAt = tenant.CreatedAt,
            UpdatedOn = tenant.UpdatedOn,
        };
    }

    private static TenantCreateArgs MapToCreateArgs(CreateTenantCommand command)
    {
        return new TenantCreateArgs
        {
            Identifier = command.Identifier,
            Name = command.Profile.Name,
            Plan = command.Profile.Plan,
            Database = new TenantCreateDatabaseSettings
            {
                DatabaseStrategy = command.Database.DatabaseStrategy,
                DatabaseProvider = command.Database.DatabaseProvider,
            },
        };
    }

    private static bool IsSupportedStrategy(DatabaseStrategy strategy)
    {
        return strategy == DatabaseStrategy.Shared ||
               strategy == DatabaseStrategy.Dedicated ||
               strategy == DatabaseStrategy.External;
    }

    private static ErrorOr<Success> SetupDatabases(
        Tenant tenant,
        DatabaseStrategy strategy)
    {
        foreach (string serviceName in Services)
        {
            ErrorOr<Success> setupResult = SetupServiceDatabase(tenant, serviceName, strategy);
            if (setupResult.IsError)
            {
                return setupResult;
            }
        }

        return Result.Success;
    }

    private static ErrorOr<Tenant> CreateAggregate(CreateTenantCommand command)
    {
        TenantCreateArgs tenantCreateArgs = MapToCreateArgs(command);
        return Tenant.Create(tenantCreateArgs);
    }

    private static Error CreateInvalidStrategyError(DatabaseStrategy strategy)
    {
        return Error.Validation("Tenant.InvalidStrategy", $"Invalid database strategy: {strategy.Name}");
    }

    private static ReadDatabaseMode ResolveReadDatabaseMode(DatabaseStrategy strategy)
    {
        return strategy == DatabaseStrategy.External
            ? ReadDatabaseMode.SharedWrite
            : ReadDatabaseMode.SeparateRead;
    }

    private static TenantDatabaseMetadataArgs BuildMetadataArgs(
        Tenant tenant,
        string serviceName,
        ReadDatabaseMode readDatabaseMode)
    {
        string writeEnvVarKey = $"ConnectionStrings__Tenants__{tenant.Identifier}__Write";
        string? readEnvVarKey = readDatabaseMode == ReadDatabaseMode.SeparateRead
            ? $"ConnectionStrings__Tenants__{tenant.Identifier}__Read"
            : null;

        return new TenantDatabaseMetadataArgs
        {
            ServiceName = serviceName,
            WriteEnvVarKey = writeEnvVarKey,
            ReadEnvVarKey = readEnvVarKey,
            ReadDatabaseMode = readDatabaseMode,
        };
    }

    private async Task PersistWithIdentityCompensationAsync(
        CreateTenantCommand command,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        string organizationId = await this.tenantIdentityProvisioningService
            .CreateOrganizationAsync(command.Identifier, command.Profile.Name, cancellationToken)
            .ConfigureAwait(false);

        tenant.SetIdentityOrganizationId(organizationId);

        try
        {
            await this.tenantRepository.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
            await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException saveException)
        {
            await this.RollbackIdentityAsync(organizationId, saveException, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RollbackIdentityAsync(
        string organizationId,
        DbUpdateException saveException,
        CancellationToken cancellationToken)
    {
        try
        {
            await this.tenantIdentityProvisioningService
                .DeleteOrganizationAsync(organizationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException deleteException)
        {
            throw new InvalidOperationException(
                $"Tenant persistence failed and identity rollback also failed: {deleteException.Message}",
                deleteException);
        }

        throw new InvalidOperationException(
            "Tenant persistence failed after identity organization provisioning.",
            saveException);
    }

    private async Task<ErrorOr<Success>> ValidatePreconditionsAsync(
        CreateTenantCommand command,
        CancellationToken cancellationToken)
    {
        bool exists = await this.tenantRepository.ExistsByIdentifierAsync(command.Identifier, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return Error.Conflict("Tenant.AlreadyExists", $"Tenant with identifier '{command.Identifier}' already exists");
        }

        return Result.Success;
    }

    private async Task<ErrorOr<Tenant>> PrepareTenantAsync(
        CreateTenantCommand command,
        CancellationToken cancellationToken)
    {
        ErrorOr<Success> preconditionsResult = await this.ValidatePreconditionsAsync(command, cancellationToken).ConfigureAwait(false);
        if (preconditionsResult.IsError)
        {
            return preconditionsResult.Errors;
        }

        return CreateAggregate(command);
    }

    private async Task<ErrorOr<Success>> CompleteProvisioningAsync(
        CreateTenantCommand command,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        ErrorOr<Success> databaseSetupResult = SetupDatabases(tenant, command.Database.DatabaseStrategy);
        if (databaseSetupResult.IsError)
        {
            return databaseSetupResult;
        }

        await this.PersistWithIdentityCompensationAsync(command, tenant, cancellationToken).ConfigureAwait(false);
        return Result.Success;
    }
}
