# Licensing Infrastructure

- `LicensingWriteDbContext` is the Wolverine EF transaction/outbox owner;
  `LicensingReadDbContext` is untracked and save-guarded. Both apply the shared
  tenant filter and the same mappings; keep provider-specific code here.
- PostgreSQL schema `licensing` owns `license_generations` (unique binding, current
  desired generation, last license revision, concurrency fence) and `license_grants`
  (unique binding + desired generation, state, exact signed document). Migrations
  live in `Database/Migrations` and target the write context. Messaging uses schema
  `licensing_messages`; do not add a custom outbox.
- `StandardLicenseDocumentService` is the only `Standard.Licensing` adapter. It
  validates the active private key and public ring at construction, signs a complete
  binding/tier/add-on/expiry/revision/key-id claim set, and verifies signature and
  claims before returning them. Malformed, unknown-key and invalid-signature input
  never throws caller-visible details.
- `LicensingKeyRingOptions` reads managed-secret configuration; only the active key
  may carry private material. Startup fails closed without usable active material.
- `OrdersEntitlementClient` uses the managed Orders gRPC client and shared workload
  credentials/tenant metadata. Every non-OK result is unavailable, never an implied
  eligibility.
- See [shared runtime](../../../../../docs/shared-runtime.md),
  [persistence](../../../../../docs/specifications.md) and
  [multi-tenancy](../../../../../docs/multi-tenancy.md).
