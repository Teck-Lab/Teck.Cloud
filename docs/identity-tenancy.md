# Identity, tenant lookup and delegation

Use [Keycloak.AuthServices composition](keycloak-authservices.md), not custom JWT,
organization-claim, Admin API or UMA implementations. The
[identity plan and diagrams](identity-platform-plan.html) track the larger platform.

## One tenant identity and one resolution flow

The canonical tenant ID is the immutable Keycloak organization UUID. It is both
Finbuckle's `Id` and `Identifier`, Wolverine's native `TenantId`, and the value used
by our operation scope and tenant-owned persistence. An organization alias or
name can change; it is not a database tenant ID. Organization membership and
permissions are separate from an organization's enabled status.

Customer HTTP requests carry an access token, not a tenant header. Authenticate
and validate signature, issuer, audience and lifetime with Keycloak.AuthServices,
then authorize the endpoint. Request `organization:<alias>` when selecting an
organization. `OperationScope.Tenant` uses the upstream `GetOrganizations()` parser
and requires exactly one organization with an ID. It rejects absent, malformed
and ambiguous membership. Raw headers and legacy `tenant_id` claims are not authority.

The middleware asks the shared `ITenantDirectory` whether that organization is
active, then initializes immutable `OperationContext`. Finbuckle-backed EF contexts
and Wolverine handlers receive that same identity. Do not add a parallel header
resolver or independently select a tenant in each layer.

When a FastEndpoints endpoint dispatches tenant work, use Wolverine's native
`InvokeForTenantAsync` with the validated operation's tenant ID. Background messages
use native `DeliveryOptions.TenantId`. The receiver's `TenantMessageMiddleware`
checks envelope/payload agreement and consults the same directory before persistence.
An authenticated/authorized publisher is still required; envelope metadata alone
is not authentication. Wolverine may create a separate handler DI scope, so do not
assume an HTTP-scoped service instance automatically becomes the handler's instance.

Our current persistence model uses Finbuckle row isolation in a shared database.
Wolverine owns messaging, transactions and inbox/outbox. This does not enable
Wolverine's separate database-per-tenant or conjoined EF context builders. Do not
stack two EF tenant stamping/filter systems on the same context.

## Read-through Finbuckle store

Register the native Admin SDK, the existing Redis Sentinel/FusionCache foundation,
and `AddKeycloakTenantStore()`. This registers one scoped `KeycloakTenantStore` as
both `IMultiTenantStore<KeycloakTenantInfo>` and `ITenantDirectory`.

The store resolves a UUID using:

1. FusionCache memory cache.
2. FusionCache distributed Redis cache.
3. Keycloak Admin SDK over the explicitly configured HTTP/HTTPS transport on a miss.

It caches only ID, display name and the derived active/absent result. No tokens,
user memberships, arbitrary organization attributes or database credentials enter
this cache. Keys are isolated by server, realm and service-account client identity.
`GetByIdentifierAsync` accepts the same stable UUID as `GetAsync`, not an alias.

Freshness is **five seconds absolute**, measured from the source request's start.
A timestamp inside each entry prevents another replica or a delayed HTTP request
from extending that authorization window. There is no sliding renewal, eager
refresh or stale/fail-safe authorization fallback. A disabled or unknown tenant
returns null. Only a real 404 is treated as absence; permission failures and outages
propagate, never become a manufactured active tenant or a cached "missing" tenant.

This means disabling an organization can take up to five seconds to affect a new
operation that has a valid cache entry. Already-running operations are not cancelled.
After administrative changes, call `InvalidateAsync` to remove the ID through the
configured distributed cache/backplane. Events may later trigger the same hook;
the expiry bound remains necessary if an event or invalidation is missed. There is
no claim of atomic cross-system revocation. Use direct current-state verification
for a future operation whose policy cannot tolerate this window.

The store is read-only. Add/update/remove and unbounded enumeration are unsupported;
use an explicitly authorized Admin SDK operation for organization administration.
Redis is a cache, not the authority. SDK credentials must have only the required
realm permissions; a production host must never use bootstrap administrator credentials.

A cached active organization does **not** prove the current user's membership.
Membership comes from the validated token/native policy. Removing membership may
leave an already-issued token valid until expiry; choose short token lifetimes and
explicit current verification/revocation for sensitive workflows. Do not describe
the five-second organization-status bound as universal user-permission revocation.

## Administrative access without tenant headers

Use a separate admin client/gateway audience and explicit platform policies:

- Customer operation: selected token organization plus membership and permission.
- Admin operation within one tenant: route `/admin/tenants/{tenantId}/...` plus
  `OperationScope.AdministrativeTenant("tenantId", "OperateTenant")`. The middleware
  executes that registered platform policy before resolving the route target. No
  membership in the target organization is required, but ordinary tenant data
  operations still reject a disabled target.
- Organization management, including disabled organizations: a dedicated Admin SDK
  endpoint with an administrative policy. Do not route it through an active-tenant
  data guard; otherwise re-enabling an organization would be impossible.
- Cross-tenant reports/lists: separately authorized global/projection endpoints or
  explicitly bounded per-tenant queries. `OperationScope.Global` does not grant
  permission, disable EF tenant filters or turn a normal tenant repository into a
  cross-tenant repository. Never infer platform privileges from the admin GUI URL.

Record the actor, action and target tenant in application audit events. Do not put
secrets or access tokens in audit logs. Actual admin/customer business endpoints
are introduced with their services; the shared metadata is a boundary mechanism,
not a complete administrative application.

## Token exchange and external integrations

External clients authenticate with Keycloak, then call the public gateway using
`Authorization: Bearer <access-token>`. User-facing integrations use authorization
code plus PKCE; automated integrations use a separately permissioned confidential
client and client credentials. Never share a platform administrator's credentials.

The gateway validates the incoming token before delegation. For a selected user
organization, call `ExchangeForOrganizationAsync` with the alias from the validated
native organization claims. This explicitly requests `organization:<alias>`; the
plain exchange method is for flows without an organization context. Keycloak must
allow the target audience and organization scope. The downstream service validates
the delegated token and independently enforces its resource/scope permission and
tenant ownership. Exchange is not authorization by itself.

Machine-to-machine integrations require a trusted client-to-tenant binding and
narrow service permissions. A multi-tenant integration selects a target in the URL,
which is checked against its permitted tenants. Do not assume service-account
organization claims behave like user-login claims: that grant and delegated path
must have real acceptance evidence before it is advertised as supported.

## Transport and testing

Public endpoints and the canonical public issuer use HTTPS. Internal HTTP is
allowed only in an explicitly selected ambient mesh profile with STRICT mTLS and
workload access policies. Selecting an enum does not install those policies. Local
Testcontainers/Aspire use explicit isolated-development HTTP. Never disable TLS
certificate validation. The same realm-specific issuer must survive backend routing.

Testcontainers.Keycloak owns service integration dependencies; Aspire owns assembled
E2E, and Aspire/KIND owns deployed verification. Test-only direct-grant clients and
bootstrap credentials stay inside disposable fixtures. Real application clients
keep PKCE and direct access grants disabled. Test real allow/deny decisions, target
audiences, selected organization preservation, disabled/unknown tenants, cache
expiry, Redis sharing and backend errors. Readiness alone is not authentication proof.

References: [Keycloak Organizations](https://nikiforovall.blog/keycloak-authorization-services-dotnet/authorization/organizations.html),
[Finbuckle stores](https://www.finbuckle.com/MultiTenant/Docs/v10.2.0/Stores),
[Wolverine HTTP tenancy](https://wolverinefx.net/guide/http/multi-tenancy),
[Wolverine EF tenancy](https://wolverinefx.net/guide/durability/efcore/multi-tenancy).
