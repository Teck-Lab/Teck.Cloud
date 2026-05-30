// <copyright file="LicenseExpiredDomainEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Customer.Domain.Entities.LicenseAggregate.Events;

/// <summary>
/// Domain event raised when a license expires.
/// </summary>
public sealed class LicenseExpiredDomainEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseExpiredDomainEvent"/> class.
    /// </summary>
    /// <param name="licenseId">The license ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="expiredAt">The expiration timestamp.</param>
    /// <param name="gracePeriodEndsAt">The end of the grace period, or null.</param>
    public LicenseExpiredDomainEvent(
        Guid licenseId,
        string tenantId,
        DateTimeOffset expiredAt,
        DateTimeOffset? gracePeriodEndsAt)
    {
        this.LicenseId = licenseId;
        this.TenantId = tenantId;
        this.ExpiredAt = expiredAt;
        this.GracePeriodEndsAt = gracePeriodEndsAt;
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
    /// Gets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiredAt { get; }

    /// <summary>
    /// Gets the grace period end timestamp, or null if no grace period.
    /// </summary>
    public DateTimeOffset? GracePeriodEndsAt { get; }
}
