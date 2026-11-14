# Alloy redaction policy

Import `redaction.alloy` into the platform-owned Alloy configuration. It exports
an OTLP consumer and takes separate downstream consumers for logs, traces and
metrics. Unconnected outputs are dropped, as with standard Alloy components.
For example, using an existing batch processor:

```alloy
import.file "teck" {
  filename = "/etc/alloy/policies/redaction.alloy"
}

teck.teck_redaction "application" {
  logs = [otelcol.processor.batch.application.input]
  traces = [otelcol.processor.batch.application.input]
  metrics = [otelcol.processor.batch.application.input]
}
```

Point the existing OTLP receiver's three outputs at
`teck.teck_redaction.application.input`, and route the batch processor to the
actual backend exporters. Do not retain a parallel unredacted export route.
This is an importable policy, not a standalone cluster installation. Platform
manifests, endpoints, TLS, credentials and persistence stay in GitOps.

The policy uses only generally available components, verified with Alloy 1.22.0.
It filters credential keys, masks email and bearer-token patterns, and flattens
nested maps into dotted keys. Trace/span IDs, profile IDs, service identity and
ordinary numeric measurements are retained. Scope metadata, arbitrary secret
values, arrays and secrets embedded in serialized JSON or SQL are not universally
sanitized by these rules. Application masking remains enabled. Do not log secret
payloads and treat new payload shapes as requiring explicit policy tests.

Run `bun run verify:alloy-redaction` with `ALLOY_BINARY` pointing to the official
Alloy executable. It validates the imported policy, starts an isolated loopback
Alloy process, sends synthetic OTLP logs/traces/metrics and checks the actual
exported OTLP JSON. It verifies nested credential removal, email/token masking,
span-event/status handling and correlation preservation. It stops its own Alloy
process and receiver afterwards; no service is installed or existing collector
changed. The test uses synthetic markers only, not production telemetry.
