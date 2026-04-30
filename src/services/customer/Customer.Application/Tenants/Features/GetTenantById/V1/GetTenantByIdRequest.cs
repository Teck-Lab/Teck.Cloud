// <copyright file="GetTenantByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.GetTenantById.V1;

/// <summary>
/// Request to get a tenant by id.
/// </summary>
/// <param name="Id">The tenant id.</param>
public sealed record GetTenantByIdRequest(Guid Id);
