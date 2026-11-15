namespace SharedKernel.Infrastructure.Auth;

/// <summary>Explicit trust boundary for credential-bearing identity requests.</summary>
public enum IdentityTransport
{
    /// <summary>Require application HTTPS. This is the default.</summary>
    Https = 0,

    /// <summary>Allow internal HTTP only on an operator-enforced STRICT ambient mesh.</summary>
    AmbientMesh = 1,

    /// <summary>Allow HTTP for an isolated local development or disposable test network.</summary>
    IsolatedDevelopment = 2,
}
