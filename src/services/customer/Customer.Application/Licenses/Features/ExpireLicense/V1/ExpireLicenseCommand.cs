// <copyright file="ExpireLicenseCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.ExpireLicense.V1;

/// <summary>
/// Command to expire a license.
/// </summary>
/// <param name="LicenseId">The license identifier.</param>
public sealed record ExpireLicenseCommand(Guid LicenseId)
    : ICommand<ErrorOr<LicenseResponse>>;
