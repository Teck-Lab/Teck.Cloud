// <copyright file="IncreaseLicenseLimitsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.IncreaseLicenseLimits.V1;

/// <summary>
/// Request to increase the limits on an existing license.
/// </summary>
public sealed class IncreaseLicenseLimitsRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the license identifier.
    /// </summary>
    public Guid LicenseId { get; init; }

    /// <summary>
    /// Gets the feature key whose limit should be increased.
    /// </summary>
    public string FeatureKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the new limit value.
    /// </summary>
    public int NewLimit { get; init; }

    /// <summary>
    /// Gets the currency code.
    /// </summary>
    public string Currency { get; init; } = "USD";
}
