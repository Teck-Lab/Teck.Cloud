# Infrastructure migration audit

Status: the shared Core/Persistence/Infrastructure split is implemented with
global, tenant-scoped and mixed models. The table below preserves the source audit
and reuse decisions; it is not a claim that every legacy feature was ported.
See [shared runtime](shared-runtime.md) for actual APIs, tests and remaining limits.

Implemented: repositories, EF-only and Wolverine transaction ownership, Finbuckle
isolation, auditing/soft deletion, cache/backplane/locks, scoped HTTP/message/gRPC
identity, OAuth, standard FastEndpoints/ErrorOr, hosting/settings and diagnostics.
Services own permissions, dependency readiness checks, retry policy, migration
execution, tenant provisioning and live identity-provider integration. OpenBao
provisioning, feature flags and business audit publication remain deferred.
PostgreSQL, physical-envelope deduplication and pending outbox recovery after a
broker outage/application restart are tested. Other database providers, business
idempotency and abrupt process-kill windows remain explicit validation work.

## Recommended shared projects

| Project                     | Responsibilities                                                                                                                                                                 |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SharedKernel.Core           | Existing domain/CQRS primitives, repository/unit-of-work/cache contracts, plain tenant contracts and Options marker                                                              |
| SharedKernel.Persistence    | EF contexts/helpers, repositories, unit-of-work adapter, audit/soft-delete interceptors, tenant database enforcement/resolution, FusionCache/Redis and distributed locks         |
| SharedKernel.Infrastructure | Host composition, auth, tenant resolution at ingress, gRPC, Wolverine/RabbitMQ, observability, health endpoints, OpenAPI/Scalar, settings validation and transport error mapping |

Persistence references Core. Infrastructure may reference Core and Persistence.
Persistence must not reference Infrastructure: shared tenant/actor contracts belong
in Core so authentication and messaging can establish context without reverse
dependencies. The Wolverine/EF transaction bridge can live in Persistence and
reference Wolverine packages; transport configuration remains in Infrastructure.

Redis/FusionCache fit here as storage/cache adapters. Register EF, distributed cache,
locks and tenancy separately: enabling one does not require every external server.
A single Persistence assembly still brings its package dependencies transitively;
optional DI does not remove them. Split provider/cache adapter packages later if
dependency footprint or independent consumers justify it.

This is a shared-library split, not a fourth project required in every service.
The existing service Domain/Application/Host layout remains. Concrete models,
migrations, schemas and business rules stay with their service.

## Reuse decisions

Paths below are relative to Teck.Monorepo/src/shared/SharedKernel.Infrastructure.
"Adapt" means reuse the code and intent after the listed changes, not discard the pattern.

| Existing area                                                    | Destination / decision                                                                      | Changes before acceptance                                                                                                                                                                                                                                           |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Database/EFCore repositories and specifications                  | Persistence; adapt                                                                          | Match the newly imported contracts, explicit tracked mutation loads, read-only context, bounded queries and provider-tested SQL translation.                                                                                                                        |
| Database/EFCore/UnitOfWork                                       | Persistence; substantially adapt                                                            | Share the actual write context with repositories/Wolverine; explicit ownership of transactions and disposal. The current factory constructor can create a separate context, and Dispose also disposes an injected context.                                          |
| BaseDbContext, model/config helpers                              | Persistence; adapt                                                                          | Neutral global base plus tenant-specific behavior. Remove pricing coupling and PostgreSQL types from provider-neutral mapping. Audit configuration currently hardcodes timestamp with time zone.                                                                    |
| Audit/soft-delete interceptors                                   | Persistence; adapt                                                                          | Transport-neutral actor context and TimeProvider; cover synchronous and async save paths. Retain optional behaviors.                                                                                                                                                |
| Database/MultiTenant                                             | Persistence; selective reuse                                                                | Keep connection resolution, scope and health-check concepts. Remove Pricing enums, old Customer API and plan coupling; preserve rejection of missing dedicated-tenant connections rather than falling back to shared storage.                                       |
| Caching                                                          | Persistence; adapt                                                                          | Keep FusionCache, Redis, backplane and distributed-lock integration. Explicit tenant/global cache scope, stable service/type/version namespaces, escaping, invalidation after commit and bounded stale-data policy. Remove ignored tracking parameter.              |
| MultiTenant                                                      | Infrastructure resolution + Persistence enforcement; adapt                                  | Keep Finbuckle and validated identity resolution. Reduce TenantDetails to required identity facts/configuration; no fabricated active tenant, raw-header authority or compulsory tenancy for global operations.                                                     |
| Auth and GrpcHostingExtensions                                   | Infrastructure; selective reuse                                                             | Keep authentication, token exchange and client-credential concepts. Replace blocking token acquisition, respect cancellation/deadlines, support background actors and read tenant data from validated context. Reconcile each audience/permission with the new app. |
| Messaging and Hosting/TeckMessagingExtensions                    | Infrastructure + Persistence bridge; adapt                                                  | Durable Wolverine inbox/outbox, EF domain-event collection, explicit tenant/global message classification and local/external routing. Broker-required services fail startup without broker configuration.                                                           |
| Messaging/DeadLetter                                             | Infrastructure; adapt                                                                       | Keep retry/error-queue configuration; validate settings and classify retryable failures instead of indiscriminately retrying every exception.                                                                                                                       |
| Messaging/Idempotency                                            | Replace implementation                                                                      | Prefer Wolverine durable inbox for transport duplicate detection, plus transactional business idempotency where needed. Do not carry the Redis pre-execution payload marker over.                                                                                   |
| Behaviors                                                        | Selectively rebuild                                                                         | Keep validation, transaction and generated-handler composition. Remove LicenseEnforcementMiddleware. Verify typed ErrorOr responses instead of copying the custom unwrapping logic and globally relaxing service-location rules.                                    |
| Endpoints                                                        | Keep small FastEndpoints/ErrorOr and permission helpers                                     | Exclude AuthenticatedEndpoint, AdminEndpoint and VersionedEndpoint inheritance hierarchy; retain standard FastEndpoints classes. Minimal API-specific helpers are not needed for the selected API stack.                                                            |
| Hosting / root Extensions / Middlewares                          | Infrastructure; consolidate and adapt                                                       | One middleware pipeline, explicit capabilities, generated endpoint registration, deliberate CORS/proxy trust, tenant-aware rate limiting and safe exception responses.                                                                                              |
| Observability                                                    | Infrastructure/service defaults; adapt                                                      | Keep OpenTelemetry + Serilog conventions; register once alongside Aspire integration, use configured OTLP, avoid redundant exporters and secret/payload logging.                                                                                                    |
| HealthChecks                                                     | Dependency checks in Persistence/messaging adapters; endpoint composition in Infrastructure | Keep checks for enabled dependencies only; separate process liveness from dependency readiness and avoid unnecessary duplicate connections.                                                                                                                         |
| OpenApi                                                          | Infrastructure; adapt                                                                       | Keep Scalar/OpenAPI and useful audience metadata. Verify generated documents against standard endpoints; no old endpoint-base dependency or default internal exposure.                                                                                              |
| Options and Validation                                           | Core marker + Infrastructure helpers; mostly reuse                                          | Keep typed options and small validators. Prefer generated binding/validation with startup failure for missing required settings. Port only validators compatible with the new Core paging contract.                                                                 |
| Resilience                                                       | Infrastructure; adapt                                                                       | Keep bounded timeout/retry/circuit-breaker helpers. Avoid duplicate registration with Aspire; unsafe writes and non-idempotent calls need explicit retry policy.                                                                                                    |
| FeatureFlags                                                     | Defer unless a real consumer needs it                                                       | Existing OpenFeatureExtensions registers a custom in-memory provider, not an OpenFeature SDK integration. Choose simple configuration flags or a real provider deliberately.                                                                                        |
| AuditPublisher                                                   | Defer contract, keep concept                                                                | It directly depends on old SharedKernel.Events/AuditEvent. Define the new audit contract and outbox semantics before porting it.                                                                                                                                    |
| ProviderFilteredMigrationsAssembly / automatic migration helpers | Replace or redesign                                                                         | Avoid reflection-driven provider filtering; migrations remain provider/service-owned. Never apply migrations through a read principal or automatically migrate a replica.                                                                                           |
| CodeGenerationDetector / CodegenConnectionString                 | Adapt if still needed                                                                       | Support the actual build-time codegen path, with any dummy connection strictly limited to that mode; normal runtime configuration fails clearly.                                                                                                                    |

Do not import the entire csproj dependency list. It includes unrelated provider,
health-check, storage, serialization and runtime-compilation packages. Add dependencies
with their real consumers and validate generated/static handler builds.

## Concrete findings behind the changes

These are source-level findings, not a claim that the old application was exploited
or that its current production configuration reaches every path.

- Caching/GenericCacheService.GenerateCacheKey omits tenant scope when the optional
  accessor has no tenant, and uses only the entity's short type name as its prefix.
  Global and tenant caches must be intentionally different; absent tenant is not
  automatic permission to use a global key.
- Caching/Extensions creates a direct Redis multiplexer alongside distributed-cache
  and backplane registration, uses synchronous Connect, and applies two-hour
  fail-safe defaults globally. Review connection ownership and per-cache freshness.
- Messaging/Idempotency/IdempotencyMiddleware writes a Redis marker before awaiting
  the handler, without clearing it on failure or including envelope tenant scope.
  A subsequent attempt can therefore be treated as a duplicate after failure.
- Auth/GrpcTokenExchangeInterceptor uses GetAwaiter().GetResult() with
  CancellationToken.None. Its context factory reads raw tenant headers and requires
  HttpContext; this does not cover background/message execution safely.
- TenantRateLimitMiddleware partitions by a caller-supplied header; the current
  host pipeline runs it before authentication. Reuse the rate-limiting capability,
  with validated tenant partitions and separate anonymous ingress limits.
- Root Extensions registers one CORS policy name and uses another, and clears
  known-proxy/network restrictions. Hosting/TeckServiceExtensions is a second
  overlapping pipeline. Consolidate these rather than copying both.
- TenantDetails carries Plan and database topology/connection data together.
  CustomerApiTenantStore depends on old HTTP service routes, which conflict with
  the new gRPC-only internal business communication convention.
- The existing EF mapping helpers and exception processor contain PostgreSQL-specific
  details. Their existence is not proof of SQL Server or MySQL/MariaDB support.
- FeatureFlags/OpenFeatureExtensions and IFeatureProvider implement configuration
  plus process-local overrides. Their names do not make them an OpenFeature adapter.

## Global, tenant-scoped and mixed services

Multi-tenant support is mandatory in the foundation; tenancy is not mandatory for
every service, entity or operation.

| Service/data shape | Required behavior                                                                                                                 |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| Global-only        | Ordinary read/write contexts and explicit global cache/message paths; no tenant resolver or fake tenant required                  |
| Tenant-only        | Tenant-owned entities and operations require validated scope; missing scope is rejected                                           |
| Mixed              | Global reference/platform data and tenant-owned data are explicitly classified; global behavior cannot bypass tenant restrictions |

BaseEntity must not acquire TenantId unconditionally. ITenantScoped marks ownership
where needed. In EF configuration, designate tenant entities explicitly and verify
the mapping in tests. Finbuckle supports tenant-designated and shared entities in
one model; the marker alone does not automatically configure it.

Global operations that run without a tenant need an explicit global repository/context
path. If a mixed EF context requires tenant scope, use a separate global context
for those operations rather than disabling its tenant filter. This can use the
same physical database. Do not represent "global" as a missing tenant on a tenant
entity, or as a default sentinel tenant.

Use separate cache namespaces, for example global/countries versus
tenant/{tenantId}/orders, including service/type/version and escaped components.
Classify messages and jobs similarly. Never copy ambient tenant context onto a
declared global message without an intentional contract; never let a tenant-scoped
message run globally because its metadata is absent.

Global data still requires its own authorization. A tenant user may read a global
country catalogue without being permitted to modify platform configuration.
Tenant/global relationships and uniqueness constraints must be modeled deliberately.

## Migration sequence and remaining work

Steps 1-4 now have shared implementations and focused tests. This does not prove
every capability combination or service-specific recovery case. Step 5 remains
deferred until it has a consumer.

1. Add SharedKernel.Persistence and register it in Nx/solution. Port EF repositories,
   unit-of-work ownership and optional audit/delete behavior against current Core.
2. Implement tenant/global/mixed persistence behavior and cache scopes; test them
   with actual database and Redis Testcontainers.
3. Add the reusable Infrastructure composition, auth/gRPC, Wolverine and tenant
   propagation, with generated handlers and durable local/broker integration tests.
4. Add health/observability/settings/OpenAPI and verify host startup with each
   capability combination (including no Redis, no database and no tenancy).
5. Migrate optional OpenBao-backed tenant connection resolution, flags and audit
   publication only with an actual consumer and a defined contract.

Use existing tests as inputs, not unquestioned specifications. Relevant old tests
include tenant resolution/propagation, token exchange, codegen connection handling
and endpoint permissions. Rewrite tests that assert the removed endpoint hierarchy
or silent broker fallback. Add missing tests for context identity, transaction
rollback/outbox recovery, tenant/global caches, concurrent scopes and mixed data.

Acceptance includes separate read/write identities, cross-tenant read/update/delete
rejection, global operations without tenant context, detached/bulk writes, cache
invalidation after commit, consumer retries, and gRPC background calls. Use Aspire
only for assembled E2E; service integration dependencies use Testcontainers.

## Primary references

- [Finbuckle EF entity designation and mixed models](https://www.finbuckle.com/MultiTenant/Docs/v10.1.6/EFCore).
- [Wolverine durable inbox behavior](https://wolverinefx.net/guide/durability/).
- [gRPC client factory and asynchronous call credentials](https://learn.microsoft.com/en-us/aspnet/core/grpc/clientfactory).
- [Generated options validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator).

These references inform adapter choices; exact package compatibility and runtime
behavior must be verified during migration.
