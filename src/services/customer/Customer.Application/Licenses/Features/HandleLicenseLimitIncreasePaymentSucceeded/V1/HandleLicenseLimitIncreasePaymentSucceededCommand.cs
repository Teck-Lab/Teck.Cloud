// <copyright file="HandleLicenseLimitIncreasePaymentSucceededCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.HandleLicenseLimitIncreasePaymentSucceeded.V1;

/// <summary>
/// Command dispatched when the Billing service confirms limit increase payment succeeded.
/// Supersedes the existing license and issues a new one with the updated limit.
/// </summary>
/// <param name="CorrelationId">The saga correlation identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="LicenseId">The license identifier being superseded.</param>
/// <param name="FeatureKey">The feature key whose limit was increased.</param>
/// <param name="NewLimit">The new limit value to apply.</param>
/// <param name="ChargeId">The external charge identifier from the payment provider.</param>
public sealed record HandleLicenseLimitIncreasePaymentSucceededCommand(
    Guid CorrelationId,
    Guid TenantId,
    Guid LicenseId,
    string FeatureKey,
    int NewLimit,
    string ChargeId)
    : ICommand<ErrorOr<Success>>;
