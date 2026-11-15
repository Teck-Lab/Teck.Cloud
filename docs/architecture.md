# Repository architecture

One Nx workspace coordinates native language toolchains. Migrate selected business
features deliberately from Teck.Monorepo, one verified feature at a time.

- src/apps/<app>: Next.js and React Native application entry points.
- src/services/<domain>/<service>: service-owned Domain/Application/Infrastructure/Host projects;
  add layers only for actual responsibilities.
- src/services/gateway/<gateway>: customer/admin gateways.
- src/packages/<package>: TypeScript package with its own package.json.
  The design-tokens package owns the shared semantic light/dark palette; Keycloak consumes
  its CSS, and future web/native apps consume its CSS or JavaScript exports.
- src/shared/<project>: narrowly scoped .NET library with its own csproj.
- src/aspire/<project>: AppHost composition and service defaults.

Directories are reserved without manufacturing empty application projects. Share
API clients, contracts and platform-independent logic deliberately. Keep web/native
UI implementations separate where appropriate. App packages own their framework
dependencies so mobile modules do not leak into web bundles.

## Service conventions

See [the service structure](service-structure.md) for project responsibilities,
capability-based features, persistence, messaging, migrations and the service test
contract. It also records legacy inconsistencies that must be reconciled during
migration. src/services/AGENTS.md makes these conventions discoverable to agents.

The selected service stack is FastEndpoints with standard endpoint classes,
ServiceScan source-generated DI registration, Wolverine CQRS and typed results,
and Scalar API documentation. Preserve vertical slices and prefer compile-time
generation to runtime reflection. Retain EF Core/JIT while preparing for future
Native AOT; see the service conventions for generation and compatibility boundaries.

## Communication boundaries

Domain events (IDomainEvent) remain inside their owning service. Local handlers may
translate them into separate integration events (IIntegrationEvent). Cross-service
async messages use Wolverine/RabbitMQ; synchronous calls use gRPC. HTTP/REST is for
external ingress, including the customer/admin edge. Read [messaging](messaging.md)
for Wolverine EF collection, routing, durable handoffs and contract ownership.

## Nx project graph

@nx/dotnet infers each csproj and ProjectReference dependency; also register projects
in Teck.Cloud.slnx for IDE/SDK workflows. TypeScript packages use workspace:*
dependencies, per-project tsconfig files and tsconfig.lib.json for buildable libraries.
Run bun run sync after adding/changing project dependencies and commit the generated
TypeScript references. CI runs sync:check without mutating files. @nx/js/typescript
infers their build and typecheck tasks. Buildable library package exports must
point to compiled outputs (with matching declaration exports); source-only packages
may intentionally have typecheck but no build target. Verify inferred targets rather
than assuming that an empty nx run-many result built a package.

Add matching @nx/next and the selected React Native/Expo plugin with the first apps.
The layout does not select a native mobile testing tool.

Tag projects by scope, type and platform. Tags alone do not enforce boundaries;
add lint/analyzer/architecture enforcement with actual projects. Cross-process and
cross-language dependencies need explicit graph edges where inference cannot see
them: generated clients depend on contract producers; E2E projects depend on the
apps, gateways and services they exercise. Use project implicitDependencies for
those relationships. Target dependsOn schedules work but does not replace graph edges.

Validate both directions of affected selection: shared changes select consumers,
unrelated projects stay excluded. Root config inputs are conservative until narrower
analysis is demonstrated. Build contexts and deployment inputs also belong in the graph.

References:

- https://nx.dev/docs/kb/folder-structure
- https://nx.dev/docs/features/multi-language-support
- https://nx.dev/docs/technologies/dotnet/introduction

## Identity platform

The [identity integration plan and diagrams](identity-platform-plan.html) describe
Keycloak Organizations, gateway token exchange, authorization, theme and event
integration. Open the standalone HTML in a browser for current acceptance status.
Native package composition is documented in [Keycloak AuthServices](keycloak-authservices.md);
the shared lookup and scope contract is in [identity tenancy](identity-tenancy.md).

Customer and admin gateways each act as the BFF for their own clients. They compose
responses with independently authorized gRPC calls; business rules and durable workflows
stay with backend services. kgateway provides infrastructure routing, not BFF logic.
