// <copyright file="ActivateTenantRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Customer.Api.Endpoints.V1.Tenants.ActivateTenant;

/// <summary>
/// Request to activate a tenant.
/// </summary>
/// <param name="Id">Tenant identifier.</param>
public sealed record ActivateTenantRequest(Guid Id);
