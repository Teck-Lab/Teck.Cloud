using SharedKernel.Infrastructure.MultiTenancy;

namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>Replays safely by invalidating then reading current provider state, never event representations.</summary>
public static class KeycloakOrganizationNoticeHandler
{
    /// <summary>Refreshes the canonical directory; failures propagate to Wolverine's retry/error handling.</summary>
    /// <param name="notice">Validated metadata from the dedicated listener.</param>
    /// <param name="store">The host's canonical directory.</param>
    /// <param name="cancellationToken">Delivery cancellation.</param>
    /// <returns>The refresh task.</returns>
    public static async Task HandleAsync(KeycloakOrganizationNotice notice, KeycloakTenantStore store, CancellationToken cancellationToken)
    {
        var id = notice.ResourcePath[14..];
        await store.InvalidateAsync(id, cancellationToken).ConfigureAwait(false);
        _ = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
