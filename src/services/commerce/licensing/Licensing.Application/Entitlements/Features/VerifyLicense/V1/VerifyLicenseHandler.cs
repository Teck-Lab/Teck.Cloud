using ErrorOr;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Common;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;
using Wolverine.Attributes;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.VerifyLicense.V1;

/// <summary>Verifies signature, binding, expiry and current durable entitlement for a document.</summary>
[NonTransactional]
public static class VerifyLicenseHandler
{
    private const int MaximumDocumentLength = 65_536;

    /// <summary>Handles the internal verification query.</summary>
    /// <param name="query">The document and expected binding.</param>
    /// <param name="operation">The validated message tenant scope.</param>
    /// <param name="generations">Read-only generation fence.</param>
    /// <param name="grants">Read-only grant history.</param>
    /// <param name="documents">Document verifier.</param>
    /// <param name="orders">Authoritative Orders entitlement check.</param>
    /// <param name="clock">The application UTC clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Effective claims or a stable rejection; dependency failure is an error, not a valid result.</returns>
    public static Task<ErrorOr<LicenseVerificationView>> HandleAsync(
        VerifyLicenseQuery query,
        OperationContext operation,
        IGenericReadRepository<LicenseGeneration, Guid> generations,
        IGenericReadRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IOrdersEntitlementClient orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return VerifyAsync(query, operation, generations, grants, documents, orders, clock, cancellationToken);
    }

    private static Task<ErrorOr<LicenseVerificationView>> VerifyAsync(
        VerifyLicenseQuery query,
        OperationContext operation,
        IGenericReadRepository<LicenseGeneration, Guid> generations,
        IGenericReadRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IOrdersEntitlementClient orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Document)
            || query.Document.Length > MaximumDocumentLength
            || !Guid.TryParseExact(query.ExpectedTenantId, "D", out var expectedTenant)
            || !Guid.TryParseExact(query.ExpectedSubscriptionId, "D", out var expectedSubscription))
        {
            return Task.FromResult<ErrorOr<LicenseVerificationView>>(
                Error.Validation(CommerceErrorCodes.InvalidRequest, "A bounded document and UUID expected binding are required."));
        }

        var expectedTenantId = expectedTenant.ToString("D");
        if (!Guid.TryParseExact(query.TenantId, "D", out var callerTenant)
            || !string.Equals(callerTenant.ToString("D"), expectedTenantId, StringComparison.Ordinal)
            || !string.Equals(operation.Scope.TenantId, expectedTenantId, StringComparison.Ordinal))
        {
            return Task.FromResult<ErrorOr<LicenseVerificationView>>(
                Error.Forbidden(CommerceErrorCodes.Forbidden, "Verification is limited to the caller's tenant."));
        }

        var inspection = documents.Inspect(query.Document);
        if (inspection.Status == LicenseDocumentStatus.UnknownKey)
        {
            return Task.FromResult<ErrorOr<LicenseVerificationView>>(Rejected(LicenseRejection.UnknownKey));
        }

        if (inspection.Status != LicenseDocumentStatus.Valid || inspection.Claims is not { } claims)
        {
            return Task.FromResult<ErrorOr<LicenseVerificationView>>(Rejected(LicenseRejection.InvalidSignature));
        }

        var expectedSubscriptionId = expectedSubscription.ToString("D");
        return VerifyEntitlementAsync(
            claims,
            expectedTenantId,
            expectedSubscriptionId,
            generations,
            grants,
            orders,
            clock,
            cancellationToken);
    }

    private static async Task<ErrorOr<LicenseVerificationView>> VerifyEntitlementAsync(
        LicenseDocumentClaims claims,
        string expectedTenantId,
        string expectedSubscriptionId,
        IGenericReadRepository<LicenseGeneration, Guid> generations,
        IGenericReadRepository<LicenseGrant, Guid> grants,
        IOrdersEntitlementClient orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(claims.TenantId, expectedTenantId, StringComparison.Ordinal)
            || !string.Equals(claims.SubscriptionId.ToString("D"), expectedSubscriptionId, StringComparison.Ordinal))
        {
            return Rejected(LicenseRejection.WrongBinding);
        }

        var utcNow = clock.GetUtcNow().ToUniversalTime();
        if (claims.ExpiresAt.ToUniversalTime() <= utcNow)
        {
            return Rejected(LicenseRejection.Expired);
        }

        var generation = await generations.FindOneAsync(
            candidate => candidate.TenantId == expectedTenantId
                && candidate.OrderId == claims.OrderId
                && candidate.SubscriptionId == claims.SubscriptionId,
            cancellationToken).ConfigureAwait(false);
        if (generation is null || generation.LicenseRevision != claims.LicenseRevision)
        {
            return Rejected(LicenseRejection.Revoked);
        }

        var grant = await LicenseClaimIntegrity.CurrentActiveAsync(grants, generation, utcNow, cancellationToken).ConfigureAwait(false);
        if (grant is null || !LicenseClaimIntegrity.Matches(claims, grant))
        {
            return Rejected(LicenseRejection.Revoked);
        }

        var eligibility = await orders.CheckAsync(expectedTenantId, claims.OrderId, claims.SubscriptionId, generation.DesiredRevision, cancellationToken).ConfigureAwait(false);
        if (eligibility == EntitlementCheckStatus.Unavailable)
        {
            return Error.Failure(CommerceErrorCodes.DependencyUnavailable, "Orders entitlement could not be verified.");
        }

        var eligible = eligibility == EntitlementCheckStatus.Eligible;
        return eligible ? BuildView(claims) : Rejected(LicenseRejection.Revoked);
    }

    private static LicenseVerificationView BuildView(LicenseDocumentClaims claims) => new(
        Valid: true,
        Claims: new LicenseClaimView(
            claims.TenantId,
            claims.OrderId,
            claims.SubscriptionId,
            claims.Tier,
            claims.AddOnCodes,
            claims.LicenseRevision,
            claims.KeyId,
            claims.ExpiresAt),
        Rejection: LicenseRejection.None);

    private static LicenseVerificationView Rejected(LicenseRejection rejection) =>
        new(Valid: false, Claims: null, Rejection: rejection);
}
