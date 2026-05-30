// <copyright file="PlanUpgradePaymentFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Billing service when payment for a plan upgrade could not be captured.
/// The Customer service consumes this to roll back any in-flight state.
/// </summary>
[MemoryPackable]
public partial class PlanUpgradePaymentFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the human-readable failure reason.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="PlanUpgradePaymentFailedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public PlanUpgradePaymentFailedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PlanUpgradePaymentFailedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="reason">Failure reason.</param>
    public PlanUpgradePaymentFailedIntegrationEvent(Guid correlationId, Guid tenantId, string reason)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        Reason = reason;
    }
}
