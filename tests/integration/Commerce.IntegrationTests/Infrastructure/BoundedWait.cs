namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Bounded polling for durable side effects instead of fixed sleeps.</summary>
internal static class BoundedWait
{
    internal static async Task UntilAsync(
        Func<CancellationToken, Task<bool>> condition,
        TimeSpan timeout,
        string because,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            if (await condition(deadline.Token).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(because, exception);
            }
        }
    }
}
