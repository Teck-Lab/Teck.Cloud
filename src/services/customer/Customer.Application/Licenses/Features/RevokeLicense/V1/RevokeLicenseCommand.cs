// <copyright file="RevokeLicenseCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.RevokeLicense.V1;

/// <summary>
/// Command to revoke a license.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
public sealed record RevokeLicenseCommand(Guid LicenseId)
    : ICommand<ErrorOr<LicenseResponse>>;
