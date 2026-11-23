# Commerce gateway contract v1

This directory is the normative, versioned source of truth for the commerce
customer gateway, the commerce admin gateway, the internal Orders and Licensing
gRPC contracts, and the durable `commerce.license-desired.v1` and
`commerce.license-state.v1` RabbitMQ events. `customer.openapi.yaml` and
`admin.openapi.yaml` are OpenAPI 3.1; `orders.proto` and `licensing.proto` are
protobuf; the two event JSON Schemas are draft 2020-12. Executable fixtures are
checked by `tools/commerce-contract.test.mjs`.

This change is contract-only. It adds no application behavior, persistence,
Aspire composition, deployment or GitOps. The existing draft
`docs/contracts/dedicated-postgresql/v1/openapi.yaml` and its
`/v1/dedicated-database` routes are preserved unchanged; the `/v1/commerce`
flow neither implements nor depends on them. Implementation PRs consume these
checked-in schemas and fixtures instead of keeping a second browser model or a
hand-maintained protocol copy.

## Authority and the three gates

- **Payment: Stripe Billing is the authority.** The payment fact comes from a
  signed event-triggered provider re-fetch that binds the order, recurring
  price, currency, amount and current paid period. Notification arrival order
  never establishes state, and a failed re-fetch never extends the verified
  paid window. A refund, cancellation or dispute confirmed by the provider
  disables entitlement; it never deletes data.
- **Approval is a separate, auditable and final staff decision.** It records the
  authenticated staff actor, the trimmed reason, the server UTC time and the
  order ID. The first decision is final.
- **Readiness comes from a current allowlisted placement plus a read-only
  tenant/order/subscription/placement marker check.** The Admin evidence
  reference is an audit string, not proof, and is never fetched. Shared remains
  tenant isolated; a dedicated target is a tenant-specific database, not a
  cluster; optional replica access additionally requires a read-only standby
  with the same binding.

Effective entitlement requires paid **and** approved **and** a matching
nonstale readiness proof. A higher monotonic desired generation supersedes prior
facts. Orders publishes desired state through its outbox; Licensing answers with
a versioned event that Orders applies through a durable inbox. License expiry is
clamped to `min(paidPeriodEnd, probeObservedAt + 15 minutes)`. The customer UI
distinguishes pending payment, pending approval, pending readiness, active,
rejected and failed.

## State and permission matrix

Customer status is derived; it is never supplied by a client.

| `status`           | Paid | Approved | Current proof    | `license` on customer order |
| ------------------ | ---- | -------- | ---------------- | --------------------------- |
| `pendingPayment`   | no   | —        | —                | `null`                      |
| `pendingApproval`  | yes  | pending  | —                | `null`                      |
| `pendingReadiness` | yes  | approved | missing/stale    | `null`                      |
| `active`           | yes  | approved | current          | signed envelope             |
| `rejected`         | —    | rejected | —                | `null`                      |
| `failed`           | —    | —        | terminal failure | `null`                      |

`active` is the only status that exposes the signed document, and only for the
caller's own tenant and subject. An order outside that scope returns the same
`404 not_found` shape as an absent order.

| Surface                             | Required permission                                  | Enforced                                                                                          |
| ----------------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Customer catalog, basket and orders | `commerce.customer` on the customer gateway audience | Gateway validates the token and delegates; Orders independently enforces tenant and subject scope |
| Admin order list and detail         | `CommerceOrdersRead`                                 | Gateway and Orders                                                                                |
| Admin approve/reject decision       | `CommerceOrdersDecide`                               | Gateway and Orders                                                                                |
| Admin placement evidence            | `CommercePlacementRecord`                            | Gateway and Orders                                                                                |

Each Admin policy binds to a same-named staff role on the admin audience. Staff
select exactly one tenant per request; there is no unrestricted global query,
and tenant scope is validated before delegation. Reading an order does not
grant decision or evidence permission. No provisioning, deletion, manual
mark-paid, force-ready or readiness-override operation exists in either
contract.

## Wire conventions

- JSON UUID strings for order, subscription, tenant, placement and message
  identifiers. `tenantId` is the immutable Keycloak organization UUID; no second
  tenant registry exists and the browser never supplies tenant authority.
  `actorId` is the authenticated subject string; `keyId` is an opaque rotation
  identifier; offer, price, add-on and provider identifiers are opaque strings.
- Timestamps are RFC 3339 UTC with a `Z` suffix. Enums are strings. Money uses
  positive integer minor units bounded by JavaScript's safe-integer limit
  (9007199254740991) and uppercase ISO 4217 currency. Order, decision and
  license revisions are bounded `uint32` integers starting at 1; a basket
  starts at revision 0 and increments on create, update and clear without
  resetting when the selection is emptied.
- Responses distinguish `null` from absent explicitly. Nullable values are
  present as `null` until populated; consumers must not infer meaning from an
  omitted field.
- v1 is additive only. Consumers ignore unknown response properties and safely
  render unknown string enum values. Requests reject unknown properties.
- `shared` and `dedicated` are the only tiers; `dedicated` means a
  tenant-specific database, not a cluster.

## Basket and checkout semantics

One basket exists per authenticated tenant and subject. An untouched basket is
`{selection: null, revision: 0, total: null}`. `PUT /v1/commerce/basket` takes
the last observed `expectedRevision`; `0` is valid only to create the first
selection. The server canonicalizes the selection (sorted, deduplicated add-on
codes), recomputes `total` from catalog prices, and rejects codes that are not
currently offered with `422 unsupported_option`. Duplicate add-on codes are
`400 invalid_request`. Price and selection snapshots are server-owned; browser
data never determines the price mapping.

`POST /v1/commerce/orders` requires an `Idempotency-Key` UUID and the current
`basketRevision`. The first create and every successful replay of the same
intent return HTTP 201 with the same `orderId`. A replay with a different
`basketRevision` is `409 idempotency_conflict`. A new key against an
already-consumed revision is `409 stale_revision`; an empty basket is
`409 invalid_transition`. The immutable selection and price snapshot, order and
subscription IDs, and the intent receipt are stored in one transaction before
the basket is cleared in that same transaction. An order can exist while
checkout is unavailable after a retryable provider failure; the intent stays
bound to it and `checkoutUrl` is `null`. Replays return the current
`checkoutUrl` while the order is still payable, otherwise `null`. A
`checkoutUrl`, when present, is an `https://` URL on the exact configured
provider checkout origin.

## Final decision and evidence semantics

`POST .../decision` accepts `{decision, reason, expectedRevision}` with a
trimmed nonblank reason of at most 512 characters. With a matching revision,
`pending → approved` or `pending → rejected`; every later decision attempt is
`409 invalid_transition`, because the first decision is final. The actor comes
from the authenticated staff principal, never from the request body.

`POST .../placement-evidence` accepts `{placementId, evidenceReference,
expectedRevision}`. Evidence can be recorded or re-recorded only while the
order is approved and currently paid. `evidenceReference` is a bounded nonsecret
audit string of at most 512 characters. Newest evidence receives a fresh
revision even for the same placement, and a result for an older evidence
revision never overwrites it. Verification is asynchronous and internal; the
operation returns the updated Admin order, never a staff- or browser-supplied
`ready` flag. Stable terminal failure codes are `binding_mismatch`,
`capability_unavailable` and `probe_unavailable`.

Admin order shapes add `payment`, `approval`, `fulfillment` and `audit` to the
customer metadata and omit the `license` document, provider references and
secret identifiers. `audit[].action` is `approve`, `reject` or
`recordPlacementEvidence`.

## Internal gRPC contract

HTTP messages map to Orders RPCs rather than a second state model: `GetCatalog`,
`GetBasket`, `PutBasket`, `CreateOrder`, `ListCustomerOrders`,
`GetCustomerOrder`, `ListAdminOrders`, `GetAdminOrder`, `DecideOrder`,
`RecordPlacementEvidence`, and the internal `CheckCurrentEntitlement`. Licensing
exposes `GetCurrentLicense` and `VerifyLicense`.

- Delegated identity and tenant context travel in established call metadata, not
  as trusted request fields. The handler verifies metadata matches the requested
  binding. A customer middleware path cannot reach staff RPCs.
- `CheckCurrentEntitlement` takes `{tenantId, orderId, subscriptionId,
  desiredRevision}` and answers `{eligible}` from Orders' own durable facts. It
  is callable only by the scoped Licensing workload and creates no synchronous
  call cycle, because Orders never calls Licensing.
- `GetCurrentLicense` takes the same binding and returns the customer license
  envelope, or `NOT_FOUND` when no active grant matches the desired generation.
- `VerifyLicense` is an authorized workload API, not an edge endpoint. It takes
  the document plus expected tenant and subscription and returns effective
  claims, or a stable rejection: `invalid_signature`, `expired`, `revoked`,
  `wrong_binding` or `unknown_key`.
- Protobuf uses field numbers in the listed schema order starting at 1,
  message presence or `optional` for nullable JSON values,
  `google.protobuf.Timestamp` for UTC instants, and `UNSPECIFIED = 0` for every
  enum. Removed fields reserve their number and name.

## Durable events

`commerce.license-desired.v1` (Orders → Licensing) and
`commerce.license-state.v1` (Licensing → Orders) share
`{messageId, occurredAt, tenantId, orderId, subscriptionId, revision}`. Desired
state adds `{enabled, tier, addOnCodes, notAfter}`; the grant outcome adds
`{desiredRevision, state, licenseRevision, keyId, expiresAt}`, where the active
values are `null` when `state` is `inactive`.

`revision` is the desired entitlement generation the message is about; for the
state event it must equal `desiredRevision`. The desired revision is a durable,
monotonically increasing Orders generation for effective-entitlement changes.
Licensing ignores lower or equal generations, processes full desired snapshots
and returns the effective revision. Orders accepts only an outcome whose
`desiredRevision` matches its current desired generation. `messageId` stays
stable across transport retries; consumers deduplicate on it. Broker payloads
contain no license document, credentials or private key. Publishing uses the
transactional outbox and consumption uses a durable inbox; arrival order does
not establish state.

## License document semantics

Licensing issues `Standard.Licensing` signed documents binding the tenant,
order, subscription, effective tier and allowed add-ons, expiry, revision and
key ID. A document never embeds endpoints, hosts, credentials or connection
strings. The active signer is supplied through injected managed secret
configuration; only its public verification keys are distributed. Verification
validates the signature, tenant/subscription binding, revision, current
entitlement state and expiry.

Expiry is clamped to the paid period end and to the readiness proof freshness,
so a missed refresh cannot leave a license valid past its evidence. When
entitlement is disabled or superseded, verification returns `revoked`. Key
rotation preserves public-key semantics: a license remains verifiable while its
`keyId` is trusted and unexpired; after the old public key is removed,
verification returns `unknown_key`. A replayed older revision, a tampered
document, a wrong tenant/subscription binding and an expired document each map
to their stable rejection code. `GetCurrentLicense` fails closed: if it cannot
confirm current Orders entitlement, it returns unavailable rather than accepting
an unverifiable license.

## Errors

Errors use `application/problem+json` with a stable `code` plus `type`, `title`,
`status`, and `detail` only when sanitized. Details never contain secrets,
provider exception text, raw SQL errors or placement connection data.

| HTTP | `code`                   | Meaning                                          |
| ---- | ------------------------ | ------------------------------------------------ |
| 400  | `invalid_request`        | Schema or semantic validation failure            |
| 401  | `unauthenticated`        | Missing or invalid credentials                   |
| 403  | `forbidden`              | Authenticated but not permitted                  |
| 404  | `not_found`              | Absent or another tenant's resource              |
| 409  | `idempotency_conflict`   | Same key, changed intent                         |
| 409  | `stale_revision`         | Optimistic concurrency failure                   |
| 409  | `invalid_transition`     | Illegal domain transition                        |
| 422  | `unsupported_option`     | Offer or add-on not currently offered            |
| 503  | `dependency_unavailable` | Retryable dependency failure; retry with backoff |

gRPC `INVALID_ARGUMENT`, `UNAUTHENTICATED`, `PERMISSION_DENIED`, `NOT_FOUND`,
`ALREADY_EXISTS`/`ABORTED`, `FAILED_PRECONDITION` and `UNAVAILABLE` map to these
HTTP outcomes, carrying the stable code as error metadata. Retryable dependency
failures never advance state. `503` may carry an integer `Retry-After`.

## Compatibility and versioning

Within v1, add optional response properties, endpoints, optional query
parameters and new stable error codes while preserving existing behavior. Do not
add required request properties, remove or rename properties, change
types/nullability or status semantics, repurpose a field, or remove an error
code. Breaking semantics require `.v2` routes/events and new protobuf packages
(`teck.cloud.commerce.orders.v2`, `teck.cloud.commerce.licensing.v2`), with a
migration and dual-version plan. Never silently rewrite a published v1 file.
Expanding required response properties is also breaking for clients that
validate closed response schemas.

## Verification and fixtures

`bun run test:tooling` runs `tools/commerce-contract.test.mjs`, which executes
`tools/verify-commerce-contract.mjs` and rejects mutations of the checked-in
contract. It validates both OpenAPI documents, every JSON example, the two event
schemas, the protobuf structure and the license verification matrix, then compiles
both protos:

```bash
dotnet build docs/contracts/commerce/v1/Commerce.Contract.Protos.csproj -p:RestoreLockedMode=true
```

`Commerce.Contract.Protos.csproj` references the central `Grpc.Tools` and
`Google.Protobuf` versions, compiles `orders.proto` and `licensing.proto` with
`GrpcServices="None"` (messages only, no server or client generation), and is
not registered as a runtime service or as an Nx project.

Fixture prices `1000` and `3000` USD monthly, plus `500` for
`replicaReadAccess`, are isolated test values, not production prices. The
license `document` strings in the fixtures are placeholders, not verifiable
signatures.

## Provider and consumer approval record

Provider sign-off (Orders, Licensing, customer gateway, admin gateway) and
consumer sign-off (Storefront, Admin) are recorded in the PR conversation,
Teck.Cloud #26 and Multica TECCL-11 against the exact PR head SHA. Editing this
file to record a sign-off would move the revision it approves, so the
conversation is authoritative. Until the named owners record approval against
the same revision, these files are a review draft, not an approved
implementation contract.
