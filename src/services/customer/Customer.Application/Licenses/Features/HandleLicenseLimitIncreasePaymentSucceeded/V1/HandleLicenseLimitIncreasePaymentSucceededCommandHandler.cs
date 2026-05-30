// <copyright file="HandleLicenseLimitIncreasePaymentSucceededCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Common.Interfaces;
using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;

namespace Customer.Application.Licenses.Features.HandleLicenseLimitIncreasePaymentSucceeded.V1;

/// <summary>
/// Handler for <see cref="HandleLicenseLimitIncreasePaymentSucceededCommand"/>.
/// Supersedes the existing license and issues a new one with the increased feature limit applied.
/// </summary>
public sealed class HandleLicenseLimitIncreasePaymentSucceededCommandHandler
    : ICommandHandler<HandleLicenseLimitIncreasePaymentSucceededCommand, ErrorOr<Success>>
{
    private static readonly Error LicenseNotFoundError =
        Error.NotFound("License.NotFound", "License not found");

    private static readonly Error TenantNotFoundError =
        Error.NotFound("Tenant.NotFound", "Tenant not found");

    private readonly ILicenseWriteRepository licenseRepository;
    private readonly ITenantWriteRepository tenantRepository;
    private readonly ILicenseIssuer licenseIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleLicenseLimitIncreasePaymentSucceededCommandHandler"/> class.
    /// </summary>
    /// <param name="licenseRepository">The license repository.</param>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="licenseIssuer">The license issuer service.</param>
    public HandleLicenseLimitIncreasePaymentSucceededCommandHandler(
        ILicenseWriteRepository licenseRepository,
        ITenantWriteRepository tenantRepository,
        ILicenseIssuer licenseIssuer)
    {
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
        this.tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        this.licenseIssuer = licenseIssuer ?? throw new ArgumentNullException(nameof(licenseIssuer));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<Success>> Handle(
        HandleLicenseLimitIncreasePaymentSucceededCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        License? oldLicense = await this.licenseRepository
            .GetByIdAsync(command.LicenseId, cancellationToken)
            .ConfigureAwait(false);

        if (oldLicense is null)
        {
            return LicenseNotFoundError;
        }

        Tenant? tenant = await this.tenantRepository
            .GetByIdAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return TenantNotFoundError;
        }

        // Mark the old license as superseded.
        oldLicense.Supersede();
        await this.licenseRepository.UpdateAsync(oldLicense, cancellationToken).ConfigureAwait(false);

        // Build the feature override: apply the new limit on top of the plan defaults.
        TenantPlan tenantPlan = TenantPlan.FromName(oldLicense.Plan, false);
        IReadOnlyDictionary<string, string> featureOverrides = new Dictionary<string, string>
        {
            [command.FeatureKey] = command.NewLimit.ToString(),
        };

        string licenseXml = await this.licenseIssuer.IssueLicenseWithOverridesAsync(
            tenantId: oldLicense.TenantId,
            locationId: oldLicense.LocationId,
            plan: oldLicense.Plan,
            tenantPlan: tenantPlan,
            featureOverrides: featureOverrides,
            paymentMethodId: oldLicense.PaymentMethodId ?? tenant.DefaultPaymentMethodId,
            paymentScope: oldLicense.PaymentScope,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiresAt = tenantPlan.IsTrial
            ? DateTimeOffset.UtcNow.Add(TenantPlan.TrialDuration)
            : DateTimeOffset.UtcNow.AddYears(1);

        ErrorOr<License> newLicenseResult = License.Create(new LicenseCreateArgs
        {
            TenantId = oldLicense.TenantId,
            LocationId = oldLicense.LocationId,
            Plan = oldLicense.Plan,
            LicenseXml = licenseXml,
            ExpiresAt = expiresAt,
            PaymentMethodId = oldLicense.PaymentMethodId ?? tenant.DefaultPaymentMethodId,
            PaymentScope = oldLicense.PaymentScope,
            OwnershipType = oldLicense.OwnershipType,
        });

        if (newLicenseResult.IsError)
        {
            return newLicenseResult.Errors;
        }

        License newLicense = newLicenseResult.Value;
        newLicense.Activate();

        await this.licenseRepository.AddAsync(newLicense, cancellationToken).ConfigureAwait(false);

        // If this was a tenant-level license, re-assign the new license to the tenant.
        if (oldLicense.LocationId is null)
        {
            tenant.AssignLicense(newLicense.Id);
            this.tenantRepository.Update(tenant);
        }

        return Result.Success;
    }
}
