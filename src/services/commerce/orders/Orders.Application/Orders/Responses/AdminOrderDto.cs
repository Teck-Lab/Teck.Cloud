using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Admin order projection.</summary>
/// <param name="OrderId">The order id.</param>
/// <param name="SubscriptionId">The subscription id.</param>
/// <param name="Tier">The tier.</param>
/// <param name="AddOnCodes">The add-on codes.</param>
/// <param name="Total">The immutable snapshot total.</param>
/// <param name="Status">The effective status.</param>
/// <param name="Revision">The public order revision.</param>
/// <param name="CreatedAt">The creation instant.</param>
/// <param name="UpdatedAt">The last update instant.</param>
/// <param name="Payment">The payment projection.</param>
/// <param name="Approval">The approval projection.</param>
/// <param name="Fulfillment">The fulfillment projection.</param>
/// <param name="Audit">The audit entries.</param>
public sealed record AdminOrderDto(
    Guid OrderId,
    Guid SubscriptionId,
    Tier Tier,
    IReadOnlyList<string> AddOnCodes,
    MoneyDto Total,
    OrderStatus Status,
    uint Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PaymentDto Payment,
    ApprovalDto Approval,
    FulfillmentDto Fulfillment,
    IReadOnlyList<AuditEntryDto> Audit);
