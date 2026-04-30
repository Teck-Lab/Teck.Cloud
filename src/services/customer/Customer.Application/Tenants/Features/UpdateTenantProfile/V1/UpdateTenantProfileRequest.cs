// <copyright file="UpdateTenantProfileRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.UpdateTenantProfile.V1;

/// <summary>
/// Request to patch tenant profile fields.
/// </summary>
public sealed class UpdateTenantProfileRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the optional updated tenant display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the optional updated tenant plan.
    /// </summary>
    public string? Plan { get; init; }
}
