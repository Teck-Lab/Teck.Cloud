namespace Teck.Cloud.Commerce.Orders.Application.Orders.Checkout;

/// <summary>Serializes checkout intent creation per tenant and subject across processes.</summary>
public interface ICheckoutIntentLock
{
    /// <summary>Acquires the process-independent basket intent lock.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The held lock; dispose to release it.</returns>
    Task<IAsyncDisposable> AcquireAsync(string tenantId, string subjectId, CancellationToken cancellationToken);
}
