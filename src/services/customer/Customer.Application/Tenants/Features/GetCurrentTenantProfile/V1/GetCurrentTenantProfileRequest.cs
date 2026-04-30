// <copyright file="GetCurrentTenantProfileRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.GetCurrentTenantProfile.V1;

/// <summary>
/// Request model for retrieving the current tenant profile.
/// </summary>
public sealed class GetCurrentTenantProfileRequest
{
    /// <summary>
    /// Gets a value indicating whether diagnostics should be included.
    /// </summary>
    public bool IncludeDiagnostics { get; init; }
}
