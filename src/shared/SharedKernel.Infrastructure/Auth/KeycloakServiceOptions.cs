namespace SharedKernel.Infrastructure.Auth;

/// <summary>Trusted endpoints and audience for a Keycloak-protected service.</summary>
public sealed class KeycloakServiceOptions
{
    /// <summary>Gets the trusted backchannel base URL, including any server path prefix.</summary>
    public required Uri ServerUrl { get; init; }

    /// <summary>Gets the fixed realm name.</summary>
    public required string Realm { get; init; }

    /// <summary>Gets this API's exact token audience.</summary>
    public required string Audience { get; init; }

    /// <summary>Gets the canonical public realm issuer, required when the backchannel uses ambient HTTP.</summary>
    public Uri? PublicIssuer { get; init; }

    /// <summary>Gets the explicit transport trust boundary. HTTPS is required by default.</summary>
    public IdentityTransport Transport { get; init; }
}
