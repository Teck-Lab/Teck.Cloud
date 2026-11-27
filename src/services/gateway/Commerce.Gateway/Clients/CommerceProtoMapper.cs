using Google.Protobuf.WellKnownTypes;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;
using LicensingV1 = Teck.Cloud.Commerce.Licensing.V1;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>Maps generated contract messages into edge projections without inventing state.</summary>
internal static class CommerceProtoMapper
{
    internal static CatalogView ToCatalog(OrdersV1.GetCatalogResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new CatalogView([.. response.Offers.Select(ToOffer)]);
    }

    internal static BasketView ToBasket(OrdersV1.Basket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        return new BasketView(
            basket.Selection is null ? null : new BasketSelectionView(basket.Selection.OfferId, [.. basket.Selection.AddOnCodes]),
            basket.Revision,
            basket.Total is null ? null : ToMoney(basket.Total));
    }

    internal static CustomerOrderSnapshot ToCustomerSnapshot(OrdersV1.CustomerOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new CustomerOrderSnapshot(
            ParseId(order.OrderId, "orders.customer_order.order_id"),
            ParseId(order.SubscriptionId, "orders.customer_order.subscription_id"),
            ToTier(order.Tier),
            [.. order.AddOnCodes],
            ToMoney(order.Total ?? throw Malformed("orders.customer_order.total")),
            ToStatus(order.Status),
            order.Revision,
            Instant(order.CreatedAt, "orders.customer_order.created_at"),
            Instant(order.UpdatedAt, "orders.customer_order.updated_at"),
            order.HasDesiredRevision && order.DesiredRevision is > 0 ? order.DesiredRevision : null,
            License: null);
    }

    internal static AdminOrderSnapshot ToAdminSnapshot(OrdersV1.AdminOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new AdminOrderSnapshot(
            ParseId(order.OrderId, "orders.admin_order.order_id"),
            ParseId(order.SubscriptionId, "orders.admin_order.subscription_id"),
            ToTier(order.Tier),
            [.. order.AddOnCodes],
            ToMoney(order.Total ?? throw Malformed("orders.admin_order.total")),
            ToStatus(order.Status),
            order.Revision,
            Instant(order.CreatedAt, "orders.admin_order.created_at"),
            Instant(order.UpdatedAt, "orders.admin_order.updated_at"),
            ToPayment(order.Payment ?? throw Malformed("orders.admin_order.payment")),
            ToApproval(order.Approval ?? throw Malformed("orders.admin_order.approval")),
            ToFulfillment(order.Fulfillment ?? throw Malformed("orders.admin_order.fulfillment")),
            [.. order.Audit.Select(ToAudit)]);
    }

    internal static LicenseView ToLicense(LicensingV1.GetCurrentLicenseResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var license = response.License ?? throw Malformed("licensing.get_current_license_response.license");
        return string.IsNullOrWhiteSpace(license.Document) || license.Document.Length > 16384
            || string.IsNullOrWhiteSpace(license.KeyId) || license.KeyId.Length > 64
            || license.Revision == 0
            ? throw Malformed("licensing.license_envelope")
            : new LicenseView(license.Document, license.KeyId, license.Revision, CommerceTimes.Format(Instant(license.ExpiresAt, "licensing.license_envelope.expires_at")));
    }

    internal static CustomerOrderView ToView(CustomerOrderSnapshot order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new CustomerOrderView(
            order.OrderId,
            order.SubscriptionId,
            order.Tier,
            order.AddOnCodes,
            order.Total,
            order.Status,
            order.Revision,
            CommerceTimes.Format(order.CreatedAt),
            CommerceTimes.Format(order.UpdatedAt),
            order.License);
    }

    internal static AdminOrderView ToView(AdminOrderSnapshot order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new AdminOrderView(
            order.OrderId,
            order.SubscriptionId,
            order.Tier,
            order.AddOnCodes,
            order.Total,
            order.Status,
            order.Revision,
            CommerceTimes.Format(order.CreatedAt),
            CommerceTimes.Format(order.UpdatedAt),
            order.Payment,
            order.Approval,
            order.Fulfillment,
            order.Audit);
    }

    private static OfferView ToOffer(OrdersV1.Offer offer) =>
        new(offer.OfferId, ToTier(offer.Tier), offer.DisplayName, ToMoney(offer.BasePrice ?? throw Malformed("orders.offer.base_price")), [.. offer.AddOns.Select(ToAddOn)]);

    private static AddOnView ToAddOn(OrdersV1.AddOn addOn) =>
        new(addOn.Code, addOn.DisplayName, ToMoney(addOn.Price ?? throw Malformed("orders.add_on.price")));

    private static MoneyView ToMoney(OrdersV1.Money money) =>
        money.AmountMinor is > 0 and <= 9007199254740991 && money.Currency.Length == 3 && money.Currency.All(char.IsAsciiLetterUpper)
            ? new MoneyView(money.AmountMinor, money.Currency)
            : throw Malformed("orders.money");

    private static string ToTier(OrdersV1.Tier tier) => tier switch
    {
        OrdersV1.Tier.Unspecified => throw Malformed("orders.tier"),
        OrdersV1.Tier.Shared => "shared",
        OrdersV1.Tier.Dedicated => "dedicated",
        _ => throw Malformed("orders.tier"),
    };

    private static string ToStatus(OrdersV1.OrderStatus status) => status switch
    {
        OrdersV1.OrderStatus.Unspecified => throw Malformed("orders.order_status"),
        OrdersV1.OrderStatus.PendingPayment => "pendingPayment",
        OrdersV1.OrderStatus.PendingApproval => "pendingApproval",
        OrdersV1.OrderStatus.PendingReadiness => "pendingReadiness",
        OrdersV1.OrderStatus.Active => "active",
        OrdersV1.OrderStatus.Rejected => "rejected",
        OrdersV1.OrderStatus.Failed => "failed",
        _ => throw Malformed("orders.order_status"),
    };

    private static PaymentView ToPayment(OrdersV1.Payment payment) =>
        new(
            payment.Status switch
            {
                OrdersV1.PaymentStatus.Unspecified => throw Malformed("orders.payment.status"),
                OrdersV1.PaymentStatus.Pending => "pending",
                OrdersV1.PaymentStatus.Paid => "paid",
                _ => throw Malformed("orders.payment.status"),
            },
            payment.PaidThrough is null ? null : CommerceTimes.Format(Instant(payment.PaidThrough, "orders.payment.paid_through")));

    private static ApprovalView ToApproval(OrdersV1.Approval approval) =>
        new(
            approval.Status switch
            {
                OrdersV1.ApprovalStatus.Unspecified => throw Malformed("orders.approval.status"),
                OrdersV1.ApprovalStatus.Pending => "pending",
                OrdersV1.ApprovalStatus.Approved => "approved",
                OrdersV1.ApprovalStatus.Rejected => "rejected",
                _ => throw Malformed("orders.approval.status"),
            },
            Optional(approval.ActorId, approval.HasActorId, "orders.approval.actor_id"),
            Optional(approval.Reason, approval.HasReason, "orders.approval.reason"),
            approval.OccurredAt is null ? null : CommerceTimes.Format(Instant(approval.OccurredAt, "orders.approval.occurred_at")));

    private static FulfillmentView ToFulfillment(OrdersV1.Fulfillment fulfillment)
    {
        var status = fulfillment.Status switch
        {
            OrdersV1.FulfillmentStatus.Unspecified => throw Malformed("orders.fulfillment.status"),
            OrdersV1.FulfillmentStatus.AwaitingEvidence => "awaitingEvidence",
            OrdersV1.FulfillmentStatus.Verifying => "verifying",
            OrdersV1.FulfillmentStatus.Ready => "ready",
            OrdersV1.FulfillmentStatus.Failed => "failed",
            _ => throw Malformed("orders.fulfillment.status"),
        };
        var failureCode = fulfillment.FailureCode switch
        {
            OrdersV1.FulfillmentFailureCode.Unspecified => null,
            OrdersV1.FulfillmentFailureCode.BindingMismatch => "binding_mismatch",
            OrdersV1.FulfillmentFailureCode.CapabilityUnavailable => "capability_unavailable",
            OrdersV1.FulfillmentFailureCode.ProbeUnavailable => "probe_unavailable",
            _ => throw Malformed("orders.fulfillment.failure_code"),
        };
        return new FulfillmentView(
            status,
            fulfillment.HasPlacementId ? ParseId(fulfillment.PlacementId, "orders.fulfillment.placement_id") : null,
            Optional(fulfillment.EvidenceReference, fulfillment.HasEvidenceReference, "orders.fulfillment.evidence_reference"),
            fulfillment.ObservedAt is null ? null : CommerceTimes.Format(Instant(fulfillment.ObservedAt, "orders.fulfillment.observed_at")),
            failureCode);
    }

    private static AuditEntryView ToAudit(OrdersV1.AuditEntry entry)
    {
        var action = entry.Action switch
        {
            OrdersV1.AuditAction.Unspecified => throw Malformed("orders.audit_entry.action"),
            OrdersV1.AuditAction.Approve => "approve",
            OrdersV1.AuditAction.Reject => "reject",
            OrdersV1.AuditAction.RecordPlacementEvidence => "recordPlacementEvidence",
            _ => throw Malformed("orders.audit_entry.action"),
        };
        return new AuditEntryView(
            ParseId(entry.OrderId, "orders.audit_entry.order_id"),
            entry.ActorId,
            action,
            Optional(entry.Reason, entry.HasReason, "orders.audit_entry.reason"),
            CommerceTimes.Format(Instant(entry.OccurredAt, "orders.audit_entry.occurred_at")));
    }

    private static Guid ParseId(string value, string field) =>
        Guid.TryParseExact(value, "D", out var id) ? id : throw Malformed(field);

    private static DateTimeOffset Instant(Timestamp? value, string field)
    {
        if (value is null)
        {
            throw Malformed(field);
        }

        try
        {
            return value.ToDateTimeOffset();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentOutOfRangeException)
        {
            throw Malformed(field, error);
        }
    }

    private static string? Optional(string value, bool present, string field) =>
        present ? RequiredValue(value, field) : null;

    private static string RequiredValue(string value, string field) =>
        string.IsNullOrEmpty(value) ? throw Malformed(field) : value;

    private static CommerceDependencyException Malformed(string field, Exception? innerException = null)
    {
        var message = "The dependency returned a malformed commerce response field: " + field + ".";
        return innerException is null
            ? new CommerceDependencyException(message)
            : new CommerceDependencyException(message, innerException);
    }
}
