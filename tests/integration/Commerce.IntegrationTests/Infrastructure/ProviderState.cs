namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Provider state for one committed checkout binding.</summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="PriceId">The first requested recurring price.</param>
/// <param name="AmountMinor">The recurring amount in minor units.</param>
/// <param name="CustomerId">The provider customer.</param>
/// <param name="SessionId">The checkout session.</param>
/// <param name="SubscriptionId">The subscription.</param>
/// <param name="InvoiceId">The current invoice.</param>
/// <param name="Paid">Whether the current invoice is paid.</param>
/// <param name="PeriodEnd">The authoritative period end.</param>
/// <param name="Refunded">Whether the invoice was refunded.</param>
/// <param name="CreateCount">The provider create attempt count.</param>
public sealed record ProviderState(
    Guid OrderId,
    string PriceId,
    long AmountMinor,
    string CustomerId,
    string SessionId,
    string SubscriptionId,
    string InvoiceId,
    bool Paid,
    DateTimeOffset? PeriodEnd,
    bool Refunded,
    int CreateCount)
{
    /// <summary>Gets the ordered requested recurring price identifiers, one per line item.</summary>
    public IReadOnlyList<string> PriceIds { get; init; } = [];

    /// <summary>Gets the bound tenant.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Gets a value indicating whether the current charge is disputed.</summary>
    public bool Disputed { get; init; }

    internal Guid CommerceSubscriptionId { get; init; }
}
