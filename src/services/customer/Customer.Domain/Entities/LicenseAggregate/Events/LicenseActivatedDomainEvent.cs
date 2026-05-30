// <copyright file="LicenseActivatedDomainEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Customer.Domain.Entities.LicenseAggregate.Events;

/// <summary>
/// Domain event raised when a license is activated.
/// </summary>
public sealed class LicenseActivatedDomainEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseActivatedDomainEvent"/> class.
    /// </summary>
    /// <param name="licenseId">The license ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="activatedAt">The activation timestamp.</param>
    public LicenseActivatedDomainEvent(Guid licenseId, string tenantId, DateTimeOffset activatedAt)
    {
        this.LicenseId = licenseId;
        this.TenantId = tenantId;
        this.ActivatedAt = activatedAt;
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
    /// Gets the activation timestamp.
    /// </summary>
    public DateTimeOffset ActivatedAt { get; }
}
