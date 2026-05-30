// <copyright file="UpgradeTenantPlanRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591

namespace Customer.Application.Tenants.Features.UpgradeTenantPlan.V1;

/// <summary>
/// Gets the HTTP request model for upgrading a tenant's plan.
/// </summary>
public sealed class UpgradeTenantPlanRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the target plan name.
    /// </summary>
    public string TargetPlan { get; init; } = string.Empty;

    /// <summary>
    /// Gets the billing currency code.
    /// </summary>
    public string Currency { get; init; } = "USD";
}
