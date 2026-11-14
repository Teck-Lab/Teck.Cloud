# Observability

Every service Host calls `builder.AddFoundationHost()` once. This installs
OpenTelemetry traces/metrics and Serilog console/OTLP logging without opening a
database or broker connection. A worker without HTTP can call
`AddFoundationTelemetry()` directly. Do not register both on the same host.

## Instrumentation

| Component                    | Registration and coverage                                                                                                                                                                                           |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ASP.NET Core / FastEndpoints | Official ASP.NET Core instrumentation: incoming HTTP traces and request metrics; W3C trace context. `UseFoundationApi` adds Serilog request completion logs.                                                        |
| HTTP and gRPC clients        | Official HttpClient and GrpcNetClient instrumentation; gRPC suppresses the redundant downstream HTTP span.                                                                                                          |
| Wolverine                    | Native `Wolverine` ActivitySource and `Wolverine*` meters: handler execution, delivery, failures and queue diagnostics emitted by Wolverine.                                                                        |
| RabbitMQ                     | Native `RabbitMQ.Client` source/meter in addition to Wolverine transport spans.                                                                                                                                     |
| PostgreSQL                   | Published `Npgsql.OpenTelemetry` extensions for database traces and connection-pool metrics, including commands outside EF.                                                                                         |
| EF Core                      | Official EF Core instrumentation and `Microsoft.EntityFrameworkCore` meter. Npgsql EF command spans are filtered because Npgsql already traces the same command. Other providers need their own runtime validation. |
| FusionCache                  | Published `ZiggyCreatures.FusionCache.OpenTelemetry` tracing and metrics, including memory, distributed cache and backplane levels.                                                                                 |
| Redis                        | Official StackExchangeRedis instrumentation attached to the existing lazy shared multiplexer, with a one-second profiling flush. It does not create a second connection.                                            |
| Runtime / networking         | .NET 10 built-in runtime, HTTP, DNS, TLS and socket meters plus Kestrel and gRPC meters. Meters only emit when the corresponding component is in use.                                                               |
| Application                  | ActivitySources and meters named after the host application or `Teck.Cloud`. Services can register additional explicit names.                                                                                       |

Use published integrations rather than reflection-based tracing wrappers. The
EF Core, gRPC-client and Redis instrumentation packages currently have beta
versions; their exact pins are centralized in Directory.Packages.props. Tests
exercise the pinned versions. The .NET 10 runtime meter needs no additional
runtime-instrumentation package.

Wolverine owns context propagation through its envelopes; application code must
use its supported messaging APIs. HTTP/gRPC uses normal W3C context propagation.
Start custom activities only around meaningful operations; retain the current
parent instead of creating a disconnected trace. Detached jobs require explicit
context propagation at their scheduling boundary.

## Resource identity and process metrics

The shared ResourceBuilder includes the published Container, Process,
ProcessRuntime and OperatingSystem detectors. Their detected attributes are built
once and applied to traces, metrics and Serilog OTLP logs together. Container IDs
are only available in supported container environments; a Windows process or a
Docker Sandbox VM is not automatically an application container. Kubernetes pod,
namespace and node metadata still belongs in platform resource enrichment.

Process instrumentation adds virtual memory, OS thread count, uptime and Windows
handle count where supported. The duplicate `process.cpu.time` and
`process.memory.usage` streams are dropped: .NET 10's existing `System.Runtime`
meter supplies `dotnet.process.cpu.time` and `dotnet.process.memory.working_set`.
No additional EventCounters or Runtime instrumentation package is installed.
Resource attributes describe the process; metrics measure its behavior.

Pins: Container/ProcessRuntime/OperatingSystem 1.21.0-beta.1;
Process resource detector and Process instrumentation 1.21.0-rc.1. These remain
prerelease dependencies and should be upgraded together with telemetry tests.
Do not promote process/container IDs into Loki indexed labels.

## Optional Pyroscope profiling

`Pyroscope.OpenTelemetry` 0.5.1 is installed. Enable its official span processor
per host with `Telemetry:Pyroscope:Enabled=true` (environment variable
`Telemetry__Pyroscope__Enabled=true`). It defaults to false and preserves the
existing OTEL sampler. The SDK owns processor lifetime; it attaches the official
`pyroscope.profile.id` correlation tag to eligible spans before export.

This flag enables correlation, not the native profiler. A profile ID on a span
alone does not prove profiling is operational. A deployment also needs:

1. Grafana's native .NET profiler matching the application OS, architecture and
   libc, installed in the application image. No application images exist here yet.
2. Profiler variables set **before the process starts**, including
   `CORECLR_ENABLE_PROFILING=1`,
   `CORECLR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}` and
   `CORECLR_PROFILER_PATH` pointing to the installed library. Linux also needs the
   documented API wrapper (`LD_PRELOAD`) and library search path.
3. `PYROSCOPE_PROFILING_ENABLED=1`, `PYROSCOPE_SERVER_ADDRESS` and a stable
   `PYROSCOPE_APPLICATION_NAME` aligned with `OTEL_SERVICE_NAME`. Provision backend
   authentication as a secret. Profile traffic is separate from the OTLP endpoint.
4. A Pyroscope backend and Grafana Tempo-to-Pyroscope data-source mapping.

Use the official [native profiler setup](https://grafana.com/docs/pyroscope/latest/configure-client/language-sdks/dotnet/)
and [span-profile integration](https://grafana.com/docs/pyroscope/latest/configure-client/trace-span-profiles/dotnet-span-profiles/).
Our OTEL SDK instrumentation can coexist with this profiler; do not also inject a
competing CLR-based OTEL automatic profiler. This path does not establish NativeAOT
support. Profile capture, upload, overhead and Grafana navigation need a deployed
application verification; tests here prove the opt-in span-correlation behavior.

## Collector configuration

Export activates when an OTLP endpoint is configured, including the environment
provided by an Aspire AppHost. Without one, instrumentation and console logs remain
available but nothing is sent to a collector. No collector is deployed by this
library.

| Setting                                                          | Behavior                                                              |
| ---------------------------------------------------------------- | --------------------------------------------------------------------- |
| `OTEL_SERVICE_NAME`                                              | Stable service identity; defaults to the host application name.       |
| `Telemetry:ServiceVersion`                                       | Release version; defaults to entry-assembly version.                  |
| `Telemetry:ServiceInstanceId`                                    | Optional instance identifier; defaults to a per-host UUID.            |
| `OTEL_EXPORTER_OTLP_ENDPOINT`                                    | Collector base URL, e.g. `http://localhost:4317` for gRPC.            |
| `OTEL_EXPORTER_OTLP_PROTOCOL`                                    | `grpc` (default) or `http/protobuf`. HTTP commonly uses port 4318.    |
| `OTEL_EXPORTER_OTLP_HEADERS`                                     | Secret-supplied comma-separated `name=value` authentication headers.  |
| `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_ENDPOINT`              | Optional endpoint for a single signal; overrides the common endpoint. |
| `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_PROTOCOL` / `_HEADERS` | Per-signal overrides.                                                 |

For HTTP/protobuf, the common base endpoint gets `/v1/traces`, `/v1/metrics`
or `/v1/logs` appended. A signal-specific endpoint is used exactly as supplied,
including its path. gRPC endpoints are not given HTTP signal suffixes. Do not put
credentials in endpoint URLs. Supply collector headers through secrets.

All signals receive the same service name, version, instance and deployment
environment resource. Additional process-level `OTEL_RESOURCE_ATTRIBUTES` are
read by the SDK's default resource detector; foundation service identity and
deployment environment take precedence.

`AddFoundationHost(exportTelemetry: false)` disables all OTLP exports for isolated
tests while preserving instrumentation. `true` requires at least one configured
signal endpoint; malformed endpoints/protocols fail startup. The default `null`
automatically selects export from configuration.

## Sampling and sensitive data

The default `OTEL_TRACES_SAMPLER=parentbased_always_on` records all new root
traces and honors an upstream parent's sampling decision. Production can select
`parentbased_traceidratio` with `OTEL_TRACES_SAMPLER_ARG=0.1`, for example.
The standard always-on/off and trace-ID-ratio variants are supported. Sampling
controls traces, not logs; unsampled requests may still have correlated log IDs.

Validated tenant ingress and Wolverine tenant handlers add `tenant.id` to the
current activity. Serilog derives `TenantId` from that activity. Tenant IDs are
not metric dimensions. Missing scope is not replaced with a fabricated tenant.
Do not put tenant IDs, request IDs or raw URLs into custom metric labels.

EF sensitive-data logging and verbose Redis statements are not enabled. SQL
instrumentation can still include command text: parameterize SQL, and never put
credentials in SQL literals, cache keys, message names, log properties or baggage.
No automatic request/response-body, token or message-payload capture is installed.

## Serilog

Microsoft ILogger events flow through one Serilog provider. Enrichment includes
LogContext, machine name, service name/version/instance, environment, process ID,
process name, thread ID, thread name when assigned, and validated TenantId.
TraceId and SpanId are captured on the event before async batching; they appear
in both console output and OTLP log records. Thread IDs are diagnostics, not
request identifiers: an async request can move between threads.

The additional packages are Serilog.Enrichers.Process 3.0.0,
Serilog.Enrichers.Thread 4.0.0, Serilog.Enrichers.ClientInfo 3.0.0,
Serilog.Exceptions / EntityFrameworkCore / Grpc 8.4.0 and
Serilog.Enrichers.Sensitive 2.1.0. Environment 3.0.1 remains enabled.
Serilog.Enrichers.Span and Serilog.Sinks.Trace are not installed.

Exception enrichment uses explicit built-in, EF and gRPC destructurers with
reflection fallback disabled. Unknown exception-specific properties are not
visited. EF entity entries/values, arbitrary Exception.Data and gRPC trailers are
excluded; status codes, error types, inner exception details and stack traces
remain available. This deliberately avoids exporting tracked entity contents or
transport credentials as exception diagnostics.

A final output boundary converts the raw exception to maskable properties,
including the standard `exception.type`, `exception.message` and
`exception.stacktrace` attributes. It retains trace/span IDs and passes the
result through Sensitive before either sink. Console uses the masked
`ExceptionText` property. This is necessary because the package does not sanitize
the raw Exception object that a sink would otherwise render separately.

Masking includes the package's email, IBAN and credit-card patterns plus explicit
password, token, authorization, cookie, API-key, client-secret, private-key and
connection-string property names, including common underscore/header variants.
Nested structures and dictionaries are included. This does not identify every
possible secret in free text, sanitize independently exported OTEL spans, or make
logging entire request bodies safe. Do not log secrets in the first place.

Client information is opt-in per HTTP ingress host:

```json
{
  "Telemetry": {
    "ClientInfo": {
      "Enabled": true,
      "UserAgent": true
    }
  }
}
```

`Enabled` records the resolved connection IP; `UserAgent` separately allows only
the User-Agent header. Both default to false. Authorization, cookies, arbitrary
headers and user claims are not collected. No second correlation ID is generated.
Run ASP.NET forwarded-header middleware with explicitly trusted proxies before
request handling if the client IP should reflect a gateway; the enricher does
not trust X-Forwarded-For directly. Do not enable these options for every worker.
Keep client/process/thread IDs as Loki metadata, not indexed stream labels.

The Sensitive package internally uses reflection to modify message templates;
disabling exception reflection does not make the complete logging stack
NativeAOT-ready. That dependency needs dedicated trimming/AOT validation before
publishing an AOT service.

Console output uses Serilog.Sinks.Async with a 10,000-event buffer and nonblocking
overflow. If the terminal cannot keep up, console events can be dropped rather
than stalling request handling. Monitor `teck.logging.console.dropped` and
`teck.logging.console.queued` on the `Teck.Cloud` meter.

Serilog.Sinks.OpenTelemetry exports logs directly and already batches internally;
it is not wrapped in another async sink. Exporter activity is suppressed to avoid
logging/export loops. Do not also add an OpenTelemetry logger provider or duplicate
console/OTLP sinks in Serilog configuration: configured WriteTo/AuditTo sections
are rejected because they could bypass masking. DI enrichers/destructuring
policies are supported; DI sinks are not automatically attached. Host disposal
flushes the owned sinks.
Neither queue is a durable audit store or a guarantee of delivery during a crash
or prolonged collector outage.

The default level is Information, with Microsoft.AspNetCore and EF database
command logs at Warning. Serilog request completion records still include status
and duration. Override levels through the ordinary `Serilog:MinimumLevel`
configuration, for example:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "ZiggyCreatures.Caching.Fusion": "Warning"
      }
    }
  }
}
```

Use ILogger message templates with structured arguments. Debugging should follow
one trace through HTTP, handlers, SQL, cache and message consumers, with associated
logs, rather than permanently enabling noisy database/cache debug logs.

## Alloy redaction as a second layer

Application masking remains enabled for console and OTLP output. Alloy cannot
retroactively remove data already printed locally or sent over the network.
The reusable [Alloy policy](examples/observability/redaction.alloy) adds a second
layer for telemetry from all producers before backend export. See its
[configuration and verification instructions](examples/observability/README.md).

It uses the generally available `otelcol.processor.transform`, not Alloy's
experimental dedicated redaction processor. It removes credential-named fields
and masks email/bearer patterns in supported log, span and resource fields. It
preserves native trace/span IDs and `pyroscope.profile.id`. Nested maps are
flattened into dotted keys before filtering; callers must accept that downstream
attribute shape. This is a bounded policy, not arbitrary secret discovery or a
replacement for the application's email/card/IBAN masking. Arrays, serialized
JSON strings and arbitrary secrets in SQL/free text need deliberate additional
rules and fixtures when introduced.

Place it before batching, persistent export queues and backend fan-out. Use
`error_mode = "propagate"` so a transform failure does not export unsanitized data;
monitor refused/dropped telemetry. Keep receiver/exporter TLS, authentication and
endpoint configuration in the platform. The module is tested but is not deployed
to any existing Alloy instance by this change.

## Grafana: Loki, Tempo, Mimir and optional Pyroscope

The intended backend is Grafana with Loki (logs), Tempo (traces) and Mimir
(metrics). Application hosts send standard OTLP to an OpenTelemetry Collector or
Grafana Alloy, which routes each signal to its backend. Keep backend credentials,
export queues/retries and backend-specific mappings in platform configuration.
Console output remains useful locally; do not ingest both OTLP logs and the same
console stream into Loki as duplicate application events.

The required request path is:

```text
External request -> gateway -> service -> Wolverine handler -> EF / cache
                                  |             |
                                  |             +-> outbox -> RabbitMQ -> consumer
                                  +-> gRPC -> another service

Applications -- OTLP --> Collector / Alloy -- traces --> Tempo
                                         -- logs ----> Loki
                                         -- metrics -> Mimir
                                                   viewed together in Grafana
```

Every participating host must register the foundation. Each hop preserves W3C
trace context or Wolverine's native envelope context, and each operation creates
its own span. Retries should remain distinguishable attempts; do not collapse
all attempts into an unexplained success. For very delayed jobs, a separate trace
with an explicit span link and durable business-operation ID can be preferable
to a trace that remains open for days. Preserve causality across outbox delivery,
redelivery and process restart; verify it rather than assuming an in-memory
Activity survives a durable boundary.

Library instrumentation covers technical operations, not their business meaning.
Add short, named activities around important workflow stages and structured logs
for meaningful transitions, retries and failures. Use stable names such as
`orders.reserve-stock`, not IDs in span names. Put the permitted business ID in a
span/log attribute so a multi-request workflow can also be found independently
of its trace. Instrumentation must not swallow exceptions or turn failed work
into successful spans.

### Correlation configuration required in the platform

- Keep a consistent `service.name`, environment and release version across all
  signals. Loki normalizes OTLP names such as `service.name` to `service_name`.
- Use Loki's native OTLP ingestion with structured metadata. Keep trace ID, span
  ID, tenant ID, request ID, instance ID and other high-cardinality fields out of
  indexed stream labels. They remain searchable metadata, not separate streams.
- Configure the Tempo data source's trace-to-logs link to Loki, mapping
  `service.name` to `service_name` and filtering by `trace_id`. Span filtering
  is optional: filtering only by a parent span would exclude descendant logs.
- Configure Loki's log-to-trace link against its actual `trace_id` field so a log
  opens the corresponding Tempo trace. Use the native OTLP IDs exported by
  Serilog, not a regular expression over the human console template.
- Enable and verify Mimir exemplar ingestion/storage and Grafana exemplar links
  to Tempo if metric-to-trace navigation is wanted. Preserve exemplars through
  the collector; trace IDs must not become ordinary time-series labels.
- Enable Tempo's service-graph/span-metrics generation and its Mimir output if
  service maps and request-rate/error/duration views derived from traces are
  wanted. Merely registering application meters does not provision this feature.

These are deployment requirements, not already-deployed resources in Teck.Cloud.
Use Grafana's documented [trace-to-logs setup](https://grafana.com/docs/grafana/latest/datasources/tempo/configure-tempo-data-source/configure-trace-to-logs/),
[Loki OTLP mapping](https://grafana.com/docs/loki/latest/send-data/otel/),
[Mimir collector integration](https://grafana.com/docs/mimir/latest/configure/configure-otel-collector/),
[exemplar support](https://grafana.com/docs/mimir/latest/manage/use-exemplars/about-exemplars/)
and [Tempo metrics generation](https://grafana.com/docs/tempo/latest/metrics-from-traces/).

### Completeness and current progress

During initial verification, retain all internally initiated request traces and
do not introduce collector sampling. The current parent-based sampler honors an
upstream decision not to sample, so it does not guarantee storage of every
external request. Define the trusted ingress sampling policy deliberately; do
not change individual services to independently sample and fragment traces.
Sampling, export failures and retention can all make logs point to an unavailable
trace; monitor collector/export failures as well as the application.

Tempo primarily receives completed spans. It is not a live debugger or a durable
workflow status database. For long-running work, emit correlated stage-start,
stage-complete, retry and failure logs, plus bounded child spans and appropriate
in-progress/queue metrics. Do not keep a single span open indefinitely and expect
it to appear immediately in the trace waterfall. See [long-running traces](https://grafana.com/docs/tempo/latest/troubleshooting/querying/long-running-traces/).

The first assembled application E2E test must follow an actual request through
the gateway, downstream gRPC, database/cache and asynchronous consumer, including
an error/retry scenario. Assert shared trace context or explicit causal links and
matching log IDs across process boundaries. The deployment acceptance check must
then demonstrate Loki -> Tempo and Tempo -> Loki navigation in Grafana. A passing
SDK export test alone does not establish that full-system contract. Browser/mobile
instrumentation is separate work when those applications are introduced.

## Verification

Infrastructure integration tests prove incoming HTTP parent context, gRPC-client
trace correlation using a controlled HTTP/2 response, sampling/configuration
boundaries, FusionCache instrumentation, and actual HTTP/protobuf exports of all
three signals with correlated console output. Tests also verify process/thread
fields, gRPC error status, nested secret masking, raw/inner exception masking, EF
entity-value exclusion, disabled exception reflection and opt-in client metadata.
A local receiver checks endpoint
routing, headers and trace IDs; it is not a production collector/backend test.

The PostgreSQL/Redis/RabbitMQ Testcontainers fixture verifies correlated Npgsql,
Redis, FusionCache and Wolverine consumer spans through a real broker delivery,
and rejects duplicate EF/Npgsql command tracing. Live gRPC mTLS and deployed
collector/query UI verification remain application E2E/deployment checks.

## References

- [Wolverine logging and OpenTelemetry](https://wolverinefx.io/guide/logging.html)
- [FusionCache OpenTelemetry](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/OpenTelemetry.md)
- [Npgsql diagnostics](https://www.npgsql.org/doc/diagnostics/tracing.html)
- [OpenTelemetry .NET instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
- [Serilog OTLP sink](https://github.com/serilog/serilog-sinks-opentelemetry)
- [Serilog async sink](https://github.com/serilog/serilog-sinks-async)
