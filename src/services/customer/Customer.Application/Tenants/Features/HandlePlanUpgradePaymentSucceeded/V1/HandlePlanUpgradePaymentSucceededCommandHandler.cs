// <copyright file="HandlePlanUpgradePaymentSucceededCommandHandler.cs" company="TeckLab">
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

namespace Customer.Application.Tenants.Features.HandlePlanUpgradePaymentSucceeded.V1;

/// <summary>
/// Handler for <see cref="HandlePlanUpgradePaymentSucceededCommand"/>.
/// Applies the plan upgrade to the tenant aggregate, supersedes the current license,
/// and issues a new license reflecting the upgraded plan.
/// </summary>
public sealed class HandlePlanUpgradePaymentSucceededCommandHandler
    : ICommandHandler<HandlePlanUpgradePaymentSucceededCommand, ErrorOr<Success>>
{
    private static readonly Error TenantNotFoundError =
        Error.NotFound("Tenant.NotFound", "Tenant not found");

    private readonly ITenantWriteRepository tenantRepository;
    private readonly ILicenseWriteRepository licenseRepository;
    private readonly ILicenseIssuer licenseIssuer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlePlanUpgradePaymentSucceededCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="licenseRepository">The license repository.</param>
    /// <param name="licenseIssuer">The license issuer service.</param>
    public HandlePlanUpgradePaymentSucceededCommandHandler(
        ITenantWriteRepository tenantRepository,
        ILicenseWriteRepository licenseRepository,
        ILicenseIssuer licenseIssuer)
    {
        this.tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
        this.licenseIssuer = licenseIssuer ?? throw new ArgumentNullException(nameof(licenseIssuer));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<Success>> Handle(
        HandlePlanUpgradePaymentSucceededCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Tenant? tenant = await this.tenantRepository
            .GetByIdAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return TenantNotFoundError;
        }

        // Supersede the existing tenant-level license if one is assigned.
        if (tenant.CurrentLicenseId.HasValue)
        {
            License? oldLicense = await this.licenseRepository
                .GetByIdAsync(tenant.CurrentLicenseId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (oldLicense is not null)
            {
                oldLicense.Supersede();
                await this.licenseRepository.UpdateAsync(oldLicense, cancellationToken).ConfigureAwait(false);
            }
        }

        // Apply the plan upgrade on the tenant aggregate.
        tenant.UpgradePlan(command.TargetPlan);
        this.tenantRepository.Update(tenant);

        // Issue a new license for the upgraded plan.
        TenantPlan newTenantPlan = TenantPlan.FromName(command.TargetPlan, false);

        string licenseXml = await this.licenseIssuer.IssueLicenseAsync(
            tenantId: tenant.Id.ToString("D"),
            locationId: null,
            plan: command.TargetPlan,
            tenantPlan: newTenantPlan,
            paymentMethodId: tenant.DefaultPaymentMethodId,
            paymentScope: PaymentScope.TenantDefault.Name,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiresAt = newTenantPlan.IsTrial
            ? DateTimeOffset.UtcNow.Add(TenantPlan.TrialDuration)
            : DateTimeOffset.UtcNow.AddYears(1);

        ErrorOr<License> newLicenseResult = License.Create(new LicenseCreateArgs
        {
            TenantId = tenant.Id.ToString("D"),
            LocationId = null,
            Plan = command.TargetPlan,
            LicenseXml = licenseXml,
            ExpiresAt = expiresAt,
            PaymentMethodId = tenant.DefaultPaymentMethodId,
            PaymentScope = PaymentScope.TenantDefault.Name,
            OwnershipType = LicenseOwnershipType.TenantProvided,
        });

        if (newLicenseResult.IsError)
        {
            return newLicenseResult.Errors;
        }

        License newLicense = newLicenseResult.Value;
        newLicense.Activate();

        await this.licenseRepository.AddAsync(newLicense, cancellationToken).ConfigureAwait(false);

        tenant.AssignLicense(newLicense.Id);
        this.tenantRepository.Update(tenant);

        return Result.Success;
    }
}
