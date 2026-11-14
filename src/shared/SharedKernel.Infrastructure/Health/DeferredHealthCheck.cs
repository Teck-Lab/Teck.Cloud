using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedKernel.Infrastructure.Health;

/// <summary>Adapts asynchronous shared clients to published checks without blocking DI factories.</summary>
/// <param name="create">The host-owned dependency.</param>
internal sealed class DeferredHealthCheck(Func<CancellationToken, Task<IHealthCheck>> create) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var check = await create(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        return await check.CheckHealthAsync(context, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
