// <copyright file="ActivateLicenseRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.ActivateLicense.V1;

/// <summary>
/// Request to activate a license.
/// </summary>
public sealed class ActivateLicenseRequest
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }
}
