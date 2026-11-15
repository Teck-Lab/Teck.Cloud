# Identity change hints and reconciliation

The authoritative tenant directory is the Keycloak organization GET through
`KeycloakTenantStore`; snapshots expire after five seconds without fail-safe stale
reads. Events improve refresh latency. They never replace the canonical check or
prove membership, application entitlement, database routing or authorization.

## Owning host

A service which already owns identity synchronization may opt in through
`ListenToKeycloakOrganizationEvents(queue, source, realm)` when configuring
Wolverine. Configure its PostgreSQL durable inbox, RabbitMQ connection, concrete
Keycloak tenant-store registration and restricted broker credentials in that host.
No separate business service or background inventory has been invented here.

Use a dedicated exchange and queue. Provision them administratively. The identity
publisher receives write permission only on this exchange; ordinary application
publishers receive no identity-exchange write permission. Consumers need their own
queue/read permissions and no provider-administrator credentials. Use encrypted,
authenticated broker transport outside isolated test networks.

The body is bounded to 2 KiB and permits only `id`, `time`, `realm`,
`resourceType`, `operation`, and `resourcePath`. Only ORGANIZATION CREATE, UPDATE
and DELETE with an exact organization UUID path and configured realm are accepted.
No representation, user profile, token, credential, arbitrary headers or handler
name is accepted. The mapper replaces incoming bytes with this canonical metadata
before durable storage; rejects erase their body before error handling. Its
configured destination is established before mapping errors so Wolverine's durable
poison path can store an observable, sanitized failure.

A provider event UUID is namespaced by the host-configured issuer and realm into
the durable inbox ID. Duplicate delivery during the inbox retention period is
suppressed. Replays after retention remain safe: every accepted hint invalidates
and rereads the canonical organization. An old CREATE/UPDATE cannot undo a current
disablement or deletion. Event timestamps are evidence, not a last-write-wins
state machine. Even a failed administrative mutation can safely request a reread;
its payload never asserts the resulting state.

Watch the owning host's Wolverine error queue, delivery failures and reconciliation
failures. Provider outages propagate; no failed request is converted into a cached
"missing tenant" or accepted stale snapshot. Native retries/error operations may
replay only the sanitized message. Persist host-owned reconciliation checkpoints
only after the page succeeds.

`KeycloakTenantReconciler.ReconcileAsync` refreshes a bounded page of up to 256 UUIDs
from the host's trusted, durable inventory, deduplicating repeated IDs. Include
previously known IDs so deletions are discovered. It does not enumerate aliases or
invent an inventory. Schedule it in the owner with cancellation, bounded retry and
failure alerts. Broker availability is not required; rerun failed pages after the
provider recovers. The five-second request-path bound continues independently.

## KETE evaluation gate

KETE is **not enabled or shipped in the normal application image**. The isolated
`tests/fixtures/keycloak-kete/Dockerfile` pins upstream release
`2026.08.27.08.03` with its release SHA-256, and is disabled until explicitly
configured. This is an evaluation artifact, not production rollout configuration.

Upstream's ProviderFactory automatically enables all stored user events,
administrative events and administrative representations on matched realms. Its
configuration has no independent opt-out for that behavior. Keycloak strips known
credentials from admin representations, but representations still include personal
profiles and arbitrary attributes. A sanitized broker template does **not** remove
that database retention. Do not activate KETE on a real realm until its retention,
privacy controls and operational ownership have been deliberately resolved.

The template serializer can restrict transmitted data to the six fields above,
using JSON escaping for every string. Select only organization CRUD events in one
realm, a dedicated publisher and publisher confirmations. KETE's asynchronous
publisher is not a transactional durable outbox for Keycloak mutations; broker
outage or process loss can lose notifications. Reconciliation is mandatory.

This module does not invalidate already issued membership claims or sessions.
User/membership changes require a separately designed session/revocation policy;
they are deliberately outside this organization-status contract.

Sources: [pinned KETE source](https://github.com/FortuneN/kete/tree/2026.08.27.08.03),
[template serializer](https://fortunen.github.io/kete/user-guide/serializers/template/),
[AMQP destination](https://fortunen.github.io/kete/user-guide/destinations/amqp-0.9.1/),
[Keycloak secret stripping](https://github.com/keycloak/keycloak/blob/26.8.2/server-spi-private/src/main/java/org/keycloak/models/utils/StripSecretsUtils.java).

## Verification and business projection boundary

The real-container suite verifies delivery through Wolverine's PostgreSQL inbox,
duplicate/reordered notifications, disabled/deleted canonical organizations,
sanitation of poison messages before database persistence, restricted publishers,
and reconciliation despite broker/provider failures. The explicit KETE evaluation
checks real provider output and its representation-retention side effect using the
separate image. These are identity integration tests, not an application delivery
or business-projection acceptance test.

No Customer service projection, customer lifecycle event, billing entitlement or
business state is implemented by this shared module. When an owning service is
introduced, it must define its domain projection and integration-event contract,
update them transactionally using Wolverine's EF inbox/outbox, and test recovery
against that actual schema. Do not emit a "tenant changed" business event directly
from an untrusted provider hint. This module only refreshes canonical metadata.

The existing `Teck.Cloud` OTEL meter exports
`teck.identity.reconciliation.pages`,
`teck.identity.reconciliation.organizations`, and
`teck.identity.reconciliation.duration` (seconds), with only the bounded `outcome`
tag (`success`, `failure`, `cancelled`). No organization/user IDs or raw payloads
are labels. Partial progress is counted even if a page later fails; the host must
still retry the page and must not advance its checkpoint on that failure. Native
Wolverine delivery/error telemetry supplies the event-processing failure signal.
