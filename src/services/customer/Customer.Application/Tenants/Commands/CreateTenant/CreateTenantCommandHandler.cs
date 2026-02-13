using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Models;
using SharedKernel.Core.Pricing;


namespace Customer.Application.Tenants.Commands.CreateTenant;

/// <summary>
/// Handler for CreateTenantCommand.
/// </summary>
public class CreateTenantCommandHandler : ICommandHandler<CreateTenantCommand, ErrorOr<TenantDto>>
{
    private static readonly string[] Services = ["catalog", "orders", "customer"];

    private readonly ITenantWriteRepository _tenantRepository;
    private readonly Customer.Application.Common.Interfaces.IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
public CreateTenantCommandHandler(
        ITenantWriteRepository tenantRepository,
        Customer.Application.Common.Interfaces.IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<TenantDto>> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        // Check if tenant already exists
        var exists = await _tenantRepository.ExistsByIdentifierAsync(command.Identifier, cancellationToken);
        if (exists)
        {
            return Error.Conflict("Tenant.AlreadyExists", $"Tenant with identifier '{command.Identifier}' already exists");
        }

        // Create tenant aggregate
        var tenantResult = Tenant.Create(
            command.Identifier,
            command.Name,
            command.Plan,
            command.DatabaseStrategy,
            command.DatabaseProvider);

        if (tenantResult.IsError)
        {
            return tenantResult.Errors;
        }

        var tenant = tenantResult.Value;

        // Process each service
        foreach (var serviceName in Services)
        {
            var setupResult = await SetupServiceDatabaseAsync(
                tenant,
                serviceName,
                command.DatabaseStrategy,
                command.CustomCredentials);


            if (setupResult.IsError)
            {
                return setupResult.Errors;
            }
        }

        // Save tenant
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var dto = MapToDto(tenant);

        return dto;
    }

private static Task<ErrorOr<Success>> SetupServiceDatabaseAsync(
        Tenant tenant,
        string serviceName,
        DatabaseStrategy strategy,
        DatabaseCredentials? customCredentials)

    {
        bool hasSeparateReadDatabase = false;

        if (strategy == DatabaseStrategy.Shared)
        {
            hasSeparateReadDatabase = true;
        }
        else if (strategy == DatabaseStrategy.Dedicated)
        {
            hasSeparateReadDatabase = true;
        }
        else if (strategy == DatabaseStrategy.External)
        {
            if (customCredentials == null)
            {
                return Task.FromResult<ErrorOr<Success>>(Error.Validation("Tenant.ExternalCredentialsRequired", "Custom credentials are required for External database strategy"));
            }

            hasSeparateReadDatabase = false;
        }
        else
        {
            return Task.FromResult<ErrorOr<Success>>(Error.Validation("Tenant.InvalidStrategy", $"Invalid database strategy: {strategy.Name}"));
        }

        var writeEnvVarKey = $"ConnectionStrings__Tenants__{tenant.Identifier}__Write";
        string? readEnvVarKey = hasSeparateReadDatabase ? $"ConnectionStrings__Tenants__{tenant.Identifier}__Read" : null;

        tenant.AddDatabaseMetadata(serviceName, writeEnvVarKey, readEnvVarKey, hasSeparateReadDatabase);

        return Task.FromResult<ErrorOr<Success>>(Result.Success);

    }



    private static TenantDto MapToDto(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Plan = tenant.Plan,
            DatabaseStrategy = tenant.DatabaseStrategy.Name,
            DatabaseProvider = tenant.DatabaseProvider.Name,
            IsActive = tenant.IsActive,
            Databases = tenant.Databases.Select(database => new TenantDatabaseMetadataDto
            {
                ServiceName = database.ServiceName,
                WriteEnvVarKey = database.WriteEnvVarKey,
                ReadEnvVarKey = database.ReadEnvVarKey,
                HasSeparateReadDatabase = database.HasSeparateReadDatabase
            }).ToList(),
            CreatedAt = tenant.CreatedAt,
            UpdatedOn = tenant.UpdatedOn
        };
    }
}
