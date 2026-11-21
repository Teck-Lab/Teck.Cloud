# Dedicated PostgreSQL gateway contract v1

The OpenAPI file and SignalR JSON Schema are the normative, versioned JSON wire contracts for the customer gateway and Admin → Orders surface. Examples are executable fixtures checked by `bun run verify:dedicated-postgresql-contract`. Do not add persistence, Host implementation, cluster operations, activation automation or deletion endpoints here.

## Identity, tenant scope and visibility

Customer routes derive tenant identity from the authenticated principal. They accept no tenant ID, tenant slug, connection string, host, or secret. Customer order and operation reads are constrained to that tenant. A resource outside the principal's visible scope returns the same `404 RESOURCE_NOT_VISIBLE` shape as an absent resource so existence is not disclosed. `401 UNAUTHENTICATED` means no valid principal. `403 RESOURCE_NOT_VISIBLE` may be used where existence disclosure is not a risk and a distinct authorization failure is useful; clients must not infer tenant existence from either response.

Admin is a separate authorization plane. Each operation carries its own `x-required-permission`; reading order detail does not grant diagnostics or action permissions. Tenant identifiers and diagnostics are admin-only. Customer-safe DTOs never include internal provider responses, database endpoints, credentials, connection strings, secret references, control-plane identifiers, or raw exception text. Diagnostics use stable codes and redacted display-safe messages.

Administrative retry, restore, hold and refund are distinct audited requests. They require idempotency keys, an expected current revision and a non-empty reason. The server validates both the role permission and state transition. Refund delegates to Billing and never deletes or mutates database infrastructure. Restore is constrained by the approved recovery policy. There is deliberately no delete or hard-purge API.

## JSON and progress semantics

- IDs are opaque, resource-prefixed strings. Clients must not parse suffixes or treat them as sequential values.
- Revisions are unsigned decimal strings, compared as arbitrary precision integers. JSON numbers are unsafe above 2^53−1 in common JavaScript clients; never coerce a revision to a `number` or compare strings lexicographically.
- Timestamps are RFC 3339 UTC (`Z`). Clients serialize UTC and reject timestamps without the UTC marker in these contracts.
- Counts are nonnegative integers. An operation freezes `requiredStepCount` at start. Its denominator never changes; omitted/inapplicable work is not retroactively added or removed. `succeededStepCount` and `failedStepCount` are separate; a failure does not inflate success. Phase is independent of the denominator and distinguishes copy, validation, cutover, provisioning, restore and refund.
- State and phase values are extensible strings with published known values (`x-known-enum-values`). Clients must safely display an unknown value, preserve unknown payload fields, and avoid inventing transitions. Do not reject a newer enum value solely because it is unknown.
- Cursor paging is stable newest-first with opaque cursors and a deterministic `orderId` tie-breaker. Clients continue using the server-returned cursor; they do not manufacture offsets.

## Realtime and recovery

Connect using the authenticated principal to `/hubs/dedicated-database-operations`. `realtime-subscription.schema.json` specifies the client-to-server JSON Hub Protocol calls `SubscribeToOrder` and `UnsubscribeFromOrder`; `signalr-events.schema.json` specifies the server-to-client `OperationChanged` hint and SignalR Completion success/failure envelopes. Each subscription call has one opaque order-ID argument, for example `{"type":1,"invocationId":"1","target":"SubscribeToOrder","arguments":[{"orderId":"ord_example"}]}`. Unsubscribe uses the same shape with target `UnsubscribeFromOrder`.
The matching SignalR Completion has `type: 3` and the invocation ID. A successful void completion contains neither `result` nor `error`; a failed completion contains `error` and no `result`. On `RESOURCE_NOT_VISIBLE`, stop watching the order, do not retry or fetch its snapshot, and show the same user-facing not-found behavior as for an absent order. On `SERVICE_UNAVAILABLE`, leave the order unsubscribed and do not fetch its snapshot; retry only `SubscribeToOrder` with bounded exponential backoff and jitter capped at 30 seconds. Unknown error codes are non-retryable: stop watching and do not fetch a snapshot. Only a matching successful Completion permits the authenticated snapshot fetch. SignalR error values MUST be stable codes only; never expose exception text or secrets.

Clients may name an order to watch, but cannot choose a tenant, operation ID, group name or permission. The server derives tenant scope from the authenticated principal and verifies order visibility before joining the connection to that order's group; Admin membership is independently permission-checked. A not-visible order does not add group membership. Unsubscribe removes only the current connection's membership for that order.

Clients subscribe before the initial authorized HTTP snapshot fetch. After each `OperationChanged` hint, refetch the authoritative snapshot; apply only snapshots whose decimal-string revision compares greater by arbitrary-precision integer arithmetic. Ignore duplicate/stale hints; overlapping hints may coalesce when they trigger a fetch. On reconnect, resubscribe to every still-watched order and await each matching successful SignalR completion (`type:3`, same `invocationId`, no `result` or `error`) before refetching the snapshot. If disconnected, reconnect with bounded backoff and poll the snapshot endpoint as a fallback. SignalR carries no snapshot, credentials or internal diagnostics and is never the source of truth.

## Manual exception visibility and activation

A manual exception may link to the reviewed exception PR and evidence in the Admin projection. It records status and whether separately authorized activation is required. The client exposes the evidence/status and activation gate; it cannot activate via this contract. Automated activation requires a separate approved policy and preflight. Manual exception does not change the order's tenant scope, authorize production deployment, or authorize hard purge. Recovery-window, backup/WAL expiry and automated purge authority remain launch decisions; no value is assumed here.

## Errors

Errors use `application/problem+json` with a stable `code`: `VALIDATION_FAILED` (400), `UNAUTHENTICATED` (401), `RESOURCE_NOT_VISIBLE` (403/404), `IDEMPOTENCY_KEY_REUSED` (409), `STALE_REVISION_OR_INVALID_TRANSITION` (409), and `SERVICE_UNAVAILABLE` (503). Clients branch on `code` and HTTP status, not `title` or `detail`. Error details must remain display safe and contain no secrets or provider exception messages. Transient 503 may include integer `Retry-After`; retry only idempotent requests with bounded backoff.

## Compatibility and versioning

`/v1` is additive within its major version. Compatible changes: add optional response properties, add endpoints, add optional query parameters, and add new stable error codes while preserving existing behavior. Consumers must ignore unknown response properties and safely render unknown string enum values. Do not add required request properties, remove or rename properties, change types/nullability, change status semantics, repurpose a field, or remove error codes in v1. Such changes require a separately versioned major API route and SignalR contract `$id`, plus migration and dual-version plan; never silently rewrite the v1 files. Expanding required response properties is also breaking for clients that validate closed response schemas. Keep response schemas open for additive evolution and requests closed where clients send only intentional values.

## Provider and consumer approval record

Before implementation begins, record explicit provider sign-off (Gateway, Orders and Control Plane) and consumer sign-off (storefront and Admin) against the exact PR revision in both Teck.Cloud #13 and Multica TECCL-6. Plan approval is not provider/consumer sign-off. Until the named owners record approval, these files are a review draft, not an approved implementation contract.
