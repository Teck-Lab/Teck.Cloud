// <copyright file="PatchCurrentTenantProfileRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.PatchCurrentTenantProfile.V1;

/// <summary>
/// Request to patch the current tenant profile fields.
/// </summary>
public sealed class PatchCurrentTenantProfileRequest
{
    /// <summary>
    /// Gets the optional updated tenant display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the optional updated tenant plan.
    /// </summary>
    public string? Plan { get; init; }
}
