// <copyright file="GetLicensesByTenantIdQuery.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.GetLicensesByTenantId.V1;

/// <summary>
/// Query to get all licenses for a tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
public sealed record GetLicensesByTenantIdQuery(string TenantId)
    : IQuery<ErrorOr<IReadOnlyList<LicenseResponse>>>;
