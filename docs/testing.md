# Test layers

See [test tooling](test-tooling.md) for Shouldly, NSubstitute, fixture generators,
FastEndpoints fixtures and MTP-compatible coverage conventions.

| Layer               | Location                                | Nx target        | Environment                                          |
| ------------------- | --------------------------------------- | ---------------- | ---------------------------------------------------- |
| Unit                | tests/unit for .NET; colocated TS tests | test             | No infrastructure                                    |
| Architecture        | tests/architecture                      | test             | Dependency/architecture rules                        |
| Service integration | tests/integration                       | test:integration | Testcontainers; WebApplicationFactory for HTTP       |
| Application E2E     | tests/e2e/web                           | test:e2e         | Aspire-managed stack; browser/API assertions         |
| Kubernetes E2E      | tests/e2e/kubernetes                    | test:kubernetes  | Aspire/KIND with deployed candidate manifests/images |

Core and Infrastructure unit tests plus Persistence/Infrastructure integration
suites are installed. They use real PostgreSQL, Redis and RabbitMQ Testcontainers
and a test-only FastEndpoints WebApplicationFactory host under tests/fixtures.
The verify:static target checks generated Wolverine handlers without the runtime
compiler. Shared-layer architecture checks are installed. Business application E2E and
service-specific architecture rules remain to be added with consumers.
Mobile testing is undecided; its Nx project can add a runner and targets later.

Integration tests use real Testcontainers dependencies, never Aspire startup. Verify
committed effects through fresh connections. Tests without external dependencies
need no container. Application E2E owns one Aspire lifecycle and exercises actual
frontend/gateway/services. Next.js browser journeys use TypeScript Playwright via
Nx/Bun; .NET may own API-only journeys. Isolate data and bound readiness/cleanup.

Kubernetes tests run candidate images using actual deployment/ manifests and test
through the deployed gateway, including operator behavior where relevant. An
Aspire-generated chart cannot validate different repository manifests. Check the
sandbox kernel/runtime supports the requested cluster features.

Implement small RED/GREEN slices; run assembled journeys before acceptance. Capture
comparable coverage and failure evidence. An infrastructure failure or zero tests
is not behavioral RED. Unit/architecture targets are cacheable with complete inputs;
live integration/E2E/Kubernetes tests default to no cache and bounded concurrency.
Place assertions at the smallest real boundary that can expose the defect.

## Event and service-transport coverage

Follow [messaging](messaging.md#required-proof-with-the-first-service). Unit tests
cover internal domain events and translation into integration contracts. Architecture
and route tests prevent domain events from leaving their owning service. Integration
tests use database/RabbitMQ Testcontainers and actual gRPC boundaries; cover outbox
rollback/recovery, duplicates, local translator recovery, tenant isolation, deadlines
and committed consumer effects. WebApplicationFactory is for the external HTTP
adapter; use a real HTTP/2 server where gRPC transport/TLS is under test. Aspire E2E
starts the actual communicating services rather than replacing broker/gRPC hops with
local dispatch. Shared tests cover durable delivery/retry, save-then-throw rollback, physical
envelope deduplication and pending outbox recovery after broker outage/application
restart. Abrupt process-kill windows, duplicate business intents and live gRPC/TLS
tests remain required with real services; see [shared runtime](shared-runtime.md).

## Multi-tenant coverage

Follow [multi-tenancy](multi-tenancy.md#required-proof). Exercise two tenants with
overlapping identifiers across both database contexts, gRPC and durable messaging.
Include forged/missing context, detached writes, concurrent scopes, replay and
cache/idempotency isolation. Aspire E2E proves isolation through actual ingress.

The Redis Sentinel E2E suite now boots the real Aspire AppHost and verifies primary-loss recovery. See [local Sentinel](redis-sentinel.md) for its advertised-host prerequisite; HTTP gateway output-cache tests remain focused Testcontainers integration tests.
