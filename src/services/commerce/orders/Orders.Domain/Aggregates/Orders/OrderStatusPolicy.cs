using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

/// <summary>Pure lifecycle projection shared by command responses and query reads.</summary>
public static class OrderStatusPolicy
{
    /// <summary>Resolves the customer-visible status using the contract precedence.</summary>
    /// <param name="approval">The final staff decision.</param>
    /// <param name="paid">Whether a current verified paid period exists.</param>
    /// <param name="freshProof">Whether current evidence has a fresh successful probe.</param>
    /// <param name="currentGrant">Whether the current desired generation has a current active grant.</param>
    /// <param name="terminalFailure">Whether the current evidence has a terminal failure.</param>
    /// <returns>The effective order status.</returns>
    public static OrderStatus Resolve(
        ApprovalStatus approval,
        bool paid,
        bool freshProof,
        bool currentGrant,
        bool terminalFailure) => (approval, paid, terminalFailure, freshProof && currentGrant) switch
        {
            (ApprovalStatus.Rejected, _, _, _) => OrderStatus.Rejected,
            (_, false, _, _) => OrderStatus.PendingPayment,
            (ApprovalStatus.Pending, _, _, _) => OrderStatus.PendingApproval,
            (_, _, true, _) => OrderStatus.Failed,
            (_, _, _, true) => OrderStatus.Active,
            _ => OrderStatus.PendingReadiness,
        };
}
