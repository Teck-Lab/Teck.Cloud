namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Recorded staff audit action.</summary>
public enum AuditAction
{
    /// <summary>The order was approved.</summary>
    Approve = 1,

    /// <summary>The order was rejected.</summary>
    Reject = 2,

    /// <summary>Placement evidence was recorded or replaced.</summary>
    RecordPlacementEvidence = 3,
}
