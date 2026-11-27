using Google.Protobuf.WellKnownTypes;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Host.Grpc;

/// <summary>Maps Application contracts to the frozen Orders protobuf messages.</summary>
internal static class ProtoMapping
{
    internal static V1.Money Money(MoneyDto value) => new()
    {
        AmountMinor = value.AmountMinor,
        Currency = value.Currency,
    };

    internal static V1.Offer Offer(OfferDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var offer = new V1.Offer
        {
            OfferId = value.OfferId,
            Tier = Tier(value.Tier),
            DisplayName = value.DisplayName,
            BasePrice = Money(value.BasePrice),
        };
        offer.AddOns.AddRange(value.AddOns.Select(addOn => new V1.AddOn
        {
            Code = addOn.Code,
            DisplayName = addOn.DisplayName,
            Price = Money(addOn.Price),
        }));
        return offer;
    }

    internal static V1.Basket Basket(BasketDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var basket = new V1.Basket { Revision = value.Revision };
        if (value.Selection is { } selection)
        {
            var wire = new V1.BasketSelection { OfferId = selection.OfferId };
            wire.AddOnCodes.AddRange(selection.AddOnCodes);
            basket.Selection = wire;
        }

        if (value.Total is { } total)
        {
            basket.Total = Money(total);
        }

        return basket;
    }

    internal static V1.CustomerOrder Customer(CustomerOrderDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var order = new V1.CustomerOrder
        {
            OrderId = value.OrderId.ToString("D"),
            SubscriptionId = value.SubscriptionId.ToString("D"),
            Tier = Tier(value.Tier),
            Total = Money(value.Total),
            Status = Status(value.Status),
            Revision = value.Revision,
            CreatedAt = Timestamp.FromDateTimeOffset(value.CreatedAt),
            UpdatedAt = Timestamp.FromDateTimeOffset(value.UpdatedAt),
            License = null,
        };
        order.AddOnCodes.AddRange(value.AddOnCodes);
        if (value.DesiredRevision is { } desired)
        {
            order.DesiredRevision = desired;
        }

        return order;
    }

    internal static V1.AdminOrder Admin(AdminOrderDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var order = new V1.AdminOrder
        {
            OrderId = value.OrderId.ToString("D"),
            SubscriptionId = value.SubscriptionId.ToString("D"),
            Tier = Tier(value.Tier),
            Total = Money(value.Total),
            Status = Status(value.Status),
            Revision = value.Revision,
            CreatedAt = Timestamp.FromDateTimeOffset(value.CreatedAt),
            UpdatedAt = Timestamp.FromDateTimeOffset(value.UpdatedAt),
            Payment = Payment(value.Payment),
            Approval = Approval(value.Approval),
            Fulfillment = Fulfillment(value.Fulfillment),
        };
        order.AddOnCodes.AddRange(value.AddOnCodes);
        order.Audit.AddRange(value.Audit.Select(AuditEntry));
        return order;
    }

    internal static V1.Tier Tier(Domain.Common.Tier tier) =>
        tier == Domain.Common.Tier.Dedicated ? V1.Tier.Dedicated : V1.Tier.Shared;

    internal static V1.OrderStatus Status(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => V1.OrderStatus.PendingPayment,
        OrderStatus.PendingApproval => V1.OrderStatus.PendingApproval,
        OrderStatus.PendingReadiness => V1.OrderStatus.PendingReadiness,
        OrderStatus.Active => V1.OrderStatus.Active,
        OrderStatus.Rejected => V1.OrderStatus.Rejected,
        OrderStatus.Failed => V1.OrderStatus.Failed,
        _ => V1.OrderStatus.Unspecified,
    };

    internal static V1.PaymentStatus PaymentStatus(Domain.Common.PaymentStatus status) =>
        status == Domain.Common.PaymentStatus.Paid ? V1.PaymentStatus.Paid : V1.PaymentStatus.Pending;

    internal static V1.ApprovalStatus ApprovalStatus(Domain.Common.ApprovalStatus status) => status switch
    {
        Domain.Common.ApprovalStatus.Pending => V1.ApprovalStatus.Pending,
        Domain.Common.ApprovalStatus.Approved => V1.ApprovalStatus.Approved,
        Domain.Common.ApprovalStatus.Rejected => V1.ApprovalStatus.Rejected,
        _ => V1.ApprovalStatus.Pending,
    };

    internal static V1.FulfillmentStatus FulfillmentStatus(Domain.Common.FulfillmentStatus status) => status switch
    {
        Domain.Common.FulfillmentStatus.AwaitingEvidence => V1.FulfillmentStatus.AwaitingEvidence,
        Domain.Common.FulfillmentStatus.Verifying => V1.FulfillmentStatus.Verifying,
        Domain.Common.FulfillmentStatus.Ready => V1.FulfillmentStatus.Ready,
        Domain.Common.FulfillmentStatus.Failed => V1.FulfillmentStatus.Failed,
        _ => V1.FulfillmentStatus.Unspecified,
    };

    internal static V1.FulfillmentFailureCode FailureCode(FulfillmentFailureCode? code) => code switch
    {
        FulfillmentFailureCode.BindingMismatch => V1.FulfillmentFailureCode.BindingMismatch,
        FulfillmentFailureCode.CapabilityUnavailable => V1.FulfillmentFailureCode.CapabilityUnavailable,
        FulfillmentFailureCode.ProbeUnavailable => V1.FulfillmentFailureCode.ProbeUnavailable,
        _ => V1.FulfillmentFailureCode.Unspecified,
    };

    internal static V1.AuditAction AuditAction(Domain.Common.AuditAction action) => action switch
    {
        Domain.Common.AuditAction.Approve => V1.AuditAction.Approve,
        Domain.Common.AuditAction.Reject => V1.AuditAction.Reject,
        Domain.Common.AuditAction.RecordPlacementEvidence => V1.AuditAction.RecordPlacementEvidence,
        _ => V1.AuditAction.Unspecified,
    };

    private static V1.Payment Payment(PaymentDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var payment = new V1.Payment { Status = PaymentStatus(value.Status) };
        if (value.PaidThrough is { } paidThrough)
        {
            payment.PaidThrough = Timestamp.FromDateTimeOffset(paidThrough);
        }

        return payment;
    }

    private static V1.Approval Approval(ApprovalDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var approval = new V1.Approval { Status = ApprovalStatus(value.Status) };
        if (value.ActorId is { } actor)
        {
            approval.ActorId = actor;
        }

        if (value.Reason is { } reason)
        {
            approval.Reason = reason;
        }

        if (value.OccurredAt is { } decidedAt)
        {
            approval.OccurredAt = Timestamp.FromDateTimeOffset(decidedAt);
        }

        return approval;
    }

    private static V1.Fulfillment Fulfillment(FulfillmentDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var fulfillment = new V1.Fulfillment
        {
            Status = FulfillmentStatus(value.Status),
            FailureCode = FailureCode(value.FailureCode),
        };
        if (value.PlacementId is { } placement)
        {
            fulfillment.PlacementId = placement;
        }

        if (value.EvidenceReference is { } evidence)
        {
            fulfillment.EvidenceReference = evidence;
        }

        if (value.ObservedAt is { } observedAt)
        {
            fulfillment.ObservedAt = Timestamp.FromDateTimeOffset(observedAt);
        }

        return fulfillment;
    }

    private static V1.AuditEntry AuditEntry(AuditEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var wire = new V1.AuditEntry
        {
            OrderId = entry.OrderId.ToString("D"),
            ActorId = entry.ActorId,
            Action = AuditAction(entry.Action),
            OccurredAt = Timestamp.FromDateTimeOffset(entry.OccurredAt),
        };
        if (entry.Reason is { } reason)
        {
            wire.Reason = reason;
        }

        return wire;
    }
}
