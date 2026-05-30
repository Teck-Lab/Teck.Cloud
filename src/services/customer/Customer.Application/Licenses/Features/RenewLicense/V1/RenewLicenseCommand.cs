// <copyright file="RenewLicenseCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.RenewLicense.V1;

/// <summary>
/// Command to renew a license with a new expiration date.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
/// <param name="NewPlan">The new plan name.</param>
/// <param name="NewExpiry">The new expiration date.</param>
public sealed record RenewLicenseCommand(
    Guid LicenseId,
    string NewPlan,
    DateTimeOffset NewExpiry)
    : ICommand<ErrorOr<LicenseResponse>>;
