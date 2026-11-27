using ErrorOr;
using Microsoft.Extensions.Options;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Application.Orders.Settings;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;

/// <summary>Server-owned catalog advertisement and pricing.</summary>
/// <param name="options">The bound catalog configuration.</param>
/// <param name="placement">The configured allowlisted placements.</param>
public sealed class CatalogService(IOptions<CatalogOptions> options, IPlacementProbe placement)
{
    private const string ReplicaAddOn = "replicaReadAccess";

    /// <summary>Lists only currently configured and backed offers.</summary>
    /// <returns>The advertised offers.</returns>
    public IReadOnlyList<CatalogOffer> GetOffers()
    {
        var advertised = new List<CatalogOffer>();
        foreach (var offer in options.Value.Offers)
        {
            if (!TryParseTier(offer.Tier, out var tier)
                || string.IsNullOrWhiteSpace(offer.OfferId)
                || string.IsNullOrWhiteSpace(offer.DisplayName)
                || offer.AmountMinor <= 0)
            {
                continue;
            }

            if (tier == Tier.Dedicated && !HasDedicatedPlacement())
            {
                continue;
            }

            var addOns = new List<CatalogAddOn>();
            foreach (var addOn in offer.AddOns)
            {
                if (string.IsNullOrWhiteSpace(addOn.Code)
                    || string.IsNullOrWhiteSpace(addOn.DisplayName)
                    || addOn.AmountMinor <= 0
                    || (string.Equals(addOn.Code, ReplicaAddOn, StringComparison.Ordinal) && !HasReplicaPlacement()))
                {
                    continue;
                }

                addOns.Add(new CatalogAddOn(addOn.Code, addOn.DisplayName, addOn.AmountMinor));
            }

            advertised.Add(new CatalogOffer(offer.OfferId, tier, offer.DisplayName, offer.AmountMinor, options.Value.Currency, addOns));
        }

        return advertised;
    }

    /// <summary>Resolves and prices a canonical selection against the advertised catalog.</summary>
    /// <param name="selection">The canonical selection.</param>
    /// <returns>The priced selection, or a stable error.</returns>
    public ErrorOr<ResolvedSelection> Resolve(BasketSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var offer = GetOffers().FirstOrDefault(candidate => string.Equals(candidate.OfferId, selection.OfferId, StringComparison.Ordinal));
        if (offer is null)
        {
            return BasketErrors.UnsupportedOption;
        }

        var lines = new List<BillingPriceLine>
        {
            new(offer.OfferId, offer.AmountMinor, options.Value.Currency, "month", 1),
        };
        var total = offer.AmountMinor;
        foreach (var code in selection.AddOnCodes)
        {
            var addOn = offer.AddOns.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.Ordinal));
            if (addOn is null)
            {
                return BasketErrors.UnsupportedOption;
            }

            lines.Add(new BillingPriceLine(addOn.Code, addOn.AmountMinor, options.Value.Currency, "month", 1));
            total = checked(total + addOn.AmountMinor);
        }

        var money = Money.Create(total, options.Value.Currency);
        return money.IsError
            ? money.FirstError
            : new ResolvedSelection(offer.Tier, money.Value, new BillingPrice(offer.OfferId, TierName(offer.Tier), lines));
    }

    /// <summary>Gets whether a dedicated placement is configured.</summary>
    /// <returns>Whether a dedicated offering is backed.</returns>
    public bool HasDedicatedPlacement() => placement.Registrations.Any(static registration =>
        string.Equals(registration.Tier, "dedicated", StringComparison.OrdinalIgnoreCase));

    /// <summary>Gets whether a dedicated placement with a read-only replica is configured.</summary>
    /// <returns>Whether the replica add-on is backed.</returns>
    public bool HasReplicaPlacement() => placement.Registrations.Any(static registration =>
        string.Equals(registration.Tier, "dedicated", StringComparison.OrdinalIgnoreCase) && registration.ReplicaConfigured);

    private static bool TryParseTier(string? value, out Tier tier)
    {
        if (string.Equals(value, "shared", StringComparison.OrdinalIgnoreCase))
        {
            tier = Tier.Shared;
            return true;
        }

        if (string.Equals(value, "dedicated", StringComparison.OrdinalIgnoreCase))
        {
            tier = Tier.Dedicated;
            return true;
        }

        tier = default;
        return false;
    }

    private static string TierName(Tier tier) => tier == Tier.Dedicated ? "dedicated" : "shared";
}
