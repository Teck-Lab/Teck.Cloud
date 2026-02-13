using Customer.Application.Common.Interfaces;
using Customer.Application.Tenants.DTOs;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;
using SharedKernel.Core.Database;

namespace Customer.Application.Tenants.Commands.CreateTenant;

/// <summary>
/// Handler for CreateTenantCommand.
/// </summary>
public class CreateTenantCommandHandler : ICommandHandler<CreateTenantCommand, ErrorOr<TenantDto>>
{
    private static readonly string[] Services = ["catalog", "orders", "customer"];

    private readonly ITenantWriteRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTenantCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public CreateTenantCommandHandler(
        ITenantWriteRepository tenantRepository,
        IUnitOfWork unitOfWork)
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
                command.DatabaseProvider,
                command.CustomCredentials,
                cancellationToken);

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

    private async Task<ErrorOr<Success>> SetupServiceDatabaseAsync(
        Tenant tenant,
        string serviceName,
        DatabaseStrategy strategy,
        DatabaseProvider provider,
        DatabaseCredentials? customCredentials,
        CancellationToken cancellationToken)
    {
        bool hasSeparateReadDatabase = false;

        if (strategy == DatabaseStrategy.Shared)
        {
            // Shared database - runtime will supply DSNs via environment variables; nothing to persist here.
            hasSeparateReadDatabase = true;
        }
        else if (strategy == DatabaseStrategy.Dedicated)
        {
            // Dedicated database - assume external provisioning will populate env vars for this tenant/service.
            hasSeparateReadDatabase = true;
        }
        else if (strategy == DatabaseStrategy.External)
        {
            // External database - use provided credentials (do not persist to Vault)
            if (customCredentials == null)
            {
                return Error.Validation("Tenant.ExternalCredentialsRequired", "Custom credentials are required for External database strategy");
            }

            hasSeparateReadDatabase = false;
        }
        else
        {
            return Error.Validation("Tenant.InvalidStrategy", $"Invalid database strategy: {strategy.Name}");
        }

        // Build environment variable keys for runtime DSN resolution
        var writeEnvVarKey = $"ConnectionStrings__Tenants__{tenant.Identifier}__Write";
        string? readEnvVarKey = hasSeparateReadDatabase ? $"ConnectionStrings__Tenants__{tenant.Identifier}__Read" : null;

        // Add database metadata to tenant (store env-var keys for runtime resolution)
        tenant.AddDatabaseMetadata(serviceName, writeEnvVarKey, readEnvVarKey, hasSeparateReadDatabase);

        return Result.Success;
    }

    private static DatabaseCredentials GenerateCredentials(
        string serviceName,
        DatabaseProvider provider,
        DatabaseStrategy strategy,
        string tenantIdentifier = "",
        bool isReadOnly = false)
    {
        var suffix = isReadOnly ? "_ro" : "_rw";
        var strategyPrefix = strategy == DatabaseStrategy.Shared ? "shared" : tenantIdentifier;

        var username = $"{strategyPrefix}_{serviceName}_user{suffix}";
        var password = GenerateSecurePassword();
        var host = "localhost"; // Default, will be overridden in environment
        var port = provider.DefaultPort;
        var databaseName = strategy == DatabaseStrategy.Shared
            ? $"{serviceName}_shared"
            : $"{serviceName}_{tenantIdentifier.Replace("-", "_", StringComparison.Ordinal)}";

        return new DatabaseCredentials
        {
            Admin = new UserCredentials
            {
                Username = username,
                Password = password
            },
            Application = new UserCredentials
            {
                Username = username,
                Password = password
            },
            Host = host,
            Port = port,
            Database = databaseName,
            Provider = provider.Name
        };
    }

    private static string GenerateSecurePassword()
    {
        // In production, use a proper secure password generator
        // For now, generate a random GUID-based password
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "x", StringComparison.Ordinal)
            .Replace("/", "y", StringComparison.Ordinal)
            .Replace("=", "z", StringComparison.Ordinal);
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
