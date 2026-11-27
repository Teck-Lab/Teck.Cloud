using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.VerifyLicense.V1;

/// <summary>Verifies one signed document against expected binding and current durable entitlement.</summary>
/// <param name="TenantId">The validated caller tenant, also carried by the message envelope.</param>
/// <param name="Document">The untrusted signed document.</param>
/// <param name="ExpectedTenantId">The binding the caller expects.</param>
/// <param name="ExpectedSubscriptionId">The subscription the caller expects.</param>
public sealed record VerifyLicenseQuery(string TenantId, string Document, string ExpectedTenantId, string ExpectedSubscriptionId)
    : IQuery<LicenseVerificationView>, ITenantMessage;
