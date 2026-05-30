// <copyright file="AssignLicenseLocationCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.AssignLicenseLocation.V1;

/// <summary>
/// Command to assign a license to a location.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
/// <param name="LocationId">The location identifier, or null to unassign.</param>
public sealed record AssignLicenseLocationCommand(
    Guid LicenseId,
    string? LocationId)
    : ICommand<ErrorOr<LicenseResponse>>;
