// <copyright file="LocationLicenseChangedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Integration event raised when a location's license status changes.
/// </summary>
[MemoryPackable]
public partial class LocationLicenseChangedIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the location identifier.
    /// </summary>
    public Guid LocationId { get; set; }

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
    /// Initializes a new instance of the <see cref="LocationLicenseChangedIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public LocationLicenseChangedIntegrationEvent()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocationLicenseChangedIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationId">The location identifier.</param>
    /// <param name="licenseId">The license identifier.</param>
    /// <param name="oldStatus">The old license status.</param>
    /// <param name="newStatus">The new license status.</param>
    /// <param name="plan">The plan name.</param>
    public LocationLicenseChangedIntegrationEvent(
        Guid tenantId,
        Guid locationId,
        Guid licenseId,
        string oldStatus,
        string newStatus,
        string plan)
    {
        TenantId = tenantId;
        LocationId = locationId;
        LicenseId = licenseId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Plan = plan;
    }
}
