namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The authoritative provider state observed by a server-side re-fetch.</summary>
public enum BillingEvidenceStatus
{
    /// <summary>No provider binding exists yet, so there is nothing to reconcile.</summary>
    None = 0,

    /// <summary>The provider shows no current paid period; never advance paid proof.</summary>
    Pending = 1,

    /// <summary>The current invoice is paid, current, unrefunded and undisputed.</summary>
    Paid = 2,

    /// <summary>The provider confirmed a refund; entitlement is disabled but durable data is kept.</summary>
    Refunded = 3,

    /// <summary>The provider confirmed an active dispute; entitlement is disabled but durable data is kept.</summary>
    Disputed = 4,

    /// <summary>The provider confirmed cancellation or an expired subscription; entitlement is disabled but durable data is kept.</summary>
    Canceled = 5,

    /// <summary>The provider state does not match the stored order binding; never advance paid proof.</summary>
    Mismatch = 6,

    /// <summary>The provider could not be consulted; never advance or extend paid proof.</summary>
    Unavailable = 7,
}
