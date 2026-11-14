# Service health

Every service calls `AddFoundationHost()` and maps `MapFoundationHealth()`.
Redis, RabbitMQ, Keycloak, application lifecycle, system disk and Wolverine bus/listener
checks are registered by default. There are no enable/disable switches. Missing dependency
configuration makes readiness unhealthy; it does not silently skip a required check.

## Endpoints

| Endpoint               | Purpose                                                    | Dependency outages                               |
| ---------------------- | ---------------------------------------------------------- | ------------------------------------------------ |
| `/health/live`         | Process responds; use for Kubernetes liveness.             | Remains healthy.                                 |
| `/health/startup`      | Host has reached `ApplicationStarted` and is not stopping. | Does not probe dependencies.                     |
| `/health/ready`        | Host lifecycle and every required dependency are healthy.  | Returns 503, including degraded checks.          |
| `/health/dependencies` | Diagnostic JSON with check names, status and duration.     | Returns 503 for degraded/unhealthy dependencies. |

Startup supports slow host initialization without early liveness restarts. It does not
prove arbitrary background warm-up has completed: service-specific warm-up must participate
in host startup or register an additional readiness check. Do not put dependency checks in
liveness: restarting every service during a broker/database outage makes recovery harder.

The dependency endpoint omits exceptions, descriptions, connection strings and arbitrary
check data. Keep all health routes internal at ingress/network level, especially dependency
diagnostics; these endpoints are not customer API routes. Probe authentication, if enabled
by host authorization policy, must also be supported by the probing infrastructure.
Do not expose a general `/health` alias or a separate HTTP endpoint for every dependency.
Metrics continue to use OTLP, not health responses.

## Default checks and composition

- `application`: published ApplicationStatus check observes host shutdown.
- `system-disk`: published System disk check requires 512 MiB free on the content-root
  drive by default. `Health:System:Drive` selects the writable drive and
  `Health:System:MinimumFreeMegabytes` sets a positive threshold. Configure the actual
  writable mount in containers. This is not a Kubernetes node or memory-limit probe.
- `redis`: the shared `RedisConnection` supplied by `AddFoundationRedis` probes the
  Sentinel-selected primary with `DemandMaster`. A stopped former primary does not fail
  readiness after successful promotion. Standalone test connections use the published
  Redis check. See [Sentinel composition](redis-sentinel.md).
- `rabbitmq`: published RabbitMQ check opens/closes a channel on one lazy, host-owned,
  automatically recovering probe connection to the URI from `UseRequiredRabbitMq`.
  Wolverine owns its separate transport connections; no connection is opened per probe.
- `keycloak`: published OpenIdConnectServer check fetches realm discovery from the HTTPS
  authority configured by `AddFoundationAuthentication`. Workers without JWT authentication
  supply `Authentication:Authority` instead. Use the realm URL, e.g.
  `https://identity.example.com/realms/teck`, not the server root. Redirects are disabled;
  TLS is validated. No client secret or admin credential is required. This is a discovery
  availability check, not a token issuance/login test or proof of every realm/client setting.
- `wolverine-bus` and `wolverine-listeners`: `WolverineFx.HealthChecks` checks the started
  runtime and listener states. Degraded listener states fail readiness. The published
  listener check considers an empty listener set healthy; application tests must still prove
  required queue bindings and actual message delivery.

Checks use a five-second timeout by default; `Health:TimeoutSeconds` accepts 1–30 seconds.
Redis and RabbitMQ adapter waits respect the health cancellation budget. Driver-owned
reconnection may continue independently. Do not run probes in a tight polling loop.

### Provider-dependent database checks

PostgreSQL is conditional on the selected provider, not on a health toggle.
`UseFoundationMessaging<TWrite>` automatically adds `postgres-messaging` for the PostgreSQL
inbox/outbox database. It executes the package's lightweight SQL check and uses Npgsql pooling.
For additional PostgreSQL read/write data sources, register each required route using the
existing DI-owned data source:

```csharp
builder.Services.AddFoundationPostgresHealth(
    services => services.GetRequiredService<NpgsqlDataSource>(),
    "postgres-read");
```

Use unique names and select keyed data sources when read/write endpoints differ. Do not
register Npgsql against a SQL Server/MySQL service; its provider composition must add the
corresponding database check instead. PostgreSQL probes currently use a fixed five-second
budget. Readiness does not prove migrations, database privileges for all operations, tenant
isolation, backup recovery or end-to-end application correctness.

## Verification

Integration tests stop/restart real PostgreSQL, RabbitMQ and Redis Testcontainers, verify
healthy -> unhealthy -> healthy behavior on the same host and keep liveness healthy.
HTTP tests verify startup/readiness separation, missing mandatory configuration and safe
JSON responses. Controlled OIDC discovery responses verify the Keycloak realm path,
malformed discovery and failure/recovery. Real Keycloak login, token validation and
production probe/ingress wiring remain service E2E/deployment responsibilities.

## References

- [ASP.NET Core health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
- [Published dependency health checks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
- [Wolverine health checks](https://wolverinefx.io/guide/health-checks.html)
