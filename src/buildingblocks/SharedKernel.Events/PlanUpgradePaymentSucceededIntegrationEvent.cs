// <copyright file="PlanUpgradePaymentSucceededIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Billing service when payment for a plan upgrade has been captured successfully.
/// The Customer service consumes this to apply the plan change and re-issue the license.
/// </summary>
[MemoryPackable]
public partial class PlanUpgradePaymentSucceededIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the target plan name to apply.</summary>
    public string TargetPlan { get; set; } = default!;

    /// <summary>Gets or sets the external charge identifier from the payment provider.</summary>
    public string ChargeId { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="PlanUpgradePaymentSucceededIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public PlanUpgradePaymentSucceededIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PlanUpgradePaymentSucceededIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="targetPlan">Target plan name.</param>
    /// <param name="chargeId">External charge identifier.</param>
    public PlanUpgradePaymentSucceededIntegrationEvent(
        Guid correlationId,
        Guid tenantId,
        string targetPlan,
        string chargeId)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        TargetPlan = targetPlan;
        ChargeId = chargeId;
    }
}
