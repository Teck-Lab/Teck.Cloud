using System.Text.Json.Nodes;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>One committed checkout created through the real customer gateway.</summary>
/// <param name="OrderId">The order.</param>
/// <param name="SubscriptionId">The subscription.</param>
/// <param name="Order">The initial customer projection.</param>
/// <param name="CheckoutUrl">The provider checkout URL.</param>
/// <param name="TotalMinor">The immutable total in minor units.</param>
/// <param name="IdempotencyKey">The original intent key.</param>
/// <param name="BasketRevision">The original basket revision.</param>
internal sealed record Checkout(
    Guid OrderId,
    Guid SubscriptionId,
    JsonObject Order,
    string? CheckoutUrl,
    long TotalMinor,
    string IdempotencyKey,
    uint BasketRevision);
