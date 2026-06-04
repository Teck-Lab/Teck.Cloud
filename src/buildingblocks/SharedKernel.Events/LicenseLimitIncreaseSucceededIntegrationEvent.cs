// <copyright file="LicenseLimitIncreaseSucceededIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>Integration event raised when a license limit increase succeeds.</summary>
[MemoryPackable]
public partial class LicenseLimitIncreaseSucceededIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the correlation identifier for the operation.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the feature key for which the limit was increased.</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the new limit for the feature.</summary>
    public int NewLimit { get; set; }

    /// <summary>Gets or sets the charge identifier from the billing provider.</summary>
    public string ChargeId { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseLimitIncreaseSucceededIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreaseSucceededIntegrationEvent()
    {
        // Required for MemoryPack deserialization
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseLimitIncreaseSucceededIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="licenseId">The license identifier.</param>
    /// <param name="correlationId">The correlation identifier for the operation.</param>
    /// <param name="featureKey">The feature key for which the limit was increased.</param>
    /// <param name="newLimit">The new feature limit.</param>
    /// <param name="chargeId">The billing provider charge identifier.</param>
    public LicenseLimitIncreaseSucceededIntegrationEvent(Guid tenantId, Guid licenseId, Guid correlationId, string featureKey, int newLimit, string chargeId)
    {
        TenantId = tenantId;
        LicenseId = licenseId;
        CorrelationId = correlationId;
        FeatureKey = featureKey;
        NewLimit = newLimit;
        ChargeId = chargeId;
    }
}
