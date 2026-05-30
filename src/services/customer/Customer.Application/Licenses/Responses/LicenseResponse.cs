// <copyright file="LicenseResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Licenses.Responses;

/// <summary>
/// Response model for license operations.
/// </summary>
public sealed class LicenseResponse
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the location identifier, or null for tenant-level.
    /// </summary>
    public string? LocationId { get; set; }

    /// <summary>
    /// Gets or sets the plan name.
    /// </summary>
    public string Plan { get; set; } = default!;

    /// <summary>
    /// Gets or sets the license status.
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Gets or sets the expiration date.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the grace period end date, or null.
    /// </summary>
    public DateTimeOffset? GracePeriodEndsAt { get; set; }

    /// <summary>
    /// Gets or sets the payment method identifier.
    /// </summary>
    public string? PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the payment scope.
    /// </summary>
    public string PaymentScope { get; set; } = default!;

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last updated date.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
