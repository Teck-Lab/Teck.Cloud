namespace SharedKernel.Infrastructure.Auth;

/// <summary>Secret-supplied OAuth client settings. Keep this object out of structured logs.</summary>
public sealed class OAuthClientOptions
{
    /// <summary>Gets the trusted token endpoint.</summary>
    public required Uri TokenEndpoint { get; init; }

    /// <summary>Gets the OAuth client identifier.</summary>
    public required string ClientId { get; init; }

    /// <summary>Gets the client secret loaded from the host's secret source.</summary>
    public required string ClientSecret { get; init; }

    /// <summary>Gets the transport trust boundary; HTTPS is required by default.</summary>
    public IdentityTransport Transport { get; init; }

    /// <inheritdoc/>
    public override string ToString() => "OAuthClientOptions { credentials redacted }";
}
