using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finbuckle.MultiTenant.Abstractions;
using Keycloak.AuthServices.Sdk.Kiota;
using Keycloak.AuthServices.Sdk.Kiota.Admin;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using SharedKernel.Core.MultiTenancy;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Read-only Finbuckle store and shared active-tenant directory, backed by FusionCache and the Keycloak SDK.</summary>
/// <param name="client">Authenticated, least-privileged Admin SDK client.</param>
/// <param name="cache">The host's Redis-backed FusionCache.</param>
/// <param name="options">Trusted Keycloak realm and client configuration.</param>
/// <param name="clock">Optional clock for the hard authorization freshness bound.</param>
public sealed partial class KeycloakTenantStore(KeycloakAdminApiClient client, IFusionCache cache, IOptions<KeycloakAdminClientOptions> options, TimeProvider? clock = null)
    : IMultiTenantStore<KeycloakTenantInfo>, ITenantDirectory
{
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(5);
    private static readonly FusionCacheEntryOptions EntryOptions = new()
    {
        Duration = Freshness,
        DistributedCacheDuration = Freshness,
        IsFailSafeEnabled = false,
        AllowBackgroundDistributedCacheOperations = false,
        AllowBackgroundBackplaneOperations = false,
        ReThrowDistributedCacheExceptions = true,
        ReThrowSerializationExceptions = true,
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly string _namespace = Namespace(options.Value);
    private readonly string _realm = options.Value.Realm;

    /// <inheritdoc/>
    public Task<KeycloakTenantInfo?> GetAsync(string id) => GetAsync(id, CancellationToken.None);

    /// <summary>Resolves a stable organization ID with bounded freshness and cancellation.</summary>
    /// <param name="id">The organization UUID from a validated membership claim or trusted message.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The active organization metadata, or null for an unknown or disabled organization.</returns>
    public Task<KeycloakTenantInfo?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        return Guid.TryParseExact(id, "D", out var parsed)
            ? GetCachedAsync(parsed.ToString("D"), cancellationToken)
            : Task.FromResult<KeycloakTenantInfo?>(null);
    }

    /// <inheritdoc/>
    public Task<KeycloakTenantInfo?> GetByIdentifierAsync(string identifier) => GetAsync(identifier);

    /// <inheritdoc/>
    public async Task<bool> IsActiveAsync(string tenantId, CancellationToken cancellationToken) =>
        await GetAsync(tenantId, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>Invalidates the organization in memory, Redis and the configured backplane.</summary>
    /// <param name="id">The stable organization UUID.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The invalidation task. Call after a successful organization mutation.</returns>
    public Task InvalidateAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Guid.TryParseExact(id, "D", out var parsed)
            ? cache.RemoveAsync(_namespace + parsed.ToString("D"), EntryOptions, cancellationToken).AsTask()
            : Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> AddAsync(KeycloakTenantInfo tenantInfo) => UnsupportedAsync<bool>();

    /// <inheritdoc/>
    public Task<bool> UpdateAsync(KeycloakTenantInfo tenantInfo) => UnsupportedAsync<bool>();

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string identifier) => UnsupportedAsync<bool>();

    /// <inheritdoc/>
    public Task<IEnumerable<KeycloakTenantInfo>> GetAllAsync() => UnsupportedAsync<IEnumerable<KeycloakTenantInfo>>();

    /// <inheritdoc/>
    public Task<IEnumerable<KeycloakTenantInfo>> GetAllAsync(int take, int skip) => UnsupportedAsync<IEnumerable<KeycloakTenantInfo>>();

    private static Task<T> UnsupportedAsync<T>() => Task.FromException<T>(new NotSupportedException("Use the authorized Keycloak Admin SDK for administration; the tenant store only resolves identities."));

    private static string Namespace(KeycloakAdminClientOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AuthServerUrl) || string.IsNullOrWhiteSpace(settings.Realm) || string.IsNullOrWhiteSpace(settings.Resource))
        {
            throw new InvalidOperationException("Tenant lookup requires a Keycloak server, realm and client identity.");
        }

        var identity = new Uri(settings.AuthServerUrl).AbsoluteUri + "\n" + settings.Realm + "\n" + settings.Resource;
        return "keycloak-tenants:v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))) + ":";
    }

    private async Task<KeycloakTenantInfo?> GetCachedAsync(string id, CancellationToken cancellationToken)
    {
        var key = _namespace + id;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var bytes = await cache.GetOrSetAsync<byte[]>(key, (_, token) => LoadAsync(id, token), options: EntryOptions, token: cancellationToken).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize(bytes, SnapshotJson.Default.Snapshot)
                ?? throw new InvalidOperationException("Tenant cache returned invalid metadata.");
            if (snapshot.Tenant is { } tenant && (!string.Equals(tenant.Id, id, StringComparison.Ordinal) || !string.Equals(tenant.Identifier, id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Tenant cache ownership is inconsistent.");
            }

            // A Redis hit or delayed request must not extend the original authorization window.
            var now = _clock.GetUtcNow();
            if (snapshot.ExpiresAt > now && snapshot.ExpiresAt <= now + Freshness)
            {
                return snapshot.Tenant;
            }

            await cache.RemoveAsync(key, EntryOptions, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Tenant status could not be refreshed within its freshness window.");
    }

    private async Task<byte[]> LoadAsync(string id, CancellationToken cancellationToken)
    {
        var expires = _clock.GetUtcNow() + Freshness;
        KeycloakTenantInfo? tenant = null;
        try
        {
            var organization = await client.Admin.Realms[_realm].Organizations[id]
                .GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (organization?.Enabled is null || !string.Equals(organization.Id, id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Keycloak returned invalid organization metadata.");
            }

            if (organization.Enabled.Value)
            {
                tenant = new KeycloakTenantInfo { Id = id, Identifier = id, Name = organization.Name };
            }
        }
        catch (ApiException exception) when (exception.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            // Only an authoritative absence is a negative cache entry; timeouts and permission failures propagate.
        }

        return JsonSerializer.SerializeToUtf8Bytes(new Snapshot { Tenant = tenant, ExpiresAt = expires }, SnapshotJson.Default.Snapshot);
    }

    private sealed class Snapshot
    {
        public required KeycloakTenantInfo? Tenant { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }
    }

    [JsonSerializable(typeof(Snapshot))]
    private sealed partial class SnapshotJson : JsonSerializerContext;
}
