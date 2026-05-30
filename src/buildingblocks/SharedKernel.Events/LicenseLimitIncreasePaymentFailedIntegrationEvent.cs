// <copyright file="LicenseLimitIncreasePaymentFailedIntegrationEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using MemoryPack;
using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Raised by the Billing service when payment for a license limit increase could not be captured.
/// The Customer service consumes this to roll back any reserved state.
/// </summary>
[MemoryPackable]
public partial class LicenseLimitIncreasePaymentFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Gets or sets the saga correlation identifier.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the license identifier.</summary>
    public Guid LicenseId { get; set; }

    /// <summary>Gets or sets the human-readable failure reason.</summary>
    public string Reason { get; set; } = default!;

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreasePaymentFailedIntegrationEvent"/> class.</summary>
    [MemoryPackConstructor]
    public LicenseLimitIncreasePaymentFailedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LicenseLimitIncreasePaymentFailedIntegrationEvent"/> class.</summary>
    /// <param name="correlationId">Saga correlation identifier.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="licenseId">License identifier.</param>
    /// <param name="reason">Failure reason.</param>
    public LicenseLimitIncreasePaymentFailedIntegrationEvent(
        Guid correlationId, Guid tenantId, Guid licenseId, string reason)
    {
        CorrelationId = correlationId;
        TenantId = tenantId;
        LicenseId = licenseId;
        Reason = reason;
    }
}
