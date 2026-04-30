// <copyright file="ActivateTenantRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.ActivateTenant.V1;

/// <summary>
/// Request to activate a tenant.
/// </summary>
/// <param name="Id">Tenant identifier.</param>
public sealed record ActivateTenantRequest(Guid Id);
