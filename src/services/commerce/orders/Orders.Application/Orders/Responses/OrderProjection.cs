using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Maps loaded facts to the Application response contracts.</summary>
internal static class OrderProjection
{
    internal static OrderView From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderView(
            order.Id,
            order.SubscriptionId,
            order.Tier,
            order.AddOnCodes,
            order.TotalAmountMinor,
            order.Currency,
            order.Revision,
            order.CreatedAt,
            order.UpdatedOn ?? order.CreatedAt,
            order.PaymentStatus,
            order.PaidThrough,
            order.ApprovalStatus,
            order.ApprovalActorId,
            order.ApprovalReason,
            order.ApprovalOccurredAt,
            order.EvidenceRevision,
            order.EvidencePlacementId,
            order.EvidenceReference,
            order.FulfillmentStatus,
            order.ProofEvidenceRevision,
            order.ProofPlacementId,
            order.ProofObservedAt,
            order.ProofFailureCode,
            order.DesiredRevision,
            order.DesiredEnabled,
            order.DesiredNotAfter,
            order.LicenseState,
            order.LicenseRevision,
            order.LicenseKeyId,
            order.LicenseExpiresAt,
            order.AppliedDesiredRevision);
    }

    internal static OrderView From(OrderReadModel order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new OrderView(
            order.Id,
            order.SubscriptionId,
            order.Tier,
            BasketSelection.DecodeAddOnCodes(order.AddOnCodesCsv),
            order.TotalAmountMinor,
            order.Currency,
            order.Revision,
            order.CreatedAt,
            order.UpdatedOn ?? order.CreatedAt,
            order.PaymentStatus,
            order.PaidThrough,
            order.ApprovalStatus,
            order.ApprovalActorId,
            order.ApprovalReason,
            order.ApprovalOccurredAt,
            order.EvidenceRevision,
            order.EvidencePlacementId,
            order.EvidenceReference,
            order.FulfillmentStatus,
            order.ProofEvidenceRevision,
            order.ProofPlacementId,
            order.ProofObservedAt,
            order.ProofFailureCode,
            order.DesiredRevision,
            order.DesiredEnabled,
            order.DesiredNotAfter,
            order.LicenseState,
            order.LicenseRevision,
            order.LicenseKeyId,
            order.LicenseExpiresAt,
            order.AppliedDesiredRevision);
    }

    internal static CustomerOrderDto Customer(OrderView view, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        var status = view.Status(now);
        return new CustomerOrderDto(
            view.Id,
            view.SubscriptionId,
            view.Tier,
            view.AddOnCodes,
            new MoneyDto(view.TotalAmountMinor, view.Currency),
            status,
            view.Revision,
            view.CreatedAt,
            view.UpdatedAt,
            status == OrderStatus.Active && view.DesiredRevision > 0 ? view.DesiredRevision : null);
    }

    internal static AdminOrderDto Admin(OrderView view, IReadOnlyList<AuditEntryDto> audit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(audit);
        var paid = view.IsPaid(now);
        return new AdminOrderDto(
            view.Id,
            view.SubscriptionId,
            view.Tier,
            view.AddOnCodes,
            new MoneyDto(view.TotalAmountMinor, view.Currency),
            view.Status(now),
            view.Revision,
            view.CreatedAt,
            view.UpdatedAt,
            new PaymentDto(paid ? PaymentStatus.Paid : PaymentStatus.Pending, paid ? view.PaidThrough : null),
            new ApprovalDto(view.ApprovalStatus, view.ApprovalActorId, view.ApprovalReason, view.ApprovalOccurredAt),
            new FulfillmentDto(
                view.ProjectedFulfillment(now),
                view.EvidencePlacementId,
                view.EvidenceReference,
                view.ProjectedObservedAt(now),
                view.ProjectedFailureCode(now)),
            audit);
    }

    internal static AuditEntryDto Audit(OrderAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditEntryDto(entry.OrderId, entry.ActorId, entry.Action, entry.Reason, entry.OccurredAt);
    }

    internal static AuditEntryDto Audit(OrderAuditReadModel entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditEntryDto(entry.OrderId, entry.ActorId, entry.Action, entry.Reason, entry.OccurredAt);
    }

    internal static BasketDto Basket(Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        var selection = basket.Selection is { } current
            ? new BasketSelectionDto(current.OfferId, current.AddOnCodes)
            : null;
        var total = basket.Total is { } money ? new MoneyDto(money.AmountMinor, money.Currency) : null;
        return new BasketDto(selection, basket.Revision, total);
    }

    internal static BasketDto Basket(BasketReadModel basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        var selection = basket.OfferId is null
            ? null
            : new BasketSelectionDto(basket.OfferId, BasketSelection.DecodeAddOnCodes(basket.AddOnCodesCsv));
        var total = basket.TotalAmountMinor is { } amount && basket.TotalCurrency is { } currency
            ? new MoneyDto(amount, currency)
            : null;
        return new BasketDto(selection, basket.Revision, total);
    }
}
