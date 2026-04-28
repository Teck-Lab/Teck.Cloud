// <copyright file="DeactivateTenantRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Customer.Api.Endpoints.V1.Tenants.DeactivateTenant;

/// <summary>
/// Request to deactivate a tenant.
/// </summary>
/// <param name="Id">Tenant identifier.</param>
public sealed record DeactivateTenantRequest(Guid Id);
