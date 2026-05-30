// <copyright file="License.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.LicenseAggregate;

/// <summary>
/// License aggregate root - represents a software license for a tenant or location.
/// </summary>
public sealed class License : BaseEntity, IAggregateRoot
{
    private static readonly Error TenantIdRequiredError =
        Error.Validation("License.TenantId", "Tenant ID cannot be empty");

    private static readonly Error PlanRequiredError =
        Error.Validation("License.Plan", "Plan cannot be empty");

    private static readonly Error LicenseXmlRequiredError =
        Error.Validation("License.LicenseXml", "License XML cannot be empty");

    private static readonly Error ExpiresAtRequiredError =
        Error.Validation("License.ExpiresAt", "Expiration date must be in the future");

    private License()
    {
    }

    /// <summary>
    /// Gets the tenant identifier this license belongs to.
    /// </summary>
    public string TenantId { get; private set; } = default!;

    /// <summary>
    /// Gets the location identifier this license applies to, or null for tenant-level licensing.
    /// </summary>
    public string? LocationId { get; private set; }

    /// <summary>
    /// Gets the plan name for this license.
    /// </summary>
    public string Plan { get; private set; } = default!;

    /// <summary>
    /// Gets the current status of the license.
    /// </summary>
    public LicenseStatus Status { get; private set; } = default!;

    /// <summary>
    /// Gets the signed license XML payload.
    /// </summary>
    public string LicenseXml { get; private set; } = default!;

    /// <summary>
    /// Gets the expiration date of the license.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the end of the grace period, or null if not applicable.
    /// </summary>
    public DateTimeOffset? GracePeriodEndsAt { get; private set; }

    /// <summary>
    /// Gets the payment method identifier, or null if not yet set.
    /// </summary>
    public string? PaymentMethodId { get; private set; }

    /// <summary>
    /// Gets the payment scope — "TenantDefault" or "LocationOverride".
    /// </summary>
    public string PaymentScope { get; private set; } = default!;

    /// <summary>
    /// Gets the ownership type — who owns and manages this license.
    /// </summary>
    public LicenseOwnershipType OwnershipType { get; private set; } = default!;

    /// <summary>
    /// Gets the date and time when the license was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new license.
    /// </summary>
    /// <param name="args">License creation arguments.</param>
    /// <returns>The created license or validation errors.</returns>
    public static ErrorOr<License> Create(LicenseCreateArgs args)
    {
        EnsureRequiredArguments(args);
        return CreateValidated(args);
    }

    /// <summary>
    /// Activates the license.
    /// </summary>
    public void Activate()
    {
        this.Status = LicenseStatus.Active;
        this.GracePeriodEndsAt = null;
        this.UpdatedAt = DateTimeOffset.UtcNow;

        this.AddDomainEvent(new LicenseActivatedDomainEvent(this.Id, this.TenantId, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Marks the license as expired.
    /// </summary>
    public void Expire()
    {
        this.Status = LicenseStatus.Expired;
        this.UpdatedAt = DateTimeOffset.UtcNow;

        this.AddDomainEvent(new LicenseExpiredDomainEvent(this.Id, this.TenantId, DateTimeOffset.UtcNow, this.GracePeriodEndsAt));
    }

    /// <summary>
    /// Enters the grace period after expiry.
    /// </summary>
    /// <param name="gracePeriod">The duration of the grace period.</param>
    public void EnterGracePeriod(TimeSpan gracePeriod)
    {
        this.Status = LicenseStatus.Grace;
        this.GracePeriodEndsAt = DateTimeOffset.UtcNow.Add(gracePeriod);
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Revokes the license.
    /// </summary>
    public void Revoke()
    {
        this.Status = LicenseStatus.Revoked;
        this.GracePeriodEndsAt = null;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Renews the license with a new expiration date and signed XML.
    /// </summary>
    /// <param name="newLicenseXml">The new signed license XML.</param>
    /// <param name="newExpiry">The new expiration date.</param>
    public void Renew(string newLicenseXml, DateTimeOffset newExpiry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newLicenseXml);

        this.LicenseXml = newLicenseXml;
        this.ExpiresAt = newExpiry;
        this.Status = LicenseStatus.Active;
        this.GracePeriodEndsAt = null;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this license as superseded by a newer one.
    /// </summary>
    public void Supersede()
    {
        this.Status = LicenseStatus.Superseded;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the payment method for this license.
    /// </summary>
    /// <param name="paymentMethodId">The payment method identifier.</param>
    public void SetPaymentMethod(string? paymentMethodId)
    {
        this.PaymentMethodId = paymentMethodId;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the payment scope.
    /// </summary>
    /// <param name="paymentScope">The payment scope — "TenantDefault" or "LocationOverride".</param>
    public void UpdatePaymentScope(string paymentScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentScope);
        this.PaymentScope = paymentScope;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the location identifier for this license.
    /// </summary>
    /// <param name="locationId">The location identifier, or null to unassign.</param>
    public void SetLocationId(string? locationId)
    {
        this.LocationId = locationId;
        this.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void EnsureRequiredArguments(LicenseCreateArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
    }

    private static List<Error> ValidateCreateArguments(LicenseCreateArgs args)
    {
        List<Error> errors = [];
        AddIfMissing(errors, args.TenantId, TenantIdRequiredError);
        AddIfMissing(errors, args.Plan, PlanRequiredError);
        AddIfMissing(errors, args.LicenseXml, LicenseXmlRequiredError);

        if (args.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            errors.Add(ExpiresAtRequiredError);
        }

        return errors;
    }

    private static ErrorOr<License> CreateValidated(LicenseCreateArgs args)
    {
        List<Error> errors = ValidateCreateArguments(args);
        if (errors.Count > 0)
        {
            return errors;
        }

        License license = Instantiate(args);
        LicenseCreatedDomainEvent licenseCreatedDomainEvent = BuildCreatedDomainEvent(license, args);
        license.AddDomainEvent(licenseCreatedDomainEvent);
        return license;
    }

    private static License Instantiate(LicenseCreateArgs args)
    {
        return new License
        {
            TenantId = args.TenantId,
            LocationId = args.LocationId,
            Plan = args.Plan,
            Status = LicenseStatus.Trial,
            LicenseXml = args.LicenseXml,
            ExpiresAt = args.ExpiresAt,
            PaymentMethodId = args.PaymentMethodId,
            PaymentScope = args.PaymentScope,
            OwnershipType = args.OwnershipType ?? LicenseOwnershipType.TenantProvided,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static LicenseCreatedDomainEvent BuildCreatedDomainEvent(License license, LicenseCreateArgs args)
    {
        return new LicenseCreatedDomainEvent(
            license.Id,
            args.TenantId,
            args.LocationId,
            args.Plan,
            LicenseStatus.Trial.Name,
            args.ExpiresAt);
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
