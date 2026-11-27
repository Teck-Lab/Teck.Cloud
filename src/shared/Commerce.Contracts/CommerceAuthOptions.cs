using SharedKernel.Infrastructure.Auth;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Trusted commerce identity endpoints and this host's OAuth client.</summary>
/// <remarks>
/// Bind with <see cref="CommerceAuth.FromConfiguration"/> from
/// <c lang="text">Commerce:Identity:ServerUrl</c> (required), <c lang="text">Realm</c> (required), <c lang="text">PublicIssuer</c>,
/// <c lang="text">Transport</c> (<c lang="csharp">Https</c>, <c lang="csharp">AmbientMesh</c> or <c lang="csharp">IsolatedDevelopment</c>),
/// <c lang="text">TokenEndpoint</c>, <c lang="text">ClientId</c> and <c lang="text">ClientSecret</c>. Outbound hosts must supply the
/// token endpoint and confidential client; the tenant directory remains host-owned composition.
/// </remarks>
public sealed class CommerceAuthOptions
{
    /// <summary>The settings section read by <see cref="CommerceAuth.FromConfiguration"/>.</summary>
    public const string ConfigurationSection = "Commerce:Identity";

    /// <summary>Gets the trusted Keycloak server URL, including any path prefix.</summary>
    public required Uri ServerUrl { get; init; }

    /// <summary>Gets the fixed realm name.</summary>
    public required string Realm { get; init; }

    /// <summary>Gets the canonical public issuer, required when the backchannel uses ambient HTTP.</summary>
    public Uri? PublicIssuer { get; init; }

    /// <summary>Gets the explicit transport trust boundary.</summary>
    public IdentityTransport Transport { get; init; }

    /// <summary>Gets this host's OAuth token endpoint, required only when it delegates or exchanges.</summary>
    public Uri? TokenEndpoint { get; init; }

    /// <summary>Gets this host's confidential OAuth client identifier, required only for outbound credentials.</summary>
    public string? ClientId { get; init; }

    /// <summary>Gets this host's secret-supplied OAuth client secret, required only for outbound credentials.</summary>
    public string? ClientSecret { get; init; }

    /// <inheritdoc/>
    public override string ToString() => "CommerceAuthOptions { credentials redacted }";
}
