namespace SharedKernel.Infrastructure.Health;

/// <summary>The same Keycloak issuer used by host authentication and discovery checks.</summary>
/// <param name="Uri">The host-owned dependency.</param>
internal sealed record KeycloakAuthority(Uri Uri);
