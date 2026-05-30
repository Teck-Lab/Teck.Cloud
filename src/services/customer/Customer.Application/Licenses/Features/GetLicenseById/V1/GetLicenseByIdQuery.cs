// <copyright file="GetLicenseByIdQuery.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.GetLicenseById.V1;

/// <summary>
/// Query to get a license by its identifier.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
public sealed record GetLicenseByIdQuery(Guid LicenseId)
    : IQuery<ErrorOr<LicenseResponse>>;
