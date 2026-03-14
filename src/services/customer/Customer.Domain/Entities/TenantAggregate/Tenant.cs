// <copyright file="Tenant.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.TenantAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Tenant aggregate root - represents a customer tenant in the system.
/// </summary>
public class Tenant : BaseEntity, IAggregateRoot
{
    private static readonly Error IdentifierRequiredError =
        Error.Validation("Tenant.Identifier", "Identifier cannot be empty");

    private static readonly Error NameRequiredError =
        Error.Validation("Tenant.Name", "Name cannot be empty");

    private static readonly Error PlanRequiredError =
        Error.Validation("Tenant.Plan", "Plan cannot be empty");

    private readonly List<TenantDatabaseMetadata> databases = new();

    private Tenant()
    {
    }

    /// <summary>
    /// Gets the tenant identifier (unique name/slug for resolution).
    /// </summary>
    public string Identifier { get; private set; } = default!;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the tenant plan (e.g., "Free", "Pro", "Enterprise").
    /// </summary>
    public string Plan { get; private set; } = default!;

    /// <summary>
    /// Gets the associated Keycloak organization identifier.
    /// </summary>
    public string? KeycloakOrganizationId { get; private set; }

    /// <summary>
    /// Gets the database strategy for this tenant.
    /// </summary>
    public DatabaseStrategy DatabaseStrategy { get; private set; } = default!;

    /// <summary>
    /// Gets the database provider for this tenant.
    /// </summary>
    public DatabaseProvider DatabaseProvider { get; private set; } = default!;

    /// <summary>
    /// Gets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the database metadata for each service.
    /// </summary>
    public IReadOnlyList<TenantDatabaseMetadata> Databases => this.databases.AsReadOnly();

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="args">Tenant creation arguments.</param>
    /// <returns>The created tenant or validation errors.</returns>
    public static ErrorOr<Tenant> Create(TenantCreateArgs args)
    {
        EnsureRequiredArguments(args);
        return CreateValidated(args);
    }

    /// <summary>
    /// Adds database metadata for a service.
    /// </summary>
    /// <param name="args">The database metadata arguments.</param>
    public void AddDatabaseMetadata(TenantDatabaseMetadataArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        TenantDatabaseMetadata metadata = new()
        {
            TenantId = this.Id,
            ServiceName = args.ServiceName,
            WriteEnvVarKey = args.WriteEnvVarKey,
            ReadEnvVarKey = args.ReadEnvVarKey,
            ReadDatabaseMode = args.ReadDatabaseMode,
        };

        this.databases.Add(metadata);
    }

    /// <summary>
    /// Deactivates the tenant.
    /// </summary>
    public void Deactivate()
    {
        this.IsActive = false;
    }

    /// <summary>
    /// Activates the tenant.
    /// </summary>
    public void Activate()
    {
        this.IsActive = true;
    }

    /// <summary>
    /// Sets the external identity provider organization identifier.
    /// </summary>
    /// <param name="organizationId">The external organization identifier.</param>
    public void SetIdentityOrganizationId(string organizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        this.KeycloakOrganizationId = organizationId;
    }

    private static void EnsureRequiredArguments(TenantCreateArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(args.Database);
        ArgumentNullException.ThrowIfNull(args.Database.DatabaseStrategy);
        ArgumentNullException.ThrowIfNull(args.Database.DatabaseProvider);
    }

    private static List<Error> ValidateCreateArguments(TenantCreateArgs args)
    {
        List<Error> errors = [];
        AddIfMissing(errors, args.Identifier, IdentifierRequiredError);
        AddIfMissing(errors, args.Name, NameRequiredError);
        AddIfMissing(errors, args.Plan, PlanRequiredError);
        return errors;
    }

    private static ErrorOr<Tenant> CreateValidated(TenantCreateArgs args)
    {
        List<Error> errors = ValidateCreateArguments(args);
        if (errors.Count > 0)
        {
            return errors;
        }

        Tenant tenant = Instantiate(args);
        TenantCreatedDomainEvent tenantCreatedDomainEvent = BuildCreatedDomainEvent(tenant, args);
        tenant.AddDomainEvent(tenantCreatedDomainEvent);
        return tenant;
    }

    private static Tenant Instantiate(TenantCreateArgs args)
    {
        return new Tenant
        {
            Identifier = args.Identifier,
            Name = args.Name,
            Plan = args.Plan,
            DatabaseStrategy = args.Database.DatabaseStrategy,
            DatabaseProvider = args.Database.DatabaseProvider,
            IsActive = true,
        };
    }

    private static TenantCreatedDomainEvent BuildCreatedDomainEvent(Tenant tenant, TenantCreateArgs args)
    {
        TenantCreatedEventDetails tenantCreatedEventDetails = new()
        {
            TenantId = tenant.Id,
            Identifier = args.Identifier,
            Name = args.Name,
            DatabaseStrategy = args.Database.DatabaseStrategy.Name,
            DatabaseProvider = args.Database.DatabaseProvider.Name,
        };

        return new TenantCreatedDomainEvent(tenantCreatedEventDetails);
    }

    private static void AddIfMissing(List<Error> errors, string? value, Error validationError)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        errors.Add(validationError);
    }
}
