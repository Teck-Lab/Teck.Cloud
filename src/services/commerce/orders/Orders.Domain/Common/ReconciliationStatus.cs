namespace Teck.Cloud.Commerce.Orders.Domain.Common;

/// <summary>Durable provider work state.</summary>
public enum ReconciliationStatus
{
    /// <summary>Due or scheduled work that has not completed.</summary>
    Pending = 1,

    /// <summary>The work completed and applied its authoritative result.</summary>
    Completed = 2,

    /// <summary>The work is abandoned because the order or provider binding cannot be resolved.</summary>
    Abandoned = 3,
}
