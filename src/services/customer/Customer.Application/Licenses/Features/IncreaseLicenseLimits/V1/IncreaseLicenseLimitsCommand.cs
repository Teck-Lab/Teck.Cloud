// <copyright file="IncreaseLicenseLimitsCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.IncreaseLicenseLimits.V1;

/// <summary>
/// Command to request an à la carte license limit increase.
/// Publishes an integration event that triggers the Billing saga for prorated payment.
/// </summary>
/// <param name="TenantId">The tenant identifier that owns the license.</param>
/// <param name="LicenseId">The license identifier to increase limits on.</param>
/// <param name="FeatureKey">The feature key whose limit is being increased (e.g. "MaxDevices").</param>
/// <param name="NewLimit">The requested new limit value.</param>
/// <param name="Currency">The ISO 4217 currency code for the charge.</param>
public sealed record IncreaseLicenseLimitsCommand(
    Guid TenantId,
    Guid LicenseId,
    string FeatureKey,
    int NewLimit,
    string Currency)
    : ICommand<ErrorOr<Success>>;
