using System.Text.Json;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Billing;

/// <summary>Canonical encoding of verified provider price lines and billing requests.</summary>
public static class PriceSnapshot
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Encodes the verified price lines immutably.</summary>
    /// <param name="lines">The verified line set.</param>
    /// <returns>The canonical snapshot string.</returns>
    public static string Encode(IReadOnlyList<BillingVerifiedPriceLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var ordered = lines
            .OrderBy(static line => line.Code, StringComparer.Ordinal)
            .Select(static line => new BillingVerifiedPriceLine(
                line.Code,
                line.ProviderPriceId,
                line.UnitAmountMinor,
                line.Currency,
                line.Interval,
                line.IntervalCount))
            .ToArray();
        return JsonSerializer.Serialize(ordered, SerializerOptions);
    }

    /// <summary>Decodes a stored snapshot; an absent snapshot yields no lines.</summary>
    /// <param name="snapshot">The stored snapshot.</param>
    /// <returns>The verified lines.</returns>
    public static IReadOnlyList<BillingVerifiedPriceLine> Decode(string? snapshot) =>
        string.IsNullOrWhiteSpace(snapshot)
            ? []
            : JsonSerializer.Deserialize<BillingVerifiedPriceLine[]>(snapshot, SerializerOptions) ?? [];

    /// <summary>Builds the provider request for an order with its immutable intent facts.</summary>
    /// <param name="order">The loaded order.</param>
    /// <returns>The provider billing request.</returns>
    public static BillingOrder ToBillingOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new BillingOrder(
            Guid.Parse(order.TenantId),
            order.Id,
            order.SubscriptionId,
            order.OfferId,
            order.Tier == Domain.Common.Tier.Dedicated ? "dedicated" : "shared",
            Decode(order.VerifiedPriceSnapshot),
            order.CheckoutProviderSessionId,
            order.ProviderSubscriptionId,
            order.CheckoutRequestedAt);
    }

    /// <summary>Encodes the server catalog price request immutably.</summary>
    /// <param name="price">The catalog price request.</param>
    /// <returns>The canonical snapshot.</returns>
    public static string EncodeCatalog(BillingPrice price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return JsonSerializer.Serialize(price, SerializerOptions);
    }

    /// <summary>Decodes a stored catalog price request.</summary>
    /// <param name="snapshot">The stored snapshot.</param>
    /// <returns>The price request, or null when absent or malformed.</returns>
    public static BillingPrice? DecodeCatalog(string? snapshot) =>
        string.IsNullOrWhiteSpace(snapshot)
            ? null
            : JsonSerializer.Deserialize<BillingPrice>(snapshot, SerializerOptions);
}
