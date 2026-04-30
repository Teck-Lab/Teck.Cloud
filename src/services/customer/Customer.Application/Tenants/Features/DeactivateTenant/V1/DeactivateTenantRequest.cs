// <copyright file="DeactivateTenantRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.DeactivateTenant.V1;

/// <summary>
/// Request to deactivate a tenant.
/// </summary>
/// <param name="Id">Tenant identifier.</param>
public sealed record DeactivateTenantRequest(Guid Id);
