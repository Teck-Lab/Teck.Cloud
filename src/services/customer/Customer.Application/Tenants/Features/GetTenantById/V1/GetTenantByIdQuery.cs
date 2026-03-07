// <copyright file="GetTenantByIdQuery.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Tenants.Features.GetTenantById.V1;

/// <summary>
/// Query to get a tenant by ID.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
public record GetTenantByIdQuery(Guid TenantId) : IQuery<ErrorOr<TenantResponse>>;
