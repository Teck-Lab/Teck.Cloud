# Application E2E

Read [testing](../../../docs/testing.md) and [local development](../../../docs/local-development.md).

- Use Aspire to start the actual application stack. Next.js user journeys use
  TypeScript Playwright via Nx/Bun; .NET may own API-only/foundation journeys.
- Exercise frontend -> customer/admin gateway -> services -> real persistence.
  Assert what the user sees and committed effects, including denied tenant access.
- Validate real login/PKCE and trace continuity when those are acceptance criteria.
  OIDC discovery alone does not prove login, and collector export alone does not
  prove trace/log correlation.
- For Sentinel failover read [Redis Sentinel](../../../docs/redis-sentinel.md).
  Configure a reachable advertised address, bound recovery, and verify the
  Sentinel-selected primary rather than a fixed old endpoint.
- Keep test data and resources isolated; capture diagnostics and clean up on failure.
  This lane does not choose a mobile testing framework.
