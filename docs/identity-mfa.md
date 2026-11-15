# Identity MFA and session assurance

The shared `teck-local` realm uses Keycloak's built-in credentials and flows. It
ships no third-party MFA or GeoAware provider. Customers can enroll an authenticator
through the account/enrollment journey; enrollment is optional. Once configured,
the native customer conditional MFA flow asks for the user's second factor.

The `teck-admin` public client uses a separate native step-up browser flow and a
minimum ACR of `2`. Password authentication supplies level `1`; password plus
WebAuthn, TOTP or an unused recovery code supplies level `2`. The realm explicitly
maps `1` and `2` to those levels. Requesting `acr_values=1` does not lower the admin
client minimum. A password-only customer session is not an admin credential; users
must enroll a second factor before entering the administrative application. This
foundation does not silently enroll or skip MFA on an admin login failure.

WebAuthn requires user verification. The local browser fixture uses a virtual CTAP2
authenticator on localhost; production requires HTTPS and a stable relying-party
origin. The passwordless registration policy is prepared, but the shipped admin
flow is password plus a second factor, not a claim of deployed passwordless login.
TOTP uses SHA-256, six digits and 30-second counters; reuse is disabled. Removing a credential does not itself revoke every previously issued token/session;
an immediate revocation workflow must also revoke sessions, with BFF authentication freshness and downstream token lifetime remaining enforced. Recovery
codes are one-use second factors, not reusable passwords or a bypass of the first
factor. Codes, credential secrets, assertions and tokens must never enter logs.

## Approved BFF and downstream enforcement boundary

The admin BFF enforces native ACR 2 and original `auth_time` freshness on every
administrative API request and before delegation. Hosts register
`AddKeycloakAssurance()` and use
`RequireKeycloakAssurance("2", TimeSpan.FromMinutes(5))` alongside the platform-role
policy. The helper accepts exactly one authenticated identity, one matching `acr`
and one integer `auth_time`, rejecting missing, duplicate, malformed, future or stale
claims. Native refresh may renew tokens but never renews the authentication age.
Only the admin host exposes administrative routes; login, logout and session renewal
remain native session operations, not a way to bypass the administrative policy.

The admin flow stores level 2 for five minutes. OIDC `max_age=0` requests fresh
interactive authentication. Preserve the validated ID-token `acr` in a cookie BFF
with `options.ClaimActions.Remove("acr")`; ASP.NET Core otherwise deletes it.

Keycloak's native standard token exchange produces ACR 1 even after browser ACR 2,
while retaining original `auth_time` and scoped roles. This is documented
[non-browser grant behavior](https://github.com/keycloak/keycloak/blob/26.8.2/docs/documentation/server_admin/topics/authentication/flows.adoc),
not a reason to fabricate or copy assurance claims. The approved design enforces
fresh MFA at the admin BFF and keeps independent downstream authorization:

- Native JWT signature, canonical issuer, exact service audience and expiry.
- `RequireKeycloakAuthorizedParty(configuredAdminClientId)`: one authenticated
  identity and exactly one `azp` from an immutable, server-owned ordinal allowlist.
- Platform role plus native Keycloak resource/scope authorization.
- Explicit target tenant resolved through the authoritative directory and active
  before a tenant-scoped database is accessed; no organization membership or global
  fallback is assumed for platform administrators.

`azp` identifies the issuer's authorized client, **not** the network sender and not
independent proof of current MFA at the service. An already issued bearer token can
be replayed until expiry; use short-lived delegated tokens, protect the admin client
credential, and apply separately provisioned workload/network restrictions. No
arbitrary caller-supplied subject-token exchange endpoint is exposed by this BFF.
The current KIND customer probe does not prove a distinct admin workload identity.

The shared `teck-admin` and `admin-gateway` clients use a 60-second access-token
lifespan. Native service validation allows 30 seconds of clock skew, so an issued
token can remain acceptable for roughly 90 seconds from issuance; subject-token or
session constraints can shorten that window. This is not instantaneous revocation.

## Regression evidence

The unit suite checks missing, wrong, duplicate and cross-identity `azp`, immutable
configuration and rejection that another handler cannot override. The assembled
suite uses a no-membership MFA administrator for positive explicit targeting, and
an ordinary exchange client whose user really has the same platform role and UMA
read permission for the negative case. A shorter test-only freshness window exercises
native token refresh after authentication becomes stale; the same administrative
API must still deny it. The real assembled Aspire suite passed 2/2 tests, including
that stale-after-refresh denial, the no-membership administrator positive path and
the privileged ordinary-client negative. The authorized-party unit suite passed
10 cases. Test fixtures shorten freshness to 10 seconds; the default remains
five minutes. These results do not claim deployed admin workload isolation.

## Reproducible verification

`KeycloakMfaTests` is a focused identity-provider integration suite: PostgreSQL and
the same source-built themed Keycloak image are owned by Testcontainers. Playwright
exercises native PKCE login, optional customer enrollment, admin downgrade refusal,
WebAuthn, invalid/reused TOTP and single-use recovery pages. This is a protocol/page
boundary, not a substitute for assembled application acceptance. An owned ephemeral loopback listener receives the callback with logging disabled;
the actual authorization code is redeemed at Keycloak. Only the fixture clients'
redirect URLs are changed to that allocated address. It does not fabricate tokens or use test-only authentication handlers.

The separate `Identity.E2ETests` Aspire suite owns the browser/BFF/gRPC service
journey and verifies downstream authentication and authorization. The unit suite
covers exact assurance, duplicate identities and timestamp boundary decisions.
All use the shared realm/runtime descriptor. The integration Nx target builds the
candidate image first. Install the pinned Playwright Chromium browser after the
.NET test build (`pwsh <test-output>/playwright.ps1 install --with-deps chromium`).
CI does this automatically. `TECK_PLAYWRIGHT_CHANNEL=msedge` can select an installed
local Edge for development; CI uses the package's Chromium.
