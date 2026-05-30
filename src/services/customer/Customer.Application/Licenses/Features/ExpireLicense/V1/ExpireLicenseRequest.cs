// <copyright file="ExpireLicenseRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.ExpireLicense.V1;

/// <summary>
/// Request to expire a license.
/// </summary>
public sealed class ExpireLicenseRequest
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }
}
