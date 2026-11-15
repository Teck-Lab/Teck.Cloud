# Keycloak.AuthServices composition

Use the upstream packages for JWT authentication, organization membership,
Authorization Services and Admin API access. The shared helpers configure those
packages; they do not implement an alternative token validator, organization claim
parser or UMA protocol client. Versions are centralized in Directory.Packages.props.

## Service authentication and authorization

```csharp
builder.Services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
{
    ServerUrl = new Uri("https://identity.example.test/"),
    Realm = "teck",
    Audience = "customer-service",
});
```

The helper registers native AddKeycloakWebApiAuthentication,
AddKeycloakAuthorization and AddAuthorizationServer. Hosts must place
UseAuthentication before UseAuthorization and operation-scope middleware. Keep
endpoint requirements explicit: use the library's RequireOrganizationMembership
and RequireProtectedResource policy builders, or IAuthorizationService for the
same requirements. Resource/scope names are service policy, not caller-controlled
strings. Authentication is not a substitute for resource authorization or database
isolation.

A tenant-scoped token must contain the selected Keycloak organization, including
its stable ID. A raw tenant header grants nothing. The tenant store resolves current
organization status; the organization membership requirement verifies the caller's
membership. These are separate checks. See [multi-tenancy](multi-tenancy.md).

The service validates the exact issuer, audience, signature, expiration and lifetime,
with a 30-second clock skew. A standard issuer-validation callback ensures discovery
cannot broaden the configured canonical issuer. Claims are not renamed by inbound mapping. Identity HTTP
clients have bounded timeouts and redirects disabled. JWTs and service credentials
must never be logged, stored in tenant-cache entries, or forwarded to a different
service without the intended audience/exchange contract.

## Ambient mTLS and the canonical issuer

```csharp
builder.Services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
{
    ServerUrl = new Uri("http://keycloak.identity.svc:8080/"),
    PublicIssuer = new Uri("https://identity.example.test/realms/teck"),
    Realm = "teck",
    Audience = "customer-service",
    Transport = IdentityTransport.AmbientMesh,
});
```

This uses the internal endpoint for metadata/authorization calls while preserving
the public HTTPS realm issuer for token validation. Configure Keycloak's public
hostname and reachable backchannel/discovery/JWKS URLs consistently. The external
browser issuer and redirect URLs remain HTTPS. Selecting AmbientMesh does not
install or prove the mesh: both workloads must be enrolled, STRICT peer authentication
and workload authorization must be applied, and the deployed journey must verify
that plaintext bypass is rejected. Use application TLS when that boundary is absent.

IsolatedDevelopment is the explicit alternative for disposable Testcontainers or
local development networks. Neither profile disables token signature, issuer,
audience or lifetime verification. Do not select it automatically based on an
untrusted request, a header, or whether an arbitrary hostname resembles a cluster.

## Admin API access

```csharp
builder.Services.AddFoundationKeycloakAdmin(
    new Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions
    {
        AuthServerUrl = "https://identity.example.test/",
        Realm = "teck",
        Resource = "customer-organization-reader",
        Credentials = new() { Secret = secretFromHostConfiguration },
    });
```

This registers both the hand-written SDK interfaces and the generated
KeycloakAdminApiClient. Kiota covers the Organizations API. Native options remain
available through IOptions<KeycloakAdminClientOptions> for the tenant store.
The service account needs native basic/roles scopes and optional kc-admin-api scope.
Keycloak 26.8.2 requires that Admin scope and its native Admin API audience; the helper
requests kc-admin-api through Duende. Granting that scope does not grant Admin roles.
Never disable the server audience check or add the Admin scope to frontend clients.

Duende.AccessTokenManagement acquires and caches service-account tokens and attaches
them with its built-in handler. Client and token requests use ten-second timeouts
and do not follow redirects. The helper supports one configured Admin identity per
host; do not register a second identity and assume it is isolated from the first.

Provision the service account in the application realm with only permissions needed
for its operations. The tenant store needs organization reads; Customer's explicit
organization-management operations need separate, deliberately granted write rights.
Do not use a master-realm admin or bootstrap credentials in an application. The helper
does not create users, roles or permissions and cannot grant privileges the account
has not been assigned. Writes use the Admin SDK directly, not tenant-store mutation
methods. The store's cache must be invalidated after confirmed relevant mutations.

## Optional package capabilities

All selected packages are available, but these behaviors require an explicit service
need and their own integration tests:

- AddKeycloakTokenIntrospection is for lightweight tokens and the upstream claims
  enrichment path. It is not enabled for every signed JWT. Configure its credentials,
  bounded/no-redirect HttpClient and cache duration deliberately; introspection caching
  creates its own freshness window and does not replace the organization-status check.
- AddUmaPermissionTicketChallenge adds the UMA permission-ticket HTTP challenge flow.
  Register the native authenticated Protection API client first. It is different from
  ordinary Authorization Services resource/scope checks, which are already registered.
  Do not change all API failures into UMA challenges unless the caller implements that
  protocol.

Keycloak.AuthServices.OpenTelemetry is included in the shared tracing and metrics
configuration. Its authorization activity/meter complements HTTP request spans;
existing redaction and tenant/cardinality policies still apply.

## Local composition and evidence

Aspire and Testcontainers consume the shared image descriptor in
src/aspire/Teck.Cloud.AppHost/Identity/runtime.json and the shared realm import.
The descriptor identifies the local optimized themed image, its digest-pinned upstream
base and its Nx build target. Aspire uses native WithDockerfile to build it automatically
with the selected container engine; Testcontainers/CI build that target before tests.
Keycloak uses a separate PostgreSQL database with generated credentials and starts
with --optimized --import-realm. The explicit HTTP mode is confined to local composition.

Aspire generates secret parameters for customer/admin gateway clients and injects
those into Keycloak's realm-import placeholders. There are no checked-in gateway
credentials. Application clients keep PKCE and reject password grants; disposable
integration fixtures may create a separate test-only login client.

KeycloakCompositionTests verifies registration and transport validation, not remote
identity behavior. IdentityCompositionTests verifies the Aspire model consumes the
shared image/build configuration and generates secret parameters.
KeycloakCompositionIntegrationTests exercises actual issuer/audience/signature/membership
validation and native Duende/Admin read-versus-write permissions using PostgreSQL-backed
Keycloak. Assembled
Aspire/browser tests remain necessary proof of the eventual complete frontend/gateway/service
journey. The native realm scopes are exported from the pinned Keycloak version; retain
basic, roles, profile and email when updating organization claim mappings. An organization
mapper alone suppresses the server defaults and can remove subject/role claims.

Upstream references:
[Organizations](https://nikiforovall.blog/keycloak-authorization-services-dotnet/authorization/organizations.html),
[Admin API](https://nikiforovall.blog/keycloak-authorization-services-dotnet/admin-rest-api/admin-rest-api.html),
[token management](https://nikiforovall.blog/keycloak-authorization-services-dotnet/admin-rest-api/access-token.html),
[telemetry](https://nikiforovall.blog/keycloak-authorization-services-dotnet/opentelemetry.html).

## Assembled identity proof

`tests/fixtures/Identity.AppHost` links the production dependency composition;
`Identity.TestHost` is a test-only BFF/gRPC record probe, not a business service.
`Identity.E2ETests` runs real Chromium login against the shared themed Keycloak,
then exercises token exchange, native resource/scope authorization and the actual
`ScopedDbContext`/Finbuckle tenant filters in PostgreSQL. The two tenants deliberately
use the same record key. The test checks the rendered result and persisted rows,
forged tenant headers, role denials, antiforgery denials, and organization disablement.
The administrator has no organization memberships: its separate client requests
ACR 2, exchanges without an organization scope, and sends an explicit target tenant
in the administrative RPC. The BFF requires fresh MFA and the platform role; the
service independently requires its configured admin `azp`, the platform role and
native UMA permission, then checks the target is active before establishing scope.
There is no global database fallback.

The fixture BFF uses native OIDC authorization code/PKCE (including native PAR),
HttpOnly authentication cookies and ASP.NET antiforgery for mutations. Successful
login requires Keycloak's S256 client policy; a test-only boolean records that the
native code redemption included a verifier, without exposing that verifier or token.
Browser automation accepts Aspire's ephemeral development certificate only in its
disposable browser context. Production HTTP handlers retain certificate validation.
This cookie browser contract is separate from bearer-token machine/API ingress.

Build the E2E project, then install its pinned browser using the generated
`playwright.ps1 install chromium` script before `bun nx run Identity.E2ETests:test:e2e`.
Linux CI uses `install --with-deps chromium`. Supply the normal
`TECK_REDIS_ADVERTISED_HOST` required by the real Sentinel topology. Aspire owns
and disposes every test resource. The `container:build` target for
`Identity.TestHost` builds `teck/identity-probe:local` using the selected Docker or
Podman engine for the separate Kubernetes acceptance lane.

## gRPC tenant transport

`AddOperationCredentials` sends the audience-scoped bearer token, never an
`x-tenant-id` header. Ordinary services derive the selected organization from the
validated token. HTTPS is the default; cleartext call credentials require an explicit
`AmbientMesh` or `IsolatedDevelopment` selection and URI validation. Default HTTP
fails before acquiring credentials. An administrative operation uses an explicitly
authorized target field, fresh BFF assurance, trusted admin issuance client and an active tenant-directory
check; it must not infer the target from organization membership or an untrusted
header. The target context is established before a tenant-scoped database is used.

The fixture uses Duende's native user access-token manager for renewal and stores
renewed tokens through the native authentication session. Its protected refresh
probe forces one real renewal and returns only a boolean indicating token rotation;
it never returns credentials. Logout uses the native cookie and OIDC handlers and
an antiforgery-protected form, including Keycloak's registered post-logout callback.
`Identity:Transport` defaults to `IsolatedDevelopment` for Aspire. The deployed full
KIND proof selects `AmbientMesh`, with an HTTPS public issuer and private mesh HTTP
metadata/token endpoints. OIDC's metadata HTTPS guard follows this explicit transport
selection; certificate validation remains enabled for HTTPS requests.

## Starter clients and BFF consumers

The shared realm's `teck-web`/`teck-admin` clients are public PKCE clients;
`customer-gateway`/`admin-gateway` are confidential exchange-only clients with browser
flow disabled. They are not preconfigured cookie BFFs. The assembled test provisions
separate confidential browser clients to verify that supported hosting choice.
A real BFF owner must enable its standard authorization-code flow, register exact
sign-in and post-logout callbacks, require S256, configure its client secret through
the secret provider, and retain `fullScopeAllowed: false`. The admin BFF must use the
native admin step-up flow and request fresh ACR 2 without requiring organization
membership. Business roles and their client scope grants belong to that consumer.

Native OIDC builds the cookie principal from the ID token. If a BFF uses native
realm-role policies, add a **client-specific** `oidc-usermodel-realm-role-mapper`
with `claim.name: realm_access.roles`, `jsonType.label: String`, `multivalued: true`,
`id.token.claim: true`, `access.token.claim: false`, and `userinfo.token.claim: false`.
Explicitly grant only the required realm-role scopes to that client. The normal
`roles` scope continues emitting scoped access-token roles; downstream services
independently authorize the exchanged access token. Do not copy all realm roles to
all ID tokens or treat the browser cookie as downstream service authorization.

The disposable fixture uses ephemeral ASP.NET Data Protection keys. A real BFF with
multiple replicas must use a shared, protected key ring, consistent application and
cookie settings, and a compatible shared session/token-store strategy. The fixture's
successful local cookie renewal is not proof of replica failover or production key
rotation; those are acceptance requirements of the owning BFF deployment.

ASP.NET Core's native OIDC handler deletes `acr` from its cookie principal by default.
A BFF enforcing fresh Keycloak assurance must set `MapInboundClaims = false` and
`options.ClaimActions.Remove("acr")` to retain the already validated ID-token claim.
Keep the native `acr` client scope and original `auth_time`; never synthesize assurance
from `iat`, a request header, or a freshly renewed token. The assembled regression
first observed one authenticated identity and a valid `auth_time` but no `acr`, then
checks the same native policy after retaining that signed claim. See the pinned
[ASP.NET Core OIDC defaults](https://github.com/dotnet/aspnetcore/blob/v10.0.15/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs).

## Assembled acceptance and token-exchange limitation

The independent ordinary-session Aspire test passes against the real shared image,
PostgreSQL and Redis Sentinel: PKCE, token renewal, scoped gRPC authorization,
two-tenant persistence, CSRF rejection, logout/fresh login and tenant suspension.
The administrator test now follows the explicitly approved boundary documented in
[identity MFA](identity-mfa.md): fresh MFA at the admin BFF, and native JWT validation,
exact trusted admin `azp`, platform role, UMA permission and active target downstream.
Keycloak changes browser ACR 2 to ACR 1 during native exchange; no claim is fabricated
or copied to hide that behavior. The downstream service does not independently
prove current MFA. An issued bearer token remains replayable until expiry, so its
lifetime and the admin client credential are part of the trust boundary. `azp` does
not attest the caller's network/workload identity. The final assembled suite passed both tests: a no-membership MFA administrator reads
the explicitly targeted tenant; an ordinary-client token with the same platform
role and UMA read permission is rejected downstream; disabled targets are denied.
Native refresh after the test-only 10-second freshness window preserves original
`auth_time`, and the next administrative API request is denied. The production
default freshness window remains five minutes. Ordinary renewal, logout/fresh
login, CSRF, two-tenant persistence and signed-in suspension also pass.
