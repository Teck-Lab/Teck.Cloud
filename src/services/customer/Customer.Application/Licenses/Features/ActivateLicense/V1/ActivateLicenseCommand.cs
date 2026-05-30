// <copyright file="ActivateLicenseCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.ActivateLicense.V1;

/// <summary>
/// Command to activate a license.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
public sealed record ActivateLicenseCommand(Guid LicenseId)
    : ICommand<ErrorOr<LicenseResponse>>;
