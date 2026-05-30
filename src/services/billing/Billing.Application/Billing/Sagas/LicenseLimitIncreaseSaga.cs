// <copyright file="LicenseLimitIncreaseSaga.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Billing.Application.Common.Interfaces;
using SharedKernel.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Billing.Application.Billing.Sagas;

/// <summary>
/// Choreography saga that coordinates à la carte license-limit-increase payment.
///
/// Flow:
///   1. Customer publishes <see cref="LicenseLimitIncreaseRequestedIntegrationEvent"/> → saga starts.
///   2. Saga charges the payment gateway for the prorated delta.
///   3a. Charge succeeds → publishes <see cref="LicenseLimitIncreasePaymentSucceededIntegrationEvent"/> → Customer supersedes old license.
///   3b. Charge fails  → publishes <see cref="LicenseLimitIncreasePaymentFailedIntegrationEvent"/>  → Customer keeps existing license.
/// </summary>
public sealed partial class LicenseLimitIncreaseSaga : Wolverine.Saga
{
    /// <summary>Gets or sets the saga identity — correlates all messages for this increase attempt.</summary>
    [SagaIdentity]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier being superseded on success.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the feature key whose limit is being increased.</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the new limit value to apply on success.</summary>
    public int NewLimit { get; set; }

    /// <summary>Gets or sets the prorated charge amount.</summary>
    public decimal ProratedAmount { get; set; }

    /// <summary>Gets or sets the payment method identifier.</summary>
    public string PaymentMethodId { get; set; } = default!;

    /// <summary>Gets or sets the ISO 4217 currency code.</summary>
    public string Currency { get; set; } = default!;

    /// <summary>
    /// Starts the saga from an inbound limit-increase request.
    /// </summary>
    /// <param name="evt">The incoming limit increase event.</param>
    /// <returns>A new saga instance initialised from the event data.</returns>
    public static LicenseLimitIncreaseSaga Start(LicenseLimitIncreaseRequestedIntegrationEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return new LicenseLimitIncreaseSaga
        {
            Id = evt.CorrelationId,
            TenantId = evt.TenantId,
            LicenseId = evt.LicenseId,
            FeatureKey = evt.FeatureKey,
            NewLimit = evt.NewLimit,
            ProratedAmount = evt.ProratedAmount,
            PaymentMethodId = evt.PaymentMethodId,
            Currency = evt.Currency,
        };
    }

    /// <summary>
    /// Processes the prorated charge. Publishes a success or failure outcome event and completes the saga.
    /// </summary>
    /// <param name="evt">The limit increase event (re-delivered after Start).</param>
    /// <param name="gateway">Payment gateway abstraction.</param>
    /// <param name="bus">Wolverine message bus for publishing outcome events.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(
        LicenseLimitIncreaseRequestedIntegrationEvent evt,
        IPaymentGateway gateway,
        IMessageBus bus,
        ILogger<LicenseLimitIncreaseSaga> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(bus);

        string description = $"License limit increase {FeatureKey} → {NewLimit} for tenant {TenantId} license {LicenseId}";

        try
        {
            string chargeId = await gateway.ChargeAsync(PaymentMethodId, ProratedAmount, Currency, description, ct)
                .ConfigureAwait(false);

            LogChargeSucceeded(logger, TenantId, LicenseId, FeatureKey, NewLimit, chargeId);

            await bus.PublishAsync(new LicenseLimitIncreasePaymentSucceededIntegrationEvent(
                    Id, TenantId, LicenseId, FeatureKey, NewLimit, chargeId))
                .ConfigureAwait(false);
        }
        catch (Exception paymentException)
        {
            LogChargeFailed(logger, paymentException, TenantId, LicenseId, FeatureKey, paymentException.Message);

            await bus.PublishAsync(new LicenseLimitIncreasePaymentFailedIntegrationEvent(
                    Id, TenantId, LicenseId, paymentException.Message))
                .ConfigureAwait(false);
        }

        MarkCompleted();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Limit increase payment succeeded for tenant {TenantId}, license {LicenseId}: {FeatureKey} → {NewLimit}. ChargeId={ChargeId}")]
    private static partial void LogChargeSucceeded(ILogger logger, Guid tenantId, Guid licenseId, string featureKey, int newLimit, string chargeId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Limit increase payment failed for tenant {TenantId}, license {LicenseId}: {FeatureKey}. Reason={Reason}")]
    private static partial void LogChargeFailed(ILogger logger, Exception exception, Guid tenantId, Guid licenseId, string featureKey, string reason);
}
