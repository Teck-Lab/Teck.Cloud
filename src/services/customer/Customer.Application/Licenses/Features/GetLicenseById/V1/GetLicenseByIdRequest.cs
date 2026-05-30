// <copyright file="GetLicenseByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.GetLicenseById.V1;

/// <summary>
/// Request to get a license by its identifier.
/// </summary>
public sealed class GetLicenseByIdRequest
{
    /// <summary>
    /// Gets or sets the license identifier.
    /// </summary>
    public Guid Id { get; set; }
}
