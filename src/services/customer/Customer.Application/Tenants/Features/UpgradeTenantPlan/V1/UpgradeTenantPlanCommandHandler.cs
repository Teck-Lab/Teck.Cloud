// <copyright file="UpgradeTenantPlanCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pricing;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;

/// <summary>
/// Handler for <see cref="UpgradeTenantPlanCommand"/>.
/// Validates the upgrade is valid and publishes a <see cref="TenantPlanUpgradeRequestedIntegrationEvent"/>
/// so the Billing saga can charge the prorated delta before the plan is applied.
/// </summary>
public sealed class UpgradeTenantPlanCommandHandler : ICommandHandler<UpgradeTenantPlanCommand, ErrorOr<Success>>
{
    private static readonly Error TenantNotFoundError =
        Error.NotFound("Tenant.NotFound", "Tenant not found");

    private static readonly Error SamePlanError =
        Error.Validation("Tenant.Plan.SamePlan", "Target plan is the same as the current plan");

    private static readonly Error DowngradeNotAllowedError =
        Error.Validation("Tenant.Plan.Downgrade", "Plan downgrade is not supported via this endpoint");

    private static readonly Error NoPaymentMethodError =
        Error.Validation("Tenant.Plan.NoPaymentMethod", "Tenant has no default payment method configured");

    private readonly ITenantWriteRepository tenantRepository;
    private readonly IMessageBus messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpgradeTenantPlanCommandHandler"/> class.
    /// </summary>
    /// <param name="tenantRepository">The tenant repository.</param>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public UpgradeTenantPlanCommandHandler(
        ITenantWriteRepository tenantRepository,
        IMessageBus messageBus)
    {
        this.tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
    }

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<Success>> Handle(UpgradeTenantPlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Tenant? tenant = await this.tenantRepository
            .GetByIdAsync(command.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return TenantNotFoundError;
        }

        if (string.Equals(tenant.Plan, command.TargetPlan, StringComparison.OrdinalIgnoreCase))
        {
            return SamePlanError;
        }

        TenantPlan currentPlan = TenantPlan.FromName(tenant.Plan, false);
        TenantPlan targetPlan = TenantPlan.FromName(command.TargetPlan, false);

        if (targetPlan.Value <= currentPlan.Value)
        {
            return DowngradeNotAllowedError;
        }

        if (string.IsNullOrWhiteSpace(tenant.DefaultPaymentMethodId))
        {
            return NoPaymentMethodError;
        }

        // Days remaining is a rough estimate based on a 30-day billing period.
        decimal daysRemaining = 30m;
        decimal proratedAmount = VolumePricingCalculator.CalculatePlanUpgradeDelta(currentPlan, targetPlan, daysRemaining);

        TenantPlanUpgradeRequestedIntegrationEvent integrationEvent = new(
            correlationId: Guid.NewGuid(),
            tenantId: tenant.Id,
            currentPlan: tenant.Plan,
            targetPlan: command.TargetPlan,
            proratedAmount: proratedAmount,
            paymentMethodId: tenant.DefaultPaymentMethodId,
            currency: command.Currency);

        await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);

        return Result.Success;
    }
}
