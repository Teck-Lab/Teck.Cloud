namespace Teck.Cloud.AppHost;

/// <summary>Local Sentinel topology whose announced addresses work for host processes and containers.</summary>
public static class RedisSentinelHostingExtensions
{
    /// <summary>Adds a primary, replica and three authenticated Sentinels, plus the redis connection resource.</summary>
    /// <param name="builder">The AppHost builder.</param>
    /// <returns>The Sentinel connection resource for WithReference on consumers.</returns>
    public static IResourceBuilder<IResourceWithConnectionString> AddFoundationRedisSentinel(this IDistributedApplicationBuilder builder)
    {
        var advertised = builder.AddParameter("redis-advertised-host");
        var password = builder.AddParameter("redis-password", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Redis", "start.sh"));
        var primary = AddNode(builder, "redis-primary", "primary", 6379, advertised, password, script);
        var primaryPort = primary.GetEndpoint("tcp", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Port);
        _ = AddNode(builder, "redis-replica", "replica", 6379, advertised, password, script)
            .WithEnvironment("REDIS_PRIMARY_PORT", primaryPort).WaitFor(primary);
        var sentinels = Enumerable.Range(1, 3).Select(index =>
            AddNode(builder, "redis-sentinel-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "sentinel", 26379, advertised, password, script)
                .WithEnvironment("REDIS_PRIMARY_PORT", primaryPort).WaitFor(primary)).ToArray();
        var first = sentinels[0].GetEndpoint("tcp", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Port);
        var second = sentinels[1].GetEndpoint("tcp", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Port);
        var third = sentinels[2].GetEndpoint("tcp", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Port);
        return builder.AddConnectionString("redis", ReferenceExpression.Create($"{advertised}:{first},{advertised}:{second},{advertised}:{third},serviceName=teck,password={password},sentinelPassword={password},abortConnect=false"));
    }

    private static IResourceBuilder<ContainerResource> AddNode(IDistributedApplicationBuilder builder, string name, string role, int targetPort, IResourceBuilder<ParameterResource> advertised, IResourceBuilder<ParameterResource> password, string script)
    {
        var node = builder.AddContainer(name, "redis", "8-alpine")
            .WithEndpoint(targetPort: targetPort, name: "tcp", isProxied: false)
            .WithEndpoint("tcp", endpoint => endpoint.TargetHost = "0.0.0.0")
            .WithCertificateTrustScope(CertificateTrustScope.None)
            .WithEnvironment("REDIS_ROLE", role)
            .WithEnvironment("REDIS_PASSWORD", password)
            .WithEnvironment("REDIS_ADVERTISED_HOST", advertised)
            .WithEntrypoint("sh").WithArgs("-c", script);
        return node.WithEnvironment("REDIS_ADVERTISED_PORT", node.GetEndpoint("tcp", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Port));
    }
}
