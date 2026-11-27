namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Separate and final staff approval decision.</summary>
public enum ApprovalStatus
{
    /// <summary>No decision has been recorded.</summary>
    Pending = 1,

    /// <summary>The order was approved.</summary>
    Approved = 2,

    /// <summary>The order was rejected.</summary>
    Rejected = 3,
}
