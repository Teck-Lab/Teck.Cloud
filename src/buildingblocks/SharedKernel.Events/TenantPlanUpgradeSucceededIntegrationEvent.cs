// <copyright file="TenantPlanUpgradeSucceededIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>Integration event raised when a tenant plan upgrade succeeds.</summary>
[MemoryPackable]
public partial class TenantPlanUpgradeSucceededIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the correlation identifier for the operation.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the new plan the tenant is on.</summary>
    public string NewPlan { get; set; } = default!;

    /// <summary>Gets or sets the new license identifier issued for the tenant.</summary>
    public Guid NewLicenseId { get; set; }

    /// <summary>Gets or sets the charge identifier from the billing provider.</summary>
    public string ChargeId { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPlanUpgradeSucceededIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public TenantPlanUpgradeSucceededIntegrationEvent()
    {
        // Required for MemoryPack deserialization
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPlanUpgradeSucceededIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="correlationId">The correlation identifier for the operation.</param>
    /// <param name="newPlan">The new tenant plan.</param>
    /// <param name="newLicenseId">The new license identifier issued for the tenant.</param>
    /// <param name="chargeId">The billing provider charge identifier.</param>
    public TenantPlanUpgradeSucceededIntegrationEvent(Guid tenantId, Guid correlationId, string newPlan, Guid newLicenseId, string chargeId)
    {
        TenantId = tenantId;
        CorrelationId = correlationId;
        NewPlan = newPlan;
        NewLicenseId = newLicenseId;
        ChargeId = chargeId;
    }
}
