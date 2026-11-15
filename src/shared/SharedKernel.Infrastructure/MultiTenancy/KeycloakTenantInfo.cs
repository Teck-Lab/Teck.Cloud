using Finbuckle.MultiTenant.Abstractions;

namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>Minimal organization metadata. Identity and identifier are the stable Keycloak organization UUID.</summary>
public sealed class KeycloakTenantInfo : ITenantInfo
{
    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public required string Identifier { get; init; }

    /// <summary>Gets the organization display name, never an authorization identity.</summary>
    public string? Name { get; init; }
}
