# Licensing Host

- `LicensingHost.Build(string[] args)` returns the composed `WebApplication`; the
  separate `Program` process only runs it. Build opens no database and starts no
  broker. `Build(string[], Action<IServiceCollection>)` exists only for test clock
  overrides.
- Transports: internal gRPC `GetCurrentLicense`/`VerifyLicense` only, plus shared
  health endpoints. There is no browser HTTP surface and no provisioning/delete or
  force-active operation.
- Auth/tenant: `CommerceAuth.AddCommerceLicensingServiceAuth`; policy
  `CommerceLicenseRead`. The shared interceptor requires `teck-tenant-id` metadata
  after authorization, validates the directory and initializes `OperationContext`.
  Handlers additionally require the request tenant to equal that scope. Orders calls
  use `AddCommerceOperationCredentials` and the scoped workload principal.
- Settings (injected managed secrets, never committed):
  `ConnectionStrings:LicensingWrite` (required), `ConnectionStrings:LicensingRead`
  (optional fallback), `ConnectionStrings:rabbitmq` or `RabbitMq:Uri`,
  `CommerceOrders:GrpcAddress`, `Commerce:Identity:*` for `CommerceAuth`,
  `Commerce:Identity:TenantReaderSecret` for the read-only organization directory,
  `ConnectionStrings:redis` (Sentinel by default; isolated fixtures set
  `CommerceLicensing:Redis:Standalone=true`),
  `CommerceLicensing:ActiveKeyId`, `CommerceLicensing:KeyRing:<i>:{KeyId,PublicKey,
  PrivateKey,PassPhrase}`, optional `CommerceLicensing:Grpc:ListenUrl`,
  `CommerceLicensing:Grpc:CleartextHttp2`, `CommerceLicensing:Messaging:AutoCreate`,
  `CommerceLicensing:Database:AutoMigrate`. Credentials come only from the host's
  secret source.
- Migrations are a deployment choice: default startup does not migrate; isolated
  tests may set `Database:AutoMigrate=true` or call `MigrateAsync` on
  `LicensingWriteDbContext`. Messaging tables need the explicit auto-create flag in
  isolated environments. A malformed key ring stops startup.
- Wolverine consumes `commerce.license-desired.v1` and publishes
  `commerce.license-state.v1`; durable inbox/outbox and generation fencing carry
  restart/replay behavior. Health/OTEL/Serilog come from the shared foundation.
- Acceptance proof belongs to `tests/integration/Commerce.IntegrationTests` with
  real PostgreSQL, RabbitMQ, Keycloak and both gateway processes.
