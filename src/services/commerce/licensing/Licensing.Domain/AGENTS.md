# Licensing Domain

- Group source under `Aggregates/<Aggregate>/`; the roots are `LicenseGrant` and
  `LicenseGeneration`. Do not add technical folders such as Entities or ValueObjects.
- `LicenseGrant` is an immutable per-generation snapshot: tenant/order/subscription
  binding, accepted desired revision, tier, canonical sorted/deduplicated add-ons,
  monotonic signed-license revision, key id, exact UTC expiry and signed document.
  An inactive or superseded grant never exposes a document.
- `LicenseGeneration` is the durable fence: it accepts only a strictly higher
  desired revision, never regresses the license revision, and carries an
  application-managed concurrency fence. Do not derive authority from order or
  basket revisions.
- Factories return `ErrorOr` with stable errors beside the aggregate. Tenant ids are
  canonical UUID `D` strings; expiry is a future UTC instant. Never embed endpoints,
  hosts, credentials or connection strings in a grant.
- Domain is framework-neutral: no EF, Wolverine, Keycloak, Standard.Licensing or
  other service implementation. See [service structure](../../../../../docs/service-structure.md#aggregate-first-domain-and-errors)
  and [multi-tenancy](../../../../../docs/multi-tenancy.md).
- Architecture coverage lives in the commerce service architecture suite; real
  issuance/rotation/replay proof belongs in `tests/integration/Commerce.IntegrationTests`.
