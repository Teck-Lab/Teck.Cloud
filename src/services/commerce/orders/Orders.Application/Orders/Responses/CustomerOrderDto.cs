using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Customer order projection.</summary>
/// <param name="OrderId">The order id.</param>
/// <param name="SubscriptionId">The subscription id.</param>
/// <param name="Tier">The tier.</param>
/// <param name="AddOnCodes">The add-on codes.</param>
/// <param name="Total">The immutable snapshot total.</param>
/// <param name="Status">The effective status.</param>
/// <param name="Revision">The public order revision.</param>
/// <param name="CreatedAt">The creation instant.</param>
/// <param name="UpdatedAt">The last update instant.</param>
/// <param name="DesiredRevision">The internal desired generation, present only when active.</param>
public sealed record CustomerOrderDto(
    Guid OrderId,
    Guid SubscriptionId,
    Tier Tier,
    IReadOnlyList<string> AddOnCodes,
    MoneyDto Total,
    OrderStatus Status,
    uint Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint? DesiredRevision);
