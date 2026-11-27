using Microsoft.Extensions.Configuration;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Placement;

/// <summary>The immutable allowlisted placement registry; secret references stay inside infrastructure.</summary>
internal sealed class PlacementOptions
{
    /// <summary>The section path that supplies the allowlisted placement registry.</summary>
    public const string SectionPath = "CommerceOrders:Placements";

    /// <summary>The only supported replica capability add-on code.</summary>
    public const string ReplicaReadAccess = "replicaReadAccess";

    /// <summary>The only dedicated placement tier.</summary>
    public const string DedicatedTier = "dedicated";

    /// <summary>The only shared placement tier.</summary>
    public const string SharedTier = "shared";

    /// <summary>Gets the allowlisted entries in deterministic placement-identifier order.</summary>
    public IReadOnlyList<PlacementEntry> Entries { get; init; } = [];

    /// <summary>Reads and validates the allowlisted placement registry.</summary>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>The validated registry entries.</returns>
    /// <exception cref="InvalidOperationException">A structurally invalid or duplicate registry entry was configured.</exception>
    public static PlacementOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var entries = new List<PlacementEntry>();
        var placementIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in configuration.GetSection(SectionPath).GetChildren())
        {
            var placementId = Required(child, "PlacementId", child.Key);
            if (!placementIds.Add(placementId))
            {
                throw new InvalidOperationException($"Duplicate placement registry identifier '{placementId}'.");
            }

            var tier = Required(child, "Tier", placementId).ToLowerInvariant();
            if (!string.Equals(tier, SharedTier, StringComparison.Ordinal) &&
                !string.Equals(tier, DedicatedTier, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Placement '{placementId}' has an unsupported tier.");
            }

            var primarySecretReference = Required(child, "PrimarySecretReference", placementId);
            var replicaSecretReference = Optional(child["ReplicaSecretReference"]);
            Guid? dedicatedTenantId = null;
            var dedicatedText = Optional(child["DedicatedTenantId"]);
            if (dedicatedText is not null)
            {
                if (!Guid.TryParse(dedicatedText, out var parsedTenantId))
                {
                    throw new InvalidOperationException($"Placement '{placementId}' has a malformed dedicated tenant identifier.");
                }

                dedicatedTenantId = parsedTenantId;
            }

            if (string.Equals(tier, DedicatedTier, StringComparison.Ordinal) && dedicatedTenantId is null)
            {
                throw new InvalidOperationException($"Dedicated placement '{placementId}' requires a dedicated tenant identifier.");
            }

            if (string.Equals(tier, SharedTier, StringComparison.Ordinal) && dedicatedTenantId is not null)
            {
                throw new InvalidOperationException($"Shared placement '{placementId}' cannot declare a dedicated tenant identifier.");
            }

            entries.Add(new PlacementEntry(
                placementId,
                tier,
                primarySecretReference,
                replicaSecretReference,
                dedicatedTenantId));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.PlacementId, right.PlacementId));
        return new PlacementOptions { Entries = entries };
    }

    private static string Required(IConfigurationSection section, string key, string placementId)
    {
        var value = Optional(section[key]);
        return value ?? throw new InvalidOperationException($"Placement '{placementId}' is missing required setting '{key}'.");
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
