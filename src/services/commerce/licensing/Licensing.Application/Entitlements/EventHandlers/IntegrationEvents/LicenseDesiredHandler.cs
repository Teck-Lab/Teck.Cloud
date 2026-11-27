using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Common;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Specifications;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;
using Wolverine;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.EventHandlers.IntegrationEvents;

/// <summary>Applies one durable desired-state snapshot with monotonic generation fencing.</summary>
public static class LicenseDesiredHandler
{
    /// <summary>Handles the Orders desired-entitlement event.</summary>
    /// <param name="message">The complete desired snapshot.</param>
    /// <param name="operation">The validated message tenant scope.</param>
    /// <param name="generations">Tracked generation fences.</param>
    /// <param name="grants">Tracked grant snapshots.</param>
    /// <param name="documents">Signing service for new active grants.</param>
    /// <param name="messaging">The durable transactional outbox context.</param>
    /// <param name="unitOfWork">The shared write save boundary.</param>
    /// <param name="clock">The application UTC clock.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The handler task.</returns>
    public static Task HandleAsync(
        LicenseDesiredEvent message,
        OperationContext operation,
        IGenericWriteRepository<LicenseGeneration, Guid> generations,
        IGenericWriteRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IMessageContext messaging,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ApplyAsync(message, operation, generations, grants, documents, messaging, unitOfWork, clock, cancellationToken);
    }

    private static async Task ApplyAsync(
        LicenseDesiredEvent message,
        OperationContext operation,
        IGenericWriteRepository<LicenseGeneration, Guid> generations,
        IGenericWriteRepository<LicenseGrant, Guid> grants,
        ILicenseDocumentService documents,
        IMessageContext messaging,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var tenantId = message.TenantId.ToString("D");
        ValidateSnapshot(message, operation, tenantId);
        var tier = ParseTier(message.Tier);
        var addOnCodes = message.AddOnCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var now = clock.GetUtcNow();
        var generation = await LoadOrStartGenerationAsync(tenantId, message, generations, cancellationToken).ConfigureAwait(false);
        if (!generation.CanAccept(message.Revision))
        {
            return;
        }

        var notAfter = message.Enabled && message.NotAfter is { } expiry
            ? LicenseClaimIntegrity.ToMicroseconds(expiry)
            : (DateTimeOffset?)null;
        var (grant, state) = notAfter is { } expiresAt && expiresAt > now
            ? IssueActive(message, tenantId, tier, addOnCodes, expiresAt, now, documents, generation)
            : IssueInactive(message, tenantId, tier, addOnCodes, now);
        await SupersedeActiveGrantsAsync(grants, tenantId, message, cancellationToken).ConfigureAwait(false);
        await grants.AddAsync(grant, cancellationToken).ConfigureAwait(false);
        generation.Accept(message.Revision, grant.LicenseRevision, now);
        await messaging.PublishAsync(state, new DeliveryOptions { TenantId = tenantId }).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateSnapshot(LicenseDesiredEvent message, OperationContext operation, string tenantId)
    {
        if (!string.Equals(operation.Scope.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The desired snapshot tenant does not match the validated envelope tenant.");
        }

        if (message.MessageId == Guid.Empty || message.Revision == 0 || message.OrderId == Guid.Empty || message.SubscriptionId == Guid.Empty)
        {
            throw new InvalidOperationException("The desired snapshot is missing its stable identity or binding.");
        }

        if (message.Enabled != (message.NotAfter is not null))
        {
            throw new InvalidOperationException("The desired snapshot must carry an expiry only when enabled.");
        }

        if (message.AddOnCodes is null || message.AddOnCodes.Length > 20)
        {
            throw new InvalidOperationException("The desired snapshot add-on list is missing or too large.");
        }
    }

    private static async Task<LicenseGeneration> LoadOrStartGenerationAsync(
        string tenantId,
        LicenseDesiredEvent message,
        IGenericWriteRepository<LicenseGeneration, Guid> generations,
        CancellationToken cancellationToken)
    {
        var existing = await generations.LoadForUpdateAsync(
            new LicenseGenerationByBindingSpecification(tenantId, message.OrderId, message.SubscriptionId),
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var started = LicenseGeneration.Start(tenantId, message.OrderId, message.SubscriptionId);
        if (started.IsError)
        {
            throw CreateFailure(started.Errors);
        }

        await generations.AddAsync(started.Value, cancellationToken).ConfigureAwait(false);
        return started.Value;
    }

    private static (LicenseGrant Grant, LicenseStateEvent State) IssueActive(
        LicenseDesiredEvent message,
        string tenantId,
        LicenseTier tier,
        string[] addOnCodes,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        ILicenseDocumentService documents,
        LicenseGeneration generation)
    {
        var licenseRevision = generation.NextLicenseRevision()
            ?? throw CreateFailure([LicenseGenerationErrors.RevisionExhausted]);
        var signed = documents.Sign(new LicenseSigningRequest(
            tenantId,
            message.OrderId,
            message.SubscriptionId,
            tier,
            addOnCodes,
            licenseRevision,
            documents.ActiveKeyId,
            expiresAt));
        if (signed.IsError)
        {
            throw CreateFailure(signed.Errors);
        }

        var issued = LicenseGrant.IssueActive(
            tenantId,
            message.OrderId,
            message.SubscriptionId,
            message.Revision,
            tier,
            addOnCodes,
            signed.Value.LicenseRevision,
            signed.Value.KeyId,
            signed.Value.ExpiresAt,
            signed.Value.Document,
            now,
            message.MessageId);
        if (issued.IsError)
        {
            throw CreateFailure(issued.Errors);
        }

        var state = new LicenseStateEvent(
            Guid.NewGuid(),
            now,
            message.TenantId,
            message.OrderId,
            message.SubscriptionId,
            message.Revision,
            message.Revision,
            CommerceMessaging.ActiveState,
            signed.Value.LicenseRevision,
            signed.Value.KeyId,
            signed.Value.ExpiresAt);
        return (issued.Value, state);
    }

    private static (LicenseGrant Grant, LicenseStateEvent State) IssueInactive(
        LicenseDesiredEvent message,
        string tenantId,
        LicenseTier tier,
        string[] addOnCodes,
        DateTimeOffset now)
    {
        var issued = LicenseGrant.IssueInactive(
            tenantId,
            message.OrderId,
            message.SubscriptionId,
            message.Revision,
            tier,
            addOnCodes,
            now,
            message.MessageId);
        if (issued.IsError)
        {
            throw CreateFailure(issued.Errors);
        }

        var state = new LicenseStateEvent(
            Guid.NewGuid(),
            now,
            message.TenantId,
            message.OrderId,
            message.SubscriptionId,
            message.Revision,
            message.Revision,
            CommerceMessaging.InactiveState,
            LicenseRevision: null,
            KeyId: null,
            ExpiresAt: null);
        return (issued.Value, state);
    }

    private static async Task SupersedeActiveGrantsAsync(
        IGenericWriteRepository<LicenseGrant, Guid> grants,
        string tenantId,
        LicenseDesiredEvent message,
        CancellationToken cancellationToken)
    {
        var previous = await grants.FindAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.OrderId == message.OrderId
                && candidate.SubscriptionId == message.SubscriptionId
                && candidate.State == LicenseGrantState.Active,
            cancellationToken).ConfigureAwait(false);
        foreach (var active in previous)
        {
            SupersedeGrant(grants, active);
        }
    }

    private static void SupersedeGrant(IGenericWriteRepository<LicenseGrant, Guid> grants, LicenseGrant grant)
    {
        if (grant.Supersede())
        {
            grants.Update(grant);
        }
    }

    private static LicenseTier ParseTier(string tier) => tier switch
    {
        CommerceMessaging.SharedTier => LicenseTier.Shared,
        CommerceMessaging.DedicatedTier => LicenseTier.Dedicated,
        _ => throw new InvalidOperationException("The desired snapshot has an unsupported tier."),
    };

    private static InvalidOperationException CreateFailure(IReadOnlyList<ErrorOr.Error> errors)
    {
        var descriptions = string.Join(", ", errors.Select(error => error.Description));
        return new InvalidOperationException("The desired snapshot could not be applied: " + descriptions);
    }
}
