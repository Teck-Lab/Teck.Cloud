using Google.Protobuf.WellKnownTypes;
using Teck.Cloud.Commerce.Licensing.V1;
using Teck.Cloud.Commerce.Orders.V1;
using OrderTier = Teck.Cloud.Commerce.Orders.V1.Tier;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Deterministic order, subscription and license facts shared by the customer gateway matrix.</summary>
internal static class CommerceFixtures
{
    internal const uint PublicOrderRevision = 5;
    internal const uint ListOrderRevision = 4;
    internal const uint ReplayBasketRevision = 3;
    internal const uint InitialDesiredRevision = 7;
    internal const uint RereadDesiredRevision = 8;
    internal const uint LicenseRevision = 1;
    internal const string LicenseDocument = "fixture-license-document-v1";
    internal const string LicenseKeyId = "K1";
    internal const string Cursor = "cursor_orders_page_2";
    internal const string IdempotencyKey = "4c2d6e8f-0a1b-4c3d-8e5f-60718293a4b5";

    internal static readonly Guid OrderA = new(0x0f0e0d0c, 0x0b0a, 0x4999, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11);
    internal static readonly Guid SubscriptionA = new(0x1a1b1c1d, 0x2e2f, 0x4a3b, 0x8c, 0x4d, 0x5e, 0x6f, 0x70, 0x81, 0x92, 0xa3);
    internal static readonly Guid OrderB = new(0x7d8e9fa0, 0xb1c2, 0x4d3e, 0x8f, 0x40, 0x51, 0x62, 0x73, 0x84, 0x95, 0x06);
    internal static readonly Guid SubscriptionB = new(0x8e9fa0b1, 0xc2d3, 0x4e4f, 0x90, 0x51, 0x62, 0x73, 0x84, 0x95, 0x06, 0x17);

    internal static readonly DateTimeOffset CreatedAt = new(2026, 11, 23, 19, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset AUpdatedAt = new(2026, 11, 23, 20, 15, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset BUpdatedAt = new(2026, 11, 23, 20, 10, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset BRereadAt = new(2026, 11, 23, 20, 16, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset LicenseExpiresAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static CustomerOrder ActiveOrder(
        Guid orderId,
        Guid subscriptionId,
        DateTimeOffset updatedAt,
        uint revision = PublicOrderRevision,
        uint? desiredRevision = InitialDesiredRevision) =>
        Order(orderId, subscriptionId, OrderStatus.Active, revision, updatedAt, desiredRevision);

    internal static CustomerOrder PendingReadinessOrder(
        Guid orderId,
        Guid subscriptionId,
        DateTimeOffset updatedAt,
        uint revision = PublicOrderRevision,
        uint? desiredRevision = null) =>
        Order(orderId, subscriptionId, OrderStatus.PendingReadiness, revision, updatedAt, desiredRevision);

    internal static CustomerOrder Order(
        Guid orderId,
        Guid subscriptionId,
        OrderStatus status,
        uint revision,
        DateTimeOffset updatedAt,
        uint? desiredRevision)
    {
        var order = new CustomerOrder
        {
            OrderId = orderId.ToString("D"),
            SubscriptionId = subscriptionId.ToString("D"),
            Tier = OrderTier.Shared,
            Total = new Money { AmountMinor = 1000, Currency = "USD" },
            Status = status,
            Revision = revision,
            CreatedAt = Timestamp.FromDateTimeOffset(CreatedAt.ToUniversalTime()),
            UpdatedAt = Timestamp.FromDateTimeOffset(updatedAt.ToUniversalTime()),
        };
        if (desiredRevision is { } generation)
        {
            order.DesiredRevision = generation;
        }

        return order;
    }

    internal static LicenseEnvelope Envelope() =>
        new()
        {
            Document = LicenseDocument,
            KeyId = LicenseKeyId,
            Revision = LicenseRevision,
            ExpiresAt = Timestamp.FromDateTimeOffset(LicenseExpiresAt.ToUniversalTime()),
        };
}
