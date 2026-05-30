// <copyright file="TenantLicenseChangedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Integration event raised when a tenant's license status changes.
/// </summary>
[MemoryPackable]
public partial class TenantLicenseChangedIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid LicenseId { get; set; }

    /// <summary>
    /// Gets or sets the old license status.
    /// </summary>
    public string OldStatus { get; set; } = default!;

    /// <summary>
    /// Gets or sets the new license status.
    /// </summary>
    public string NewStatus { get; set; } = default!;

    /// <summary>
    /// Gets or sets the plan name.
    /// </summary>
    public string Plan { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantLicenseChangedIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public TenantLicenseChangedIntegrationEvent()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantLicenseChangedIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="licenseId">The license identifier.</param>
    /// <param name="oldStatus">The old license status.</param>
    /// <param name="newStatus">The new license status.</param>
    /// <param name="plan">The plan name.</param>
    public TenantLicenseChangedIntegrationEvent(
        Guid tenantId,
        Guid licenseId,
        string oldStatus,
        string newStatus,
        string plan)
    {
        TenantId = tenantId;
        LicenseId = licenseId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Plan = plan;
    }
}
