// <copyright file="LicenseLimitIncreaseCompletedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Customer service when a license limit increase has been fully applied
/// (new license issued, old license superseded).
/// </summary>
[MemoryPackable]
public partial class LicenseLimitIncreaseCompletedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the superseded license identifier.</summary>
    public Guid OldLicenseId { get; set; }

    /// <summary>Gets or sets the newly issued license identifier.</summary>
    public Guid NewLicenseId { get; set; }

    /// <summary>Gets or sets the feature key whose limit was increased.</summary>
    public string FeatureKey { get; set; } = default!;

    /// <summary>Gets or sets the new limit value.</summary>
    public int NewLimit { get; set; }

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreaseCompletedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreaseCompletedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreaseCompletedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="oldLicenseId">Superseded license identifier.</param>
    /// <param name="newLicenseId">New license identifier.</param>
    /// <param name="featureKey">Feature key.</param>
    /// <param name="newLimit">New limit value.</param>
    public LicenseLimitIncreaseCompletedIntegrationEvent(
        Guid correlationId, Guid tenantId, Guid oldLicenseId, Guid newLicenseId, string featureKey, int newLimit)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        OldLicenseId = oldLicenseId;
        NewLicenseId = newLicenseId;
        FeatureKey = featureKey;
        NewLimit = newLimit;
    }
}
