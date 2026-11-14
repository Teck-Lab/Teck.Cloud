using OpenTelemetry.Instrumentation.StackExchangeRedis;
using SharedKernel.Persistence.Caching;
using StackExchange.Redis;

namespace SharedKernel.Infrastructure.Hosting;

internal sealed class RedisTelemetryObserver(StackExchangeRedisInstrumentation instrumentation) : IRedisConnectionObserver
{
    public void Connected(IConnectionMultiplexer connection) => instrumentation.AddConnection(connection);
}
