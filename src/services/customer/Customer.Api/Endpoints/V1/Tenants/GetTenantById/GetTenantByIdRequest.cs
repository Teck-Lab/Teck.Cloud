// <copyright file="GetTenantByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Customer.Api.Endpoints.V1.Tenants.GetTenantById;

/// <summary>
/// Request to get a tenant by id.
/// </summary>
/// <param name="Id">The tenant id.</param>
public record GetTenantByIdRequest(Guid Id);
