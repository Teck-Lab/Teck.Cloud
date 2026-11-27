# Licensing Application

- Keep each query/handler together under `Entitlements/Features/<UseCase>/V1`; ports,
  read models, specifications and integration handlers stay capability-scoped.
- `LicenseDesiredHandler` is the only mutation path. It processes a full desired
  snapshot, ignores lower/equal generations, supersedes the prior active grant,
  increments the monotonic license revision only for active grants, and publishes
  `LicenseStateEvent` through the same Wolverine/EF transaction as the grant. Never
  publish the state before the grant commits or create a second outbox.
- `GetCurrentLicenseHandler` and `VerifyLicenseHandler` are read-only. They inspect the
  stored signed document, exact binding/generation/revision, expiry and then call the
  `IOrdersEntitlementClient` port; only an authoritative Orders answer may succeed.
  Any Orders failure fails closed as `dependency_unavailable`.
- `GetCurrentLicense` returns scoped `not_found` for absent, expired, superseded,
  obsolete or Orders-ineligible generations; it never returns an unverifiable
  envelope. `VerifyLicense` returns stable rejection codes for signature, key,
  binding, expiry and revocation, and an error for unavailable Orders.
- Handlers return `ErrorOr` and stable `CommerceErrorCodes`; Host maps to gRPC. Query
  handlers use read repositories and must not take a write repository or unit of work.
- Use the shared `ITenantMessage` envelope scope; never trust request fields for
  tenant authority. See [messaging](../../../../../docs/messaging.md),
  [validation](../../../../../docs/validation.md) and
  [specifications](../../../../../docs/specifications.md).
