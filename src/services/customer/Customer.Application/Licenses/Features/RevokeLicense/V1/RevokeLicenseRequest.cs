// <copyright file="RevokeLicenseRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.RevokeLicense.V1;

/// <summary>
/// Request to revoke a license.
/// </summary>
public sealed class RevokeLicenseRequest
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }
}
