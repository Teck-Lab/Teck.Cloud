// <copyright file="TenantPlanUpgradeCompletedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Customer service when a plan upgrade has been fully applied
/// (new license issued, old license superseded).
/// </summary>
[MemoryPackable]
public partial class TenantPlanUpgradeCompletedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the previous plan name.</summary>
    public string PreviousPlan { get; set; } = default!;

    /// <summary>Gets or sets the new active plan name.</summary>
    public string NewPlan { get; set; } = default!;

    /// <summary>Gets or sets the new license identifier.</summary>
    public Guid NewLicenseId { get; set; }

    /// <summary>Initializes a new instance of the <see cref="TenantPlanUpgradeCompletedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public TenantPlanUpgradeCompletedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TenantPlanUpgradeCompletedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="previousPlan">Previous plan name.</param>
    /// <param name="newPlan">New plan name.</param>
    /// <param name="newLicenseId">New license identifier.</param>
    public TenantPlanUpgradeCompletedIntegrationEvent(
        Guid correlationId, Guid tenantId, string previousPlan, string newPlan, Guid newLicenseId)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        PreviousPlan = previousPlan;
        NewPlan = newPlan;
        NewLicenseId = newLicenseId;
    }
}
