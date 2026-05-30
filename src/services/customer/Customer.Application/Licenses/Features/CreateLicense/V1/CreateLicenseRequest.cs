// <copyright file="CreateLicenseRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.CreateLicense.V1;

/// <summary>
/// Request to create a new license.
/// </summary>
public sealed class CreateLicenseRequest
{
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
    /// Gets or sets the payment method identifier.
    /// </summary>
    public string? PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the payment scope.
    /// </summary>
    public string PaymentScope { get; set; } = "TenantDefault";
}
