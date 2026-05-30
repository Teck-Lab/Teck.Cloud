// <copyright file="LicenseCreateArgs.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.LicenseAggregate;

/// <summary>
/// Arguments required to create a license.
/// </summary>
public sealed class LicenseCreateArgs
{
    /// <summary>
    /// Gets the tenant ID this license belongs to.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location ID this license applies to, or null for tenant-level licensing.
    /// </summary>
    public string? LocationId { get; init; }

    /// <summary>
    /// Gets the plan name for this license.
    /// </summary>
    public string Plan { get; init; } = string.Empty;

    /// <summary>
    /// Gets the license XML payload.
    /// </summary>
    public string LicenseXml { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expiration date of the license.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the payment method ID, or null if not yet set.
    /// </summary>
    public string? PaymentMethodId { get; init; }

    /// <summary>
    /// Gets the payment scope — "TenantDefault" or "LocationOverride".
    /// </summary>
    public string PaymentScope { get; init; } = "TenantDefault";

    /// <summary>
    /// Gets the ownership type for this license. Defaults to <see cref="LicenseOwnershipType.TenantProvided"/>.
    /// </summary>
    public LicenseOwnershipType? OwnershipType { get; init; }
}
