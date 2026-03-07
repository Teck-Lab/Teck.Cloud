---
description: 'Converted from docs/ai/rules/architecture/inter-service-communication.md'
applyTo: 'src/**/*.cs,tests/**/*.cs'
---
# Inter-Service Communication

## Intent

Use the right communication mode per interaction type.

## Rules

- Gateway (BFF) to downstream services: HTTP.
- Direct synchronous service-to-service calls: gRPC when appropriate.
- Asynchronous cross-service propagation: integration events via broker.
- Resolve service locations via service discovery/config, not hardcoded addresses.
- Edge gateways validate and route requests, but do not perform token exchange on behalf of downstream services.
- Enforce strict per-hop token exchange for internal synchronous calls (HTTP and gRPC).
- Configure target audience per hop and do not reuse broad multi-audience tokens across multiple downstreams.

## Reliability and Security

- Propagate auth context safely and validate per service.
- Propagate tenant/correlation context across HTTP headers and gRPC metadata consistently.
- Apply timeouts/retries/circuit breaking through configured clients.
- Handle and map transport errors explicitly.
- Emit logs/traces for all cross-service calls.
