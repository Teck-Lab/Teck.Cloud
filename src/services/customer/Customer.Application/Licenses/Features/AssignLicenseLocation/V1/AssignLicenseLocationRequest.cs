// <copyright file="AssignLicenseLocationRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.AssignLicenseLocation.V1;

/// <summary>
/// Request for assigning a license to a location.
/// </summary>
public sealed record AssignLicenseLocationRequest
{
    /// <summary>
    /// Gets the location identifier, or null to unassign.
    /// </summary>
    public string? LocationId { get; init; }
}
