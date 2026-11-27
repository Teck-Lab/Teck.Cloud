using Microsoft.Extensions.Configuration;

namespace Teck.Cloud.Commerce.Gateway.Composition;

/// <summary>Host-owned least-privileged directory lookup used before gRPC delegation.</summary>
public sealed class CommerceDirectoryOptions
{
    /// <summary>Gets the Redis Sentinel or isolated standalone connection string.</summary>
    public required string RedisConnectionString { get; init; }

    /// <summary>Gets the least-privileged Keycloak client identifier.</summary>
    public required string ReaderClientId { get; init; }

    /// <summary>Gets the reader client secret from managed secret configuration.</summary>
    public required string ReaderSecret { get; init; }

    /// <summary>Gets a value indicating whether the isolated test dependency is a standalone Redis instance.</summary>
    public bool StandaloneRedis { get; init; }

    /// <summary>Loads and validates the documented directory settings.</summary>
    /// <param name="configuration">Host configuration.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="InvalidOperationException">A required directory setting is missing.</exception>
    public static CommerceDirectoryOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new CommerceDirectoryOptions
        {
            RedisConnectionString = Required(configuration["Commerce:Directory:RedisConnectionString"], "Commerce:Directory:RedisConnectionString"),
            ReaderClientId = string.IsNullOrWhiteSpace(configuration["Commerce:Directory:ReaderClientId"]) ? "tenant-reader" : configuration["Commerce:Directory:ReaderClientId"]!,
            ReaderSecret = Required(configuration["Commerce:Directory:ReaderSecret"], "Commerce:Directory:ReaderSecret"),
            StandaloneRedis = configuration.GetValue("Commerce:Directory:StandaloneRedis", defaultValue: false),
        };
    }

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(key + " is required.") : value;
}
