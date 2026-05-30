// <copyright file="CreateLicenseCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Licenses.Responses;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Customer.Application.Licenses.Features.CreateLicense.V1;

/// <summary>
/// Command to create a new license.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="LocationId">The location identifier, or null for tenant-level licensing.</param>
/// <param name="Plan">The plan name.</param>
/// <param name="PaymentMethodId">The payment method identifier, or null.</param>
/// <param name="PaymentScope">The payment scope — "TenantDefault" or "LocationOverride".</param>
public sealed record CreateLicenseCommand(
    string TenantId,
    string? LocationId,
    string Plan,
    string? PaymentMethodId,
    string PaymentScope)
    : ICommand<ErrorOr<LicenseResponse>>;
