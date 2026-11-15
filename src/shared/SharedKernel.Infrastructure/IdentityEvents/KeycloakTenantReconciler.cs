using System.Diagnostics;
using SharedKernel.Infrastructure.MultiTenancy;

namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>Broker-independent reconciliation of a bounded page from the owning host's tenant inventory.</summary>
/// <param name="store">The canonical directory.</param>
public sealed class KeycloakTenantReconciler(KeycloakTenantStore store)
{
    /// <summary>Refreshes known UUIDs, including deleted organizations; the caller persists progress only on success.</summary>
    /// <param name="organizationIds">At most 256 distinct organization IDs from trusted, service-owned inventory.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The number of refreshed IDs.</returns>
    /// <exception cref="ArgumentException">The page is too large or contains an empty UUID.</exception>
    public Task<int> ReconcileAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);
        return organizationIds.Count > 256 || organizationIds.Contains(Guid.Empty)
            ? throw new ArgumentException("Reconciliation requires a bounded page of valid organization IDs.", nameof(organizationIds))
            : RefreshAsync([.. organizationIds.Distinct()], cancellationToken);
    }

    private async Task<int> RefreshAsync(Guid[] organizationIds, CancellationToken cancellationToken)
    {
        var count = 0;
        var outcome = "success";
        var started = Stopwatch.GetTimestamp();
        try
        {
            foreach (var organization in organizationIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = organization.ToString("D");
                await store.InvalidateAsync(id, cancellationToken).ConfigureAwait(false);
                _ = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
                count++;
            }

            return count;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        catch
        {
            outcome = "failure";
            throw;
        }
        finally
        {
            IdentityEventTelemetry.Record(count, outcome, Stopwatch.GetElapsedTime(started).TotalSeconds);
        }
    }
}
