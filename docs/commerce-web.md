# Commerce web authentication

Storefront and Admin are separate Next.js App Router applications. Each owns its
own better-auth 1.7.28 instance in `src/apps/<app>/src/server/auth.ts`, its own
confidential Keycloak client, its own auth-only PostgreSQL database/role and its
own cookies. Neither app connects to commerce service databases, the broker or a
gateway store. Business data is reached only through the matching customer/admin
gateway with a Keycloak bearer token.

## Runtime configuration and initialization

The server reads these process variables only when authentication is first used
at runtime. Importing a page, action or route during `next build` must not read
configuration, fetch the issuer or open the auth database.

| Variable                 | Purpose                                                                |
| ------------------------ | ---------------------------------------------------------------------- |
| `BETTER_AUTH_URL`        | Exact public app origin, also the callback and trusted origin.         |
| `BETTER_AUTH_SECRET`     | App-specific session/encryption key, at least 32 characters.           |
| `AUTH_DATABASE_URL`      | PostgreSQL URL for this app's auth-only database.                      |
| `KEYCLOAK_ISSUER`        | Realm issuer, for example `https://identity.example.test/realms/teck`. |
| `KEYCLOAK_CLIENT_ID`     | This app's confidential Keycloak client id.                            |
| `KEYCLOAK_CLIENT_SECRET` | The matching client secret; server-only.                               |
| `COMMERCE_GATEWAY_URL`   | This app's matching gateway origin (used by the gateway client).       |

`readAuthEnv` rejects missing/blank values, non-PostgreSQL data URLs, secrets
shorter than 32 characters, URLs with credentials/query/fragment, and HTTP URLs
that are not explicitly loopback. Errors name only the variable. `getAuth()` is
lazy and caches one process-scoped instance; a missing configuration fails the
protected path closed instead of creating a memory/cookie fallback.

HTTPS is required except for an explicitly isolated loopback HTTP origin. Cookies
are `Secure` whenever the configured base URL is HTTPS, which is every deployed
environment. In the loopback exception the browser and server share the same
host and no external traffic is admitted.

## Keycloak OIDC flow

- `genericOAuth` with the `keycloak` helper performs realm issuer discovery.
  `requireIdTokenVerification: true` means the provider registers only when
  discovery supplies an issuer and JWKS, so provider identity is the verified
  ID-token subject rather than an unverified profile field.
- The authorization-code flow always uses PKCE. The redirect URI is exactly
  `${BETTER_AUTH_URL}/api/auth/callback/keycloak`; the client registrations are
  separate and confidential.
- Both apps request exactly `openid profile email`. Storefront additionally
  sends `organization:<alias>` from its sign-in form. The alias is one nonempty
  value of at most 128 characters from `[A-Za-z0-9._-]`; there is no default or
  omitted tenant. The alias is a Keycloak scope hint only. The gateway validates
  the real organization claim and membership; a tenant header, URL parameter,
  email or client context is never authority.
- Admin never sends an organization scope. A tenant UUID chosen later in the UI
  identifies only the gateway operation target.
- Sign-in and sign-out are same-origin server actions. The action checks the
  configured app origin (`Origin`, else `Sec-Fetch-Site: same-origin`) and allows
  only a relative same-app return path. The sign-in action calls
  `auth.api.signInSocial` with `provider: "keycloak"`; the sign-out action calls
  `auth.api.signOut` without forwarding provider tokens.
- The app never asks Keycloak for a token in the browser. Keycloak issues the
  gateway-audience bearer through the normal client/audience mapper.

## Better Auth HTTP surface

Only `GET /api/auth/callback/keycloak` is routed to the Better Auth handler.
There is no catch-all Better Auth route. Every other path under `/api/auth`,
including `/api/auth/get-access-token` and `/api/auth/refresh-token`, returns 404
even with a valid session. The callback route rejects non-matching paths itself
and returns 503 without details if runtime configuration is unavailable.

## Sessions, cookies and disabled features

The instance disables password authentication, email-based/implicit account
linking, account-cookie transport and session cookie caching. OAuth tokens are
encrypted at rest with `account.encryptOAuthTokens: true`. Keycloak `(issuer, sub)`
is the provider identity; it is not interchangeable with a local account or
another provider.

| Boundary      | Storefront               | Admin         |
| ------------- | ------------------------ | ------------- |
| Cookie prefix | `teck-storefront`        | `teck-admin`  |
| Session field | `commerceOrganizationId` | none          |
| Scopes        | base + one org hint      | base          |
| Gateway       | customer gateway         | admin gateway |

Cookies are host-only (no cross-subdomain cookies), `HttpOnly`, `SameSite=Lax`,
`Secure` for HTTPS, and scoped to `/`. `trustedOrigins` contains only the
configured app origin. The two apps use distinct secrets, cookie prefixes and
auth databases, so starting one app with the sibling's session cookie establishes
no session.

## Storefront organization binding

`commerceOrganizationId` is a server-only session field with `input: false`; a
browser cannot supply it. It is populated in the Storefront session-create hook
from the linked Keycloak account written by the verified callback. The claim shape
is the gateway's rich single-organization representation:

```json
{ "organization": { "<alias>": { "id": "<organization-uuid>" } } }
```

The hook requires exactly one linked Keycloak account and exactly one organization
entry with a UUID id, and refuses session creation if the account is missing,
ambiguous, undecryptable, the issuer is not the configured issuer, or the
organization is absent/malformed/not a UUID. Better Auth 1.7.28 creates or updates
the linked account before calling `createSession` for both new and returning
users, so no separate callback completion handler is needed and no existing
session is finalized during a protected request.

If the same linked account later switches organization and overwrites its
provider token, the older session still carries the previous organization UUID.
Every protected Storefront request compares the session UUID to the candidate
access token's single organization UUID; a mismatch fails closed and requires a
fresh sign-in.

## Gateway bearer retrieval

`getGatewayAccessToken(headers)` in each app:

1. Resolves the session with `auth.api.getSession({ headers })`.
2. Requires a Storefront session with a valid organization UUID.
3. Calls `auth.api.listUserAccounts({ headers })` and requires exactly one
   `providerId: "keycloak"` account owned by that session.
4. Calls `auth.api.getAccessToken` with the body `{ accountId }`, where
   `accountId` is the Better Auth account row id. The 1.7.28 selector schema
   requires no `providerId`; a browser-supplied account selector is impossible.
5. Narrows the returned token: configured issuer, subject equal to the linked
   account subject, and (Storefront) organization equal to the session field.

The returned token is sent only as `Authorization: Bearer ...` from Next to the
matching configured gateway with `cache: "no-store"`. It is never returned to the
browser, logged, serialized in React props or written to browser storage. No
session cookie is forwarded and Next does not mint local identity tokens.

No session or no unique linked account means no gateway request. An invalid or
unrefreshable token (`APIError`) requires sign-in. A session/auth-store transport
failure raises `AuthUnavailableError`, which the UI shows as temporary
unavailability without a gateway call. Gateway 401 requires sign-in; 403 shows
denied; mutations are not retried on either. The gateway alone validates
signature, audience, organization and staff permissions and alone exchanges into
internal service audiences.

## Opaque browser scopes

Only opaque HMAC values reach browser storage.

- Checkout intents, Storefront only:
  `base64url(HMAC-SHA-256(BETTER_AUTH_SECRET, JSON.stringify(["checkout-intents:v1", issuer, subject, organizationId])))`.
  The issuer/subject come from the authenticated linked account; the organization
  is the session-bound UUID. This is an independent scope from pagination.
- Pagination cursor history, both apps:
  `base64url(HMAC-SHA-256(BETTER_AUTH_SECRET, JSON.stringify(["pagination-session-scope:v1", app, issuer, userId, sessionId, organizationId ?? ""])))`.
  Cursor history is keyed by this opaque value plus the current `baseHref`.

Neither scope exposes raw claims, session ids, bearer/refresh tokens, cookies,
provider URLs or licenses.

## Database schema and migrations

Each app has a checked-in initial schema generated by the pinned better-auth
1.7.28 CLI:

- `src/apps/storefront/migrations/0001_better_auth.sql`
- `src/apps/admin/migrations/0001_better_auth.sql`

They contain only `user`, `session`, `account` and `verification` plus their
indexes. Storefront's `session` additionally has the nullable
`commerceOrganizationId` column. The tables are auth-only; no business table is
created.

`src/server/auth-cli.ts` exports an eager Better Auth instance solely for the CLI
(`tools/run-auth-migration.mjs` -> `auth migrate --config ./src/server/auth-cli.ts
--yes`). The Next runtime never imports it. `src/server/auth.ts` stays lazy so
`next build` does not connect to PostgreSQL, Keycloak discovery or the gateway.

Application startup never runs migrations and never falls back to memory or a
cookie store. `bun run db:migrate:auth` (or the `db:migrate:auth` Nx target) is
the explicit operation. The checked-in SQL and a fresh `auth migrate` were
verified to produce the same schema; a second migrate run reports no changes.

## Known boundaries

Production Keycloak clients, audience mappers, secrets, database provisioning and
HTTPS ingress are deployment responsibilities. This slice proves fixture-level
session/account/token behaviour without a live provider. Real OIDC, gateway
checkout and computed styles remain Stage 3 acceptance.
