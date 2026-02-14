# Auth Setup

## Intent

Keep authentication and authorization aligned with the repository's Keycloak + BFF token-exchange model.

## Current Architecture

- Identity provider: Keycloak.
- Service auth: JWT bearer validation via shared auth extensions.
- BFF behavior: token exchange per downstream audience, then proxy request forwarding.
- Tenant context: resolved from claims and forwarded as `X-TenantId`.

## Rules

- Reuse shared auth setup from `SharedKernel.Infrastructure.Auth` for APIs.
- Do not introduce alternate auth stacks per service unless explicitly required.
- Keep protected endpoints behind resource/scope authorization policies.
- In BFF routes, use per-route audience metadata for token exchange.
- Cache exchanged tokens with expiry-aware TTL to reduce token endpoint pressure.
- Propagate tenant context consistently (`tenant_id` claims and `X-TenantId` forwarding).
- Treat inbound tenant headers from external callers as untrusted unless explicitly validated.

## Key Integration Points

- Gateway token exchange middleware and service implementation.
- Multi-tenant claim/header resolution in shared multi-tenant extensions.
- Service defaults for organization-claim based tenant resolution.
