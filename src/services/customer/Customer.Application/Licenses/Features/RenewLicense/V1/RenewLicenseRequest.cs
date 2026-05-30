// <copyright file="RenewLicenseRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.RenewLicense.V1;

/// <summary>
/// Request to renew a license.
/// </summary>
public sealed class RenewLicenseRequest
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the new plan name.
    /// </summary>
    public string NewPlan { get; set; } = default!;

    /// <summary>
    /// Gets or sets the new expiration date.
    /// </summary>
    public DateTimeOffset NewExpiry { get; set; }
}
