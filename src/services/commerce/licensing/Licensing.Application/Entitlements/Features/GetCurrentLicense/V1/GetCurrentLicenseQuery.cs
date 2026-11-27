using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.GetCurrentLicense.V1;

/// <summary>Requests the active envelope for one exact binding and desired generation.</summary>
/// <param name="TenantId">The validated organization UUID, also carried by the message envelope.</param>
/// <param name="OrderId">The bound order.</param>
/// <param name="SubscriptionId">The bound subscription.</param>
/// <param name="DesiredRevision">The exact Orders desired entitlement generation.</param>
public sealed record GetCurrentLicenseQuery(string TenantId, Guid OrderId, Guid SubscriptionId, uint DesiredRevision)
    : IQuery<LicenseEnvelopeView>, ITenantMessage;
