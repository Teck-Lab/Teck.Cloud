namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>A sanitized change hint, never an authoritative organization state.</summary>
/// <param name="Id">The native Keycloak admin event UUID.</param>
/// <param name="Time">The provider occurrence time in Unix milliseconds; not an ordering authority.</param>
/// <param name="Realm">The configured source realm name.</param>
/// <param name="ResourceType">The allowlisted organization resource type.</param>
/// <param name="Operation">The allowlisted mutation.</param>
/// <param name="ResourcePath">The exact organization UUID resource path.</param>
public sealed record KeycloakOrganizationNotice(Guid Id, long Time, string Realm, string ResourceType, string Operation, string ResourcePath);
