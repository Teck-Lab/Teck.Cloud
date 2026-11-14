# Events and service communication

These are the agreed Teck.Cloud conventions. Core contracts and shared Wolverine
EF/local/RabbitMQ wiring are implemented; service-level architecture and recovery
tests remain required. See [shared runtime](shared-runtime.md). Use Wolverine's EF
domain-event integration, not a second bus or the old manual publishing pipeline.

## Transport boundaries

| Interaction                                     | Required mechanism                                    |
| ----------------------------------------------- | ----------------------------------------------------- |
| External client entering the application        | HTTP API with FastEndpoints; Scalar documents OpenAPI |
| Synchronous call between our services           | gRPC with an explicit contract                        |
| Asynchronous communication between our services | Wolverine over RabbitMQ                               |
| Command/query inside one service                | Wolverine in-process dispatch                         |
| Domain event inside one service                 | Wolverine local handling only                         |

HTTP/REST is an external-facing application interface, not a service-to-service
business protocol. An edge gateway receiving HTTP uses gRPC when invoking another
business service; do not proxy a REST call to another service as an internal shortcut.
Inside one service, use local dispatch rather than a network call to itself.

Here HTTP means the application-level HTTP/REST API: gRPC itself normally uses
HTTP/2. Health probes and telemetry are operational endpoints, not alternative
business APIs. Do not infer permission for internal REST calls from their existence.
Third-party integrations require their own explicit adapter/contract decision;
this convention does not select an outbound vendor protocol.

## Event contracts and ownership

Use two lightweight, framework-independent marker interfaces:

- IDomainEvent identifies a fact internal to its owning service. Concrete types live
  in that service's Domain/DomainEvents. They never become RabbitMQ payloads,
  externally shared DTOs or another service's dependencies.
- IIntegrationEvent identifies a versioned, producer-owned fact intended for other
  services. Concrete types live in a focused shared contracts library; consumers
  reference contracts, never the producer's Domain/Application/Host implementation.

These are our architecture markers, not interfaces required by Wolverine. Put the
markers in SharedKernel.Core without Wolverine, EF Core or ASP.NET dependencies. A concrete type must not implement both markers. Do not
make one interface inherit the other, expose entities/navigation graphs in either
external contract, or assume an Event suffix alone enforces routing.

Event names describe facts, such as BasketCheckedOut. A message directing one
service to do work is a command and must not masquerade as a fact. Async cross-service
commands also use Wolverine/RabbitMQ with an explicit destination and independent
contract; synchronous request/response uses gRPC, not broker RPC.

For integration contracts, define event identity, occurrence time, contract version,
correlation/causation and tenant context where applicable. Specify whether each
value is payload data or Wolverine envelope metadata; do not invent conflicting
copies. Retain stable event identity across transport retries. Prefer typed fields
and known serialization types over the old unbounded Dictionary<string, object>
metadata. Document backwards-compatible evolution and use a new version for
breaking changes. A generated serializer is not by itself a compatibility policy.

## Domain events through Wolverine and EF Core

Entities collect IDomainEvent instances while applying domain behavior; entities do
not inject IMessageBus or call RabbitMQ. Application owns the local handlers and
the translation into integration contracts.

Use Wolverine's documented PublishDomainEventsFromEntityFrameworkCore<TEntity>(...)
registration with UseEntityFrameworkCoreTransactions and the selected EF/outbox
integration. Its collector runs as part of the transaction machinery; a bare EF
SaveChangesAsync call is not a substitute. Our application IUnitOfWork delegates
to the same write context inside Wolverine Eager transactions, and to the official
scoped IDbContextOutbox outside handlers. For explicit message publication outside
handlers, use IDbContextOutbox<T> directly. See [shared runtime](shared-runtime.md).
See [Wolverine's domain-event guide](https://wolverinefx.io/guide/durability/efcore/domain-events).

Choose one collection/publication path per event. Verify repeated saves do not
republish drained events, rollback/retry does not lose them, and all supported
write-context/tenant paths participate. Do not also manually publish the same
integration event from the command handler after enabling its domain-event translator.

Local event handlers belong beside the capability, for example
Application/Baskets/EventHandlers/DomainEvents. Incoming integration handlers live
under that capability's EventHandlers/IntegrationEvents. Both use Wolverine's
ordinary handler conventions and keep transport setup in Host composition.

A domain event can legitimately lead to an integration event. The local handler
constructs a separate IIntegrationEvent contract containing only the approved
external fact. It never forwards or casts the IDomainEvent object onto RabbitMQ.
Not every domain event needs a public integration equivalent.

## Routing must enforce the boundary

Configure explicit local routes for domain events and explicit RabbitMQ routes for
approved integration contracts. Exclude domain-event types from all external
conventions, including forwarded types. Do not copy PublishAllMessages-to-RabbitMQ
examples or apply an unrestricted namespace/assembly convention to mixed message types.

Wolverine publishes according to its configured routes; neither IDomainEvent nor
its EF collector means local-only automatically. Test the resolved routes, including
types with local handlers, and assert that domain types have no external destinations
and integration types have their intended broker destinations. See
[Wolverine routing](https://wolverinefx.net/guide/messaging/subscriptions).

A service requiring cross-service messages must not silently switch to local-only
mode when RabbitMQ configuration is absent. Isolated unit tests may use explicit
local test wiring; integration/E2E environments must exercise the broker.

## Transactions, durability and delivery

Use Wolverine's durable inbox/outbox and EF integration. When producing integration
events with a database mutation, persistence and outgoing message recording must
share the intended transaction. Do not introduce a custom outbox table/dispatcher
or equate SaveChangesAsync followed by PublishAsync with an atomic commit.
See [Wolverine's EF inbox/outbox guide](https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox).

For the domain-event-to-integration-event flow, local durable queues make the local
handoff recoverable. The initial database transaction records the local event;
the subsequent local translator records the integration message through the outbox
in its own processing transaction. This is two recoverable steps, not one transaction
covering the original mutation, translator and remote service. The local event stays
owned by the same service even if its local queue is persisted for recovery.

Keep invariants required before accepting a command in its immediate domain/use-case
execution. Do not defer them to a post-commit event handler and then claim they can
roll back the original command. Document eventual consistency for deferred reactions.

Use one transaction owner. In a Wolverine-managed handler, the transaction middleware
owns persistence/outbox completion; retained repository/unit-of-work abstractions
must participate in that scope rather than independently committing earlier. Do not
combine an independent unit-of-work commit with a second automatic commit. ErrorOr
failure results do not inherently prove rollback: test that rejected commands do
not persist partial mutations or publish success events.

Expect redelivery. Use durable receiving behavior, stable message/business keys and
idempotent side effects. Configure bounded retries and dead-letter/error handling;
do not acknowledge a transiently failed message as successfully handled. An ErrorOr
returned by a consumer is not automatically a broker retry decision. Do not promise
exactly-once business effects or global ordering; use explicit version/sequence
checks when handling stale or out-of-order facts.

## Basket and Order example

The names below illustrate the flow; they do not create a new checkout feature.

```text
External checkout request (HTTP)
  -> Basket application command via Wolverine
  -> Basket entity raises BasketCheckedOut : IDomainEvent
  -> EF/Wolverine commit records state and a durable LOCAL event
  -> Basket's local domain-event handler
  -> creates BasketCheckedOutV1 : IIntegrationEvent
  -> Wolverine durable outbox -> RabbitMQ
  -> Order's integration-event handler
  -> Order applies its own business rules and commits its state
```

Order never receives Basket's domain type and never reads Basket's database. If
Order needs an immediate answer from another service, it calls that service's gRPC
contract. That call is not part of a distributed database transaction; multi-service
workflows must define retries, outcomes and compensation explicitly.

Tenant scope must survive local translation, outbox/inbox persistence, retries and
background handling. Validate it before resolving tenant-sensitive persistence;
follow [multi-tenancy](multi-tenancy.md).

## Synchronous gRPC contracts

Use explicit protobuf contracts and generated clients/server types for synchronous
service calls. Keep these contracts separate from domain entities and ErrorOr<T>.
Map ErrorOr outcomes at the gRPC boundary into the documented response/status/error
contract, just as the external HTTP boundary maps to HTTP/Problem Details.

Propagate bounded deadlines, cancellation and trace context. Authenticate callers,
validate tenant/authorization context at the receiving service, and retry only when
the operation and its idempotency contract allow it. Reuse managed clients/channels;
do not create an unbounded chain of per-request connections or retry loops.
See [ASP.NET Core gRPC deadlines](https://learn.microsoft.com/en-us/aspnet/core/grpc/deadlines-cancellation?view=aspnetcore-10.0).

FastEndpoints remains the external HTTP adapter. gRPC entry points can dispatch the
same local application command/query through Wolverine; HTTP and protobuf binding
contracts need not be identical. No custom mediator or endpoint inheritance framework
is needed for either adapter. Scalar/OpenAPI describes HTTP, not the gRPC contract.

## Required proof with the first service

- Unit tests cover domain event contents, when events are raised, domain-to-integration
  translation and expected command failures.
- Architecture tests keep domain types private to their service, disallow dual event
  markers and producer-implementation references, and check transport ownership.
- Routing tests assert no domain-event external route and the exact integration routes.
- Testcontainers tests use the real database and RabbitMQ to prove rollback, atomic
  outbox recording, broker outage/recovery, local translator recovery, duplicate and
  stale delivery handling, tenant isolation and the consumer's committed effects.
- gRPC integration tests exercise real client/server contracts, status mapping,
  deadlines and auth; exercise HTTP/2/TLS over a real server when testing transport.
- Aspire E2E enters through external HTTP and exercises actual gRPC and RabbitMQ hops.
  A direct in-process call or local queue cannot stand in for that transport evidence.

These are requirements for future implementation, not claims that analyzer packages
alone enforce routing or that a business service has already passed these tests.

## What the old monorepo actually does

Read-only inspection found IDomainEvent and IIntegrationEvent in SharedKernel.Core,
with IEvent supplying Id, CreatedOn and object-valued metadata. BaseEntity<TId>
collects IDomainEvent and exposes AddDomainEvent/ClearDomainEvents. Service domain
types inherit DomainEvent. SharedKernel.Events contains external contracts; inspected
BasketCheckedOutV2IntegrationEvent is a MemoryPack POCO and does not actually implement
IIntegrationEvent, so the old marker policy is not uniformly applied.

Pricing captures PriceChanged domain events and manually maps them through
PricingEventPublisher after SaveChangesAsync. Catalog's UpdateSellPrice does similar
manual translation. BasketPricedHandler directly publishes BasketCheckedOutV2IntegrationEvent
after saving; it does not rely on an automatic domain-event handler for that publication.

The old src/services/AGENTS.md explicitly says the EF domain-event bridge is not
wired. WolverinePersistenceConfigurator enables EF transactions, durable local
queues, PostgreSQL message storage and conventional RabbitMQ routing, but contains
no PublishDomainEventsFromEntityFrameworkCore registration. It also supports a
configuration-driven local-only fallback. Do not copy those differences into the
new design or claim atomic publication from source ordering alone.

## Wolverine node coordination

UseRequiredRabbitMq enables EnableWolverineControlQueues so Wolverine instances
coordinate through RabbitMQ control queues instead of database control-queue
polling. This is internal runtime coordination, not an application integration-event
route or a replacement for PostgreSQL inbox/outbox persistence. Broker credentials
must allow Wolverine to declare and use its node-specific system queues. Keep
service business-message routes explicit; domain events remain local.
