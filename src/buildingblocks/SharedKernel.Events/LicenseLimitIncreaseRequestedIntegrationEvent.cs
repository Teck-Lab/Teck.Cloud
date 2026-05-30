// <copyright file="LicenseLimitIncreaseRequestedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Customer service when a limit increase on an existing license is requested.
/// The Billing service consumes this to charge the prorated delta before re-issuance.
/// </summary>
[MemoryPackable]
public partial class LicenseLimitIncreaseRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier that owns or requested the license change.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier being superseded.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the feature key whose limit is being increased (e.g. "MaxDevices").</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the current limit value.</summary>
    public int CurrentLimit { get; set; }

    /// <summary>Gets or sets the requested new limit value.</summary>
    public int NewLimit { get; set; }

    /// <summary>Gets or sets the prorated amount to charge in the smallest currency unit.</summary>
    public decimal ProratedAmount { get; set; }

    /// <summary>Gets or sets the payment method identifier on the payment provider.</summary>
    public string PaymentMethodId { get; set; } = default!;

    /// <summary>Gets or sets the ISO 4217 currency code.</summary>
    public string Currency { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreaseRequestedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreaseRequestedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreaseRequestedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="licenseId">License identifier.</param>
    /// <param name="featureKey">Feature key being changed.</param>
    /// <param name="currentLimit">Current limit.</param>
    /// <param name="newLimit">New limit.</param>
    /// <param name="proratedAmount">Prorated charge amount.</param>
    /// <param name="paymentMethodId">Payment method identifier.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    public LicenseLimitIncreaseRequestedIntegrationEvent(
        Guid correlationId,
        Guid tenantId,
        Guid licenseId,
        string featureKey,
        int currentLimit,
        int newLimit,
        decimal proratedAmount,
        string paymentMethodId,
        string currency)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        LicenseId = licenseId;
        FeatureKey = featureKey;
        CurrentLimit = currentLimit;
        NewLimit = newLimit;
        ProratedAmount = proratedAmount;
        PaymentMethodId = paymentMethodId;
        Currency = currency;
    }
}
