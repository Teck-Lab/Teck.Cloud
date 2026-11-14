using HealthChecks.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Persistence.Caching;
using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Health;

/// <summary>Checks the usable primary without requiring every former Sentinel primary to remain online.</summary>
/// <param name="connection">The host-owned managed Redis connection.</param>
internal sealed class RedisReadinessCheck(RedisConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = await connection.GetAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!connection.UsesSentinel)
        {
            return await new RedisHealthCheck(client).CheckHealthAsync(context, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = await client.GetDatabase().PingAsync(CommandFlags.DemandMaster).WaitAsync(cancellationToken).ConfigureAwait(false);
        return HealthCheckResult.Healthy();
    }
}
