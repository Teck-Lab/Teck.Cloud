// <copyright file="ILicenseIssuer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;

namespace Customer.Application.Common.Interfaces;

/// <summary>
/// Issues signed software licenses.
/// </summary>
public interface ILicenseIssuer
{
    /// <summary>
    /// Issues a new signed license for a tenant or location.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationId">The location identifier, or null for tenant-level licensing.</param>
    /// <param name="plan">The plan name.</param>
    /// <param name="tenantPlan">The tenant plan with quota metadata.</param>
    /// <param name="paymentMethodId">The payment method identifier.</param>
    /// <param name="paymentScope">The payment scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signed license XML string.</returns>
    Task<string> IssueLicenseAsync(
        string tenantId,
        string? locationId,
        string plan,
        TenantPlan tenantPlan,
        string? paymentMethodId,
        string paymentScope,
        CancellationToken cancellationToken);

    /// <summary>
    /// Issues a new signed license with explicit feature overrides on top of plan defaults.
    /// Use this for à la carte limit increases without a full plan change.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationId">The location identifier, or null for tenant-level licensing.</param>
    /// <param name="plan">The plan name.</param>
    /// <param name="tenantPlan">The tenant plan with quota metadata.</param>
    /// <param name="featureOverrides">Feature key/value overrides applied on top of plan defaults.</param>
    /// <param name="paymentMethodId">The payment method identifier.</param>
    /// <param name="paymentScope">The payment scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signed license XML string.</returns>
    Task<string> IssueLicenseWithOverridesAsync(
        string tenantId,
        string? locationId,
        string plan,
        TenantPlan tenantPlan,
        IReadOnlyDictionary<string, string> featureOverrides,
        string? paymentMethodId,
        string paymentScope,
        CancellationToken cancellationToken);
}
