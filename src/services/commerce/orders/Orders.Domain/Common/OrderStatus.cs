namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Effective customer-visible order status.</summary>
public enum OrderStatus
{
    /// <summary>No current verified paid period exists.</summary>
    PendingPayment = 1,

    /// <summary>Payment is verified but staff have not decided.</summary>
    PendingApproval = 2,

    /// <summary>Paid and approved, but readiness or the current grant is not established.</summary>
    PendingReadiness = 3,

    /// <summary>Paid, approved, ready and bound to the current active grant generation.</summary>
    Active = 4,

    /// <summary>The final staff decision rejected the order.</summary>
    Rejected = 5,

    /// <summary>Paid and approved with a current terminal placement failure.</summary>
    Failed = 6,
}
