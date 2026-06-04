// <copyright file="TenantPlanUpgradeFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>Integration event raised when a tenant plan upgrade fails.</summary>
[MemoryPackable]
public partial class TenantPlanUpgradeFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the correlation identifier for the operation.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the reason the upgrade failed.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Gets or sets the current plan the tenant was on.</summary>
    public string FromPlan { get; set; } = default!;

    /// <summary>Gets or sets the plan the tenant attempted to upgrade to.</summary>
    public string ToPlan { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPlanUpgradeFailedIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public TenantPlanUpgradeFailedIntegrationEvent()
    {
        // Required for MemoryPack deserialization
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPlanUpgradeFailedIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="correlationId">The correlation identifier for the operation.</param>
    /// <param name="reason">The reason the upgrade failed.</param>
    /// <param name="fromPlan">The current plan before the attempted upgrade.</param>
    /// <param name="toPlan">The target plan for the attempted upgrade.</param>
    public TenantPlanUpgradeFailedIntegrationEvent(Guid tenantId, Guid correlationId, string reason, string fromPlan, string toPlan)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        Reason = reason;
        FromPlan = fromPlan;
        ToPlan = toPlan;
    }
}
