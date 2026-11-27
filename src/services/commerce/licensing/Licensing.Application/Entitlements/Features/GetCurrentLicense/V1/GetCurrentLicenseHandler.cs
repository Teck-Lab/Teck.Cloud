using ErrorOr;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Common;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Features.GetCurrentLicense.V1;

/// <summary>Resolves the exact current active grant only after re-checking Orders' durable facts.</summary>
public static class GetCurrentLicenseHandler
{
    /// <summary>Handles the internal licensing envelope query.</summary>
    /// <param name="query">The exact binding and desired generation.</param>
    /// <param name="operation">The validated message tenant scope.</param>
    /// <param name="generations">Read-only generation fence.</param>
    /// <param name="grants">Read-only grant history.</param>
    /// <param name="documents">Document integrity verifier.</param>
    /// <param name="orders">Authoritative Orders entitlement check.</param>
    /// <param name="clock">The application UTC clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The current envelope, a scoped not-found, or a sanitized dependency failure.</returns>
    public static Task<ErrorOr<LicenseEnvelopeView>> HandleAsync(
        GetCurrentLicenseQuery query,
        OperationContext operation,
        IGenericReadRepository<LicenseGeneration, Guid> generations,
        IGenericReadRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IOrdersEntitlementClient orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ResolveAsync(query, operation, generations, grants, documents, orders, clock, cancellationToken);
    }

    private static async Task<ErrorOr<LicenseEnvelopeView>> ResolveAsync(
        GetCurrentLicenseQuery query,
        OperationContext operation,
        IGenericReadRepository<LicenseGeneration, Guid> generations,
        IGenericReadRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IOrdersEntitlementClient orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(query.TenantId, "D", out var tenant)
            || query.OrderId == Guid.Empty
            || query.SubscriptionId == Guid.Empty
            || query.DesiredRevision == 0)
        {
            return Error.Validation(CommerceErrorCodes.InvalidRequest, "A UUID binding and positive desired generation are required.");
        }

        var tenantId = tenant.ToString("D");
        if (!string.Equals(operation.Scope.TenantId, tenantId, StringComparison.Ordinal))
        {
            return Error.Forbidden(CommerceErrorCodes.Forbidden, "The requested binding is outside the validated tenant scope.");
        }

        var utcNow = clock.GetUtcNow().ToUniversalTime();
        var generation = await generations.FindOneAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.OrderId == query.OrderId
                && candidate.SubscriptionId == query.SubscriptionId,
            cancellationToken).ConfigureAwait(false);
        if (generation is null || generation.DesiredRevision != query.DesiredRevision)
        {
            return Error.NotFound(CommerceErrorCodes.NotFound, "No active grant matches the desired generation.");
        }

        var grant = await LicenseClaimIntegrity.CurrentActiveAsync(grants, generation, utcNow, cancellationToken).ConfigureAwait(false);
        if (grant is null || grant.Document is not { } document)
        {
            return Error.NotFound(CommerceErrorCodes.NotFound, "No active grant matches the desired generation.");
        }

        var inspection = documents.Inspect(document);
        if (inspection.Status != LicenseDocumentStatus.Valid
            || inspection.Claims is not { } claims
            || !LicenseClaimIntegrity.Matches(claims, grant))
        {
            return Error.NotFound(CommerceErrorCodes.NotFound, "No active grant matches the desired generation.");
        }

        var eligibility = await orders.CheckAsync(tenantId, query.OrderId, query.SubscriptionId, generation.DesiredRevision, cancellationToken).ConfigureAwait(false);
        if (eligibility == EntitlementCheckStatus.NotEligible)
        {
            return Error.NotFound(CommerceErrorCodes.NotFound, "Orders reports the generation is not currently entitled.");
        }

        var eligible = eligibility == EntitlementCheckStatus.Eligible;
        return eligible
            ? new LicenseEnvelopeView(document, grant.KeyId!, grant.LicenseRevision!.Value, grant.ExpiresAt!.Value)
            : Error.Failure(CommerceErrorCodes.DependencyUnavailable, "Orders entitlement could not be verified.");
    }
}
