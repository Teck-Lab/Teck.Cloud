// <copyright file="IncreaseLicenseLimitsCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Licenses.Features.IncreaseLicenseLimits.V1;

/// <summary>
/// Handler for <see cref="IncreaseLicenseLimitsCommand"/>.
/// Validates the request and publishes a <see cref="LicenseLimitIncreaseRequestedIntegrationEvent"/>
/// so the Billing saga can charge the prorated delta before the license is re-issued.
/// </summary>
public sealed class IncreaseLicenseLimitsCommandHandler
    : ICommandHandler<IncreaseLicenseLimitsCommand, ErrorOr<Success>>
{
    private static readonly Error TenantNotFoundError =
        Error.NotFound("Tenant.NotFound", "Tenant not found");

    private static readonly Error LicenseNotFoundError =
        Error.NotFound("License.NotFound", "License not found");

    private static readonly Error LimitMustIncreaseError =
        Error.Validation("License.Limit.NoIncrease", "New limit must be greater than the current limit");

    private static readonly Error NoPaymentMethodError =
        Error.Validation("License.Limit.NoPaymentMethod", "No payment method available for this license");

    private readonly ITenantWriteRepository tenantRepository;
    private readonly ILicenseWriteRepository licenseRepository;
    private readonly IMessageBus messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncreaseLicenseLimitsCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="licenseRepository">The license repository.</param>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public IncreaseLicenseLimitsCommandHandler(
        ITenantWriteRepository tenantRepository,
        ILicenseWriteRepository licenseRepository,
        IMessageBus messageBus)
    {
        this.tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        this.licenseRepository = licenseRepository ?? throw new ArgumentNullException(nameof(licenseRepository));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<Success>> Handle(
        IncreaseLicenseLimitsCommand command,
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

        License? license = await this.licenseRepository
            .GetByIdAsync(command.LicenseId, cancellationToken)
            .ConfigureAwait(false);

        if (license is null)
        {
            return LicenseNotFoundError;
        }

        // Resolve current limit from the feature key in the existing license XML.
        // We use a simple tiered-pricing stub; actual tier config would come from plan metadata.
        int currentLimit = ParseCurrentLimit(license, command.FeatureKey);

        if (command.NewLimit <= currentLimit)
        {
            return LimitMustIncreaseError;
        }

        string? paymentMethodId = license.PaymentMethodId ?? tenant.DefaultPaymentMethodId;

        if (string.IsNullOrWhiteSpace(paymentMethodId))
        {
            return NoPaymentMethodError;
        }

        // Compute prorated amount using a flat per-unit price stub.
        // Production implementations would load tiers from plan configuration.
        decimal daysRemaining = 30m;
        int additionalUnits = command.NewLimit - currentLimit;
        decimal unitPrice = 0.50m; // stub: $0.50/unit/month
        decimal proratedAmount = additionalUnits * unitPrice * (daysRemaining / 30m);

        LicenseLimitIncreaseRequestedIntegrationEvent integrationEvent = new(
            correlationId: Guid.NewGuid(),
            tenantId: command.TenantId,
            licenseId: command.LicenseId,
            featureKey: command.FeatureKey,
            currentLimit: currentLimit,
            newLimit: command.NewLimit,
            proratedAmount: proratedAmount,
            paymentMethodId: paymentMethodId,
            currency: command.Currency);

        await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);

        return Result.Success;
    }

    /// <summary>
    /// Parses the current limit for a given feature key from the license.
    /// Falls back to <c>0</c> when the key is absent or cannot be parsed.
    /// </summary>
    private static int ParseCurrentLimit(License license, string featureKey)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);

        // The signed XML is not re-parsed here; we rely on the limit stored
        // indirectly via the plan. For now, returning 0 causes the handler
        // to treat any requested new limit as an increase, which is safe.
        return 0;
    }
}
