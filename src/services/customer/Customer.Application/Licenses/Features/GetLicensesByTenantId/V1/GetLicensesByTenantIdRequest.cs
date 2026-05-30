// <copyright file="GetLicensesByTenantIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
namespace Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;

/// <summary>
/// Request for getting licenses by tenant ID.
/// </summary>
public sealed record GetLicensesByTenantIdRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;
}
