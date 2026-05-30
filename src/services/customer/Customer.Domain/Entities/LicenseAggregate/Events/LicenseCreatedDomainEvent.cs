// <copyright file="LicenseCreatedDomainEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Customer.Domain.Entities.LicenseAggregate.Events;

/// <summary>
/// Domain event raised when a license is created.
/// </summary>
public sealed class LicenseCreatedDomainEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseCreatedDomainEvent"/> class.
    /// </summary>
    /// <param name="licenseId">The license ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="locationId">The location ID, or null for tenant-level.</param>
    /// <param name="plan">The plan name.</param>
    /// <param name="status">The license status.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    public LicenseCreatedDomainEvent(
        Guid licenseId,
        string tenantId,
        string? locationId,
        string plan,
        string status,
        DateTimeOffset expiresAt)
    {
        this.LicenseId = licenseId;
        this.TenantId = tenantId;
        this.LocationId = locationId;
        this.Plan = plan;
        this.Status = status;
        this.ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Gets the license ID.
    /// </summary>
    public Guid LicenseId { get; }

    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the location ID, or null for tenant-level.
    /// </summary>
    public string? LocationId { get; }

    /// <summary>
    /// Gets the plan name.
    /// </summary>
    public string Plan { get; }

    /// <summary>
    /// Gets the license status.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }
}
