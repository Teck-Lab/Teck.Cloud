// <copyright file="TenantPlanUpgradeRequestedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Customer service when a tenant requests a plan upgrade.
/// The Billing service consumes this to initiate prorated payment charging.
/// </summary>
[MemoryPackable]
public partial class TenantPlanUpgradeRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the current plan name (before upgrade).</summary>
    public string CurrentPlan { get; set; } = default!;

    /// <summary>Gets or sets the requested target plan name.</summary>
    public string TargetPlan { get; set; } = default!;

    /// <summary>Gets or sets the prorated amount to charge in the smallest currency unit (e.g. cents).</summary>
    public decimal ProratedAmount { get; set; }

    /// <summary>Gets or sets the payment method identifier on the payment provider.</summary>
    public string PaymentMethodId { get; set; } = default!;

    /// <summary>Gets or sets the ISO 4217 currency code.</summary>
    public string Currency { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="TenantPlanUpgradeRequestedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public TenantPlanUpgradeRequestedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TenantPlanUpgradeRequestedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="currentPlan">Current plan name.</param>
    /// <param name="targetPlan">Target plan name.</param>
    /// <param name="proratedAmount">Prorated charge amount.</param>
    /// <param name="paymentMethodId">Payment method identifier.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    public TenantPlanUpgradeRequestedIntegrationEvent(
        Guid correlationId,
        Guid tenantId,
        string currentPlan,
        string targetPlan,
        decimal proratedAmount,
        string paymentMethodId,
        string currency)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        CurrentPlan = currentPlan;
        TargetPlan = targetPlan;
        ProratedAmount = proratedAmount;
        PaymentMethodId = paymentMethodId;
        Currency = currency;
    }
}
