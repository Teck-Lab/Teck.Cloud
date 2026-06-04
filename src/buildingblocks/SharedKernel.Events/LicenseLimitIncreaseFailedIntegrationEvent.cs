// <copyright file="LicenseLimitIncreaseFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>Integration event raised when a license limit increase fails.</summary>
[MemoryPackable]
public partial class LicenseLimitIncreaseFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the correlation identifier for the operation.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the feature key for which the limit increase was attempted.</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the reason the increase failed.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseLimitIncreaseFailedIntegrationEvent"/> class.
    /// </summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreaseFailedIntegrationEvent()
    {
        // Required for MemoryPack deserialization
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseLimitIncreaseFailedIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="licenseId">The license identifier.</param>
    /// <param name="correlationId">The correlation identifier for the operation.</param>
    /// <param name="featureKey">The feature key for which the increase failed.</param>
    /// <param name="reason">The reason the increase failed.</param>
    public LicenseLimitIncreaseFailedIntegrationEvent(Guid tenantId, Guid licenseId, Guid correlationId, string featureKey, string reason)
    {
        TenantId = tenantId;
        LicenseId = licenseId;
        CorrelationId = correlationId;
        FeatureKey = featureKey;
        Reason = reason;
    }
}
