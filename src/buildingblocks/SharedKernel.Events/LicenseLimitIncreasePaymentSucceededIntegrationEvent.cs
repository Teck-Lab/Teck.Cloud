// <copyright file="LicenseLimitIncreasePaymentSucceededIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Billing service when payment for a license limit increase succeeded.
/// The Customer service consumes this to supersede the old license and issue the new one.
/// </summary>
[MemoryPackable]
public partial class LicenseLimitIncreasePaymentSucceededIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier being superseded.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the feature key whose limit was increased.</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the new limit value to apply.</summary>
    public int NewLimit { get; set; }

    /// <summary>Gets or sets the external charge identifier from the payment provider.</summary>
    public string ChargeId { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreasePaymentSucceededIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreasePaymentSucceededIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreasePaymentSucceededIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="licenseId">License identifier being superseded.</param>
    /// <param name="featureKey">Feature key whose limit was increased.</param>
    /// <param name="newLimit">New limit value.</param>
    /// <param name="chargeId">External charge identifier.</param>
    public LicenseLimitIncreasePaymentSucceededIntegrationEvent(
        Guid correlationId,
        Guid tenantId,
        Guid licenseId,
        string featureKey,
        int newLimit,
        string chargeId)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        LicenseId = licenseId;
        FeatureKey = featureKey;
        NewLimit = newLimit;
        ChargeId = chargeId;
    }
}
