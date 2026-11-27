namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Read-only placement verification against the allowlisted registry; it never provisions or writes.</summary>
public interface IPlacementProbe
{
    /// <summary>Gets the allowlisted placement registry in deterministic placement-identifier order.</summary>
    IReadOnlyList<PlacementRegistration> Registrations { get; }

    /// <summary>Finds one allowlisted placement registration by identifier.</summary>
    /// <param name="placementId">The opaque placement identifier recorded as staff evidence.</param>
    /// <returns>The safe registration view, or <see langword="null"/> when the placement is not allowlisted.</returns>
    PlacementRegistration? FindRegistration(string placementId);

    /// <summary>Probes the configured primary target and, when required, its physical read-only standby.</summary>
    /// <param name="request">The durable order binding and placement facts to verify.</param>
    /// <param name="cancellationToken">The caller cancellation token; the probe also enforces its own bounded timeout.</param>
    /// <returns>The classified probe outcome; a nonterminal failure never activates entitlement.</returns>
    Task<PlacementProbeResult> VerifyAsync(PlacementProbeRequest request, CancellationToken cancellationToken);
}
