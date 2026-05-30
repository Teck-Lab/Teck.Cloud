// <copyright file="PlanUpgradeSaga.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Common.Interfaces;
using SharedKernel.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Billing.Application.Billing.Sagas;

/// <summary>
/// Choreography saga that coordinates plan-upgrade payment between the Billing and Customer services.
///
/// Flow:
///   1. Customer publishes <see cref="TenantPlanUpgradeRequestedIntegrationEvent"/> → saga starts.
///   2. Saga charges the payment gateway.
///   3a. Charge succeeds → publishes <see cref="PlanUpgradePaymentSucceededIntegrationEvent"/> → Customer applies upgrade.
///   3b. Charge fails  → publishes <see cref="PlanUpgradePaymentFailedIntegrationEvent"/>  → Customer rolls back.
/// </summary>
public sealed partial class PlanUpgradeSaga : Wolverine.Saga
{
    /// <summary>Gets or sets the saga identity — correlates all messages for this upgrade attempt.</summary>
    [SagaIdentity]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the current plan name recorded when the saga started.</summary>
    public string CurrentPlan { get; set; } = default!;

    /// <summary>Gets or sets the target plan name to apply on success.</summary>
    public string TargetPlan { get; set; } = default!;

    /// <summary>Gets or sets the prorated charge amount.</summary>
    public decimal ProratedAmount { get; set; }

    /// <summary>Gets or sets the payment method identifier.</summary>
    public string PaymentMethodId { get; set; } = default!;

    /// <summary>Gets or sets the ISO 4217 currency code.</summary>
    public string Currency { get; set; } = default!;

    /// <summary>
    /// Starts the saga from an inbound upgrade request, then immediately attempts the charge.
    /// Wolverine creates the saga instance and calls <see cref="Handle(TenantPlanUpgradeRequestedIntegrationEvent, IPaymentGateway, IMessageBus, ILogger{PlanUpgradeSaga}, CancellationToken)"/>
    /// in the same logical step.
    /// </summary>
    /// <param name="evt">The incoming upgrade request event.</param>
    /// <returns>A new saga instance initialised from the event data.</returns>
    public static PlanUpgradeSaga Start(TenantPlanUpgradeRequestedIntegrationEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new PlanUpgradeSaga
        {
            Id = evt.CorrelationId,
            TenantId = evt.TenantId,
            CurrentPlan = evt.CurrentPlan,
            TargetPlan = evt.TargetPlan,
            ProratedAmount = evt.ProratedAmount,
            PaymentMethodId = evt.PaymentMethodId,
            Currency = evt.Currency,
        };
    }

    /// <summary>
    /// Processes the charge. Publishes a success or failure outcome event and completes the saga.
    /// </summary>
    /// <param name="evt">The upgrade request event (re-delivered after Start).</param>
    /// <param name="gateway">Payment gateway abstraction.</param>
    /// <param name="bus">Wolverine message bus for publishing outcome events.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(
        TenantPlanUpgradeRequestedIntegrationEvent evt,
        IPaymentGateway gateway,
        IMessageBus bus,
        ILogger<PlanUpgradeSaga> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(bus);

        string description = $"Plan upgrade {CurrentPlan} → {TargetPlan} for tenant {TenantId}";

        try
        {
            string chargeId = await gateway.ChargeAsync(PaymentMethodId, ProratedAmount, Currency, description, ct)
                .ConfigureAwait(false);

            LogChargeSucceeded(logger, TenantId, CurrentPlan, TargetPlan, chargeId);

            await bus.PublishAsync(new PlanUpgradePaymentSucceededIntegrationEvent(Id, TenantId, TargetPlan, chargeId))
                .ConfigureAwait(false);
        }
        catch (Exception paymentException)
        {
            LogChargeFailed(logger, paymentException, TenantId, CurrentPlan, TargetPlan, paymentException.Message);

            await bus.PublishAsync(new PlanUpgradePaymentFailedIntegrationEvent(Id, TenantId, paymentException.Message))
                .ConfigureAwait(false);
        }

        MarkCompleted();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Plan upgrade payment succeeded for tenant {TenantId}: {CurrentPlan} → {TargetPlan}. ChargeId={ChargeId}")]
    private static partial void LogChargeSucceeded(ILogger logger, Guid tenantId, string currentPlan, string targetPlan, string chargeId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Plan upgrade payment failed for tenant {TenantId}: {CurrentPlan} → {TargetPlan}. Reason={Reason}")]
    private static partial void LogChargeFailed(ILogger logger, Exception exception, Guid tenantId, string currentPlan, string targetPlan, string reason);
}
