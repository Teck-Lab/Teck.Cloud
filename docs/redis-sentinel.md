# Local Redis Sentinel with Aspire

`src/aspire/Teck.Cloud.AppHost` is the runnable local composition. It creates one Redis
primary, one replica and three Sentinels (quorum 2), using the official Redis image.
Aspire owns resource startup, dynamic ports, secrets and cleanup. A small container entry
script renders Redis's writable Sentinel configuration; no host-side cluster setup script
or permanent VM configuration is installed.

The `redis` connection resource includes all three Sentinel seed ports, `serviceName=teck`
and Redis/Sentinel authentication. Reference it from application resources using Aspire's
normal `WithReference(redis)` and register `AddFoundationRedis` in the consuming host.
All local application and E2E consumers use Sentinel, just like the production client path.

## Local configuration

Set the AppHost parameter `Parameters:redis-advertised-host` through standard Aspire
configuration/user secrets. It must be an IPv4 address or DNS name reachable by BOTH the
host applications and all Redis/Sentinel containers. With a remote Podman WSL2 machine,
this is usually the machine's current routable WSL address. A loopback address inside a
container points to that container, and container-only DNS names cannot be resolved by
Windows applications. Sentinel must announce the published ports, not internal 6379/26379.

The topology explicitly announces the selected address and Aspire-allocated published ports
for replicas and Sentinels. Endpoints are unproxied so that discovery reaches real Redis
ports. Published ports bind to all engine-host interfaces so sibling containers can reach
them; authentication is required. Keep this development engine on a trusted network.
Choose a different address when moving between Docker Desktop, Podman, WSL2 or a
remote engine; the repo does not commit a developer's IP address. Runtime connectivity must
work first. A broken Podman machine SSH endpoint is a runtime problem, not a Sentinel setting.
AppHost preflight reports that SSH problem before starting containers on Windows/macOS,
even when the engine API itself is reachable. It also rejects unusable advertised
addresses before startup; successful DNS resolution does not prove container reachability.

`redis-password` is generated as an alphanumeric secret, persisted in local user secrets,
and reused for Redis, replication and Sentinel authentication. It may be overridden through
`Parameters:redis-password` with an alphanumeric secret. The local topology has no durable
cache volumes and no TLS listener; it is a development/test cache, not a production deployment.
Redis containers do not need Aspire's HTTPS certificate injection and use their image's
normal trust settings.

Use the normal Aspire run/debug workflow with the AppHost project. Bun remains the JS package
manager; the AppHost does not introduce npm or change Nx workspace ownership.

## E2E verification

`tests/e2e/web/Redis.Sentinel.E2ETests` runs through Nx `test:e2e`. Set
`TECK_REDIS_ADVERTISED_HOST` to the reachable engine-host address and select the normal
Aspire container runtime. The test supplies its own random secret, starts the actual AppHost,
waits for replication plus replica discovery and quorum on all three Sentinels, shuts down
only its primary and checks discovery of the promoted
replica, retained replicated data, cross-host cache access and healthy Redis checks using
the original application multiplexer. Aspire disposes the test topology afterwards.

Standalone Redis Testcontainers remain useful for focused integration tests and are explicitly
registered with `AddFoundationStandaloneRedis`. They do not substitute for this Sentinel E2E.

Three Sentinels on one development machine exercise quorum and failover protocol behavior;
they do not provide independent failure domains. Production places Sentinels across independent
failure domains and configures its own storage, ACLs, TLS, monitoring and backup policy.

References: [Redis Sentinel](https://redis.io/docs/latest/operate/oss_and_stack/management/sentinel/),
[Aspire Redis integration](https://aspire.dev/integrations/caching/redis/redis-host/).
The standard AddRedis resource does not configure this full Sentinel topology; this composition
uses Aspire's supported container, endpoint, parameter and connection-resource APIs.
