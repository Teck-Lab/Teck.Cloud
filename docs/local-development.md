# Local Aspire and KIND

The AppHost now has three explicit profiles. Run it from your IDE or use the Bun
shortcuts below. Bun remains the JavaScript package manager and Nx owns test selection.

| Profile               | Command                               | Purpose                                                                    |
| --------------------- | ------------------------------------- | -------------------------------------------------------------------------- |
| development (default) | `bun run dev`                         | PostgreSQL, RabbitMQ management, Keycloak and Redis Sentinel               |
| kind                  | `bun run dev:kind`                    | Disposable Kubernetes cluster through the upstream Aspire KIND integration |
| sentinel              | AppHost argument `--Profile=sentinel` | Focused Sentinel failover testing                                          |

## Development prerequisites

Install the repository-pinned .NET SDK, Node and Bun, then restore with
`bun install --frozen-lockfile` and `dotnet restore Teck.Cloud.slnx`. Run a supported
container engine. Aspire supports Docker and Podman; choose the engine using
`ASPIRE_CONTAINER_RUNTIME`. A remote engine must have a working connection before
starting Aspire. On WSL2/Podman, repair the machine's connection if its configured
SSH endpoint is unavailable; this repository does not hard-code a machine IP or
silently start a different engine.

Configure the standard Aspire parameter `Parameters:redis-advertised-host` once
through AppHost user secrets/configuration. It must be reachable from both host
processes and containers. See [Sentinel networking](redis-sentinel.md). Tests use
`TECK_REDIS_ADVERTISED_HOST` for the same address. CI uses its Docker bridge address.
Do not use loopback for Sentinel when containers and clients are on different hosts.

The normal HTTPS launch profile opens the authenticated Aspire dashboard. Aspire
generates dependency credentials; never commit them. The dashboard exposes endpoints,
logs and resource state. Resources are session-owned and disposable, with no persistent
application database volume in this initial setup. Stopping the AppHost removes its
containers and local test data. This is deliberately a development/test foundation.

## Early startup diagnostics

Before resources start, run mode checks the selected engine's API with a bounded,
noninteractive probe. Development and Sentinel profiles on Windows/macOS also
check the current Podman machine's SSH connection: a working engine API alone
cannot prove Aspire's file-copy path works. KIND does not require that unrelated
machine-copy connection. Its preflight requires KIND, kubectl and Helm in both modes. Docker is not required when Podman is selected.

Development/Sentinel reject missing, malformed, loopback and wildcard advertised
addresses, and DNS names without a usable IPv4 resolution. This validates host-side
prerequisites; it cannot prove container-to-host routing before the topology exists.
The actual Sentinel E2E remains the proof of cross-host discovery and failover.
Each external probe and DNS resolution has a 20-second bound. Command output is
not copied into error messages because runtime configuration may contain credentials.
The checks neither start a runtime nor rewrite connections or machine configuration;
publish mode does not run local infrastructure probes.

## Resource contract

- `postgres` hosts `application-db`, the initial database connection resource.
  Add separate `AddDatabase` resources for actual services as they are introduced;
  sharing a server does not mean sharing service schemas or migration ownership.
- `rabbitmq` supplies the AMQP connection; its management UI appears in the dashboard.
- `redis` supplies an authenticated Sentinel connection, backed by a primary, replica
  and three Sentinels. It must be consumed through the existing Sentinel-aware client.
- `keycloak` imports the `teck-local` realm. Its public `teck-web` client uses
  authorization code + PKCE S256, with redirects restricted to localhost:3000.
  Password grants and self-registration are disabled. There are no committed user
  passwords. Use the local administration UI to create development users.

Attach future application projects with Aspire `WithReference` and `WaitFor` in the
composition. Map the Keycloak realm authority to the application's existing JWT
configuration, not the master realm. Do not replace service integration Testcontainers
with Aspire. No business service, gateway, frontend or browser-login test exists yet.
The current E2E proves dependencies and OIDC discovery, not a complete user journey.

The PostgreSQL and RabbitMQ hosting packages follow Aspire 13.6.5. The official
Keycloak integration is `13.6.5-preview.1.26559.7`; KIND is the compatible Community
Toolkit `13.6.2-beta.959`. The newer Toolkit 17 beta depends on an unavailable Aspire
17 hosting package, so it is not used. These preview dependencies are explicitly
pinned and exercised by the E2E suites.

## Kubernetes verification

Install KIND 0.33.0, kubectl 1.37.2 and Helm 4.1.4 on the same host as the AppHost. The cluster
node image is pinned to Kubernetes 1.37.0 by digest. CI installs and checksum-verifies
the tools. KIND supports Docker/Podman; when necessary select the same engine with
`KIND_EXPERIMENTAL_PROVIDER`. Do not point the AppHost at a production kubeconfig.

### Full and sandbox modes

The same kind profile uses TECK_KIND_MODE to select its networking:

| Value         | Network                               | Mesh                                 |
| ------------- | ------------------------------------- | ------------------------------------ |
| unset or full | Cilium 1.20.4, standard kube-proxy    | Istio 1.32.0 ambient, Istio's own CA |
| sandbox       | KIND's default kindnet and kube-proxy | None                                 |

Full is the normal Windows/Linux default; it requires a suitable Linux container
host kernel (WSL2 on Windows). The Orca Docker Sandbox plugin supplies sandbox
in the VM image, provisioning environment and startup hooks. Nothing needs to be
set manually inside a newly provisioned/upgraded sandbox. Existing processes must
restart to inherit an updated environment. A typo fails startup; there is no
silent downgrade and no OS-name heuristic. Setting the environment on the AppHost
process, or passing --TECK_KIND_MODE=full/sandbox, explicitly overrides the default.
This setting only changes the Aspire kind profile, not development/Sentinel or the
Orca plugin's separate persistent workspace cluster. Switching modes requires a
fresh session cluster; it does not retrofit a live cluster's CNI.

Full mode disables kindnet before cluster creation and installs Cilium, Istio base,
istiod, Istio CNI and ztunnel in order through upstream Aspire/Helm resources.
Cilium keeps kube-proxy, permits CNI chaining and disables BPF masquerading for
Istio compatibility. Application manifests wait for the mesh and common platform controllers. Installation
waits are bounded; full-mode failures identify the dependency and do not fall back
to sandbox mode. Toolkit 13.6 rejects sibling WaitFor annotations under a common
KIND parent, so the ordering uses its supported BeforeResourceStarted lifecycle
callback and resource notifications instead. Named kubectl rollout checks are
required because the generic Helm health check treats zero label matches as healthy.

Application namespaces must explicitly opt into ambient with
istio.io/dataplane-mode: ambient; enforce STRICT PeerAuthentication and the intended
AuthorizationPolicy in the actual application/platform manifests. Installing the
mesh does not by itself enforce application mTLS/default-deny policy. Both modes also
install the [common KIND platform](kind-platform.md): Gateway API, kgateway, cert-manager,
CloudNativePG, Redis Operator, OpenBao Operator and Argo Rollouts. Argo CD and Kargo
are deliberately absent.

Docker Sandboxes 0.53.0's Windows kernel still lacks IPv4 multiple routing tables
and Cilium kernel prerequisites. Sandbox mode is for application/manifest tests
that do not assert those platform features; full-mode security/network behavior
must be tested on a supported host. Never skip a required mesh assertion and call
it a passed sandbox test.

The `kind` profile creates a unique `teck-cloud-dev-*` cluster, uses its own kubeconfig and deletes its
session cluster on graceful shutdown. An explicit
`Kind:Name` override must be distinct for each concurrent session. Do not reuse a name belonging
to another developer or AppHost. A forced process termination may require cleanup
of that exact cluster after verifying its ownership.

### Keycloak in the KIND profile

`bun run dev:kind` builds the shared themed Keycloak image, loads it into the new
session cluster, and applies `deployment/local/identity` after the platform
controllers are ready. With Podman, the image is exported with `podman save` and loaded through KIND's `image-archive` command because KIND's `docker-image` command still calls the Docker CLI. Local images and manifests use matching `localhost/teck/` tags. The overlay includes a CNPG database, a Keycloak Deployment,
the Hostzero Keycloak operator's instance and realm resources, a certificate, and a
kgateway route. This follows the same ownership split as `Teck.GitOps` local:
Hostzero manages the realm settings through the Admin API; the shared realm import
still supplies clients and authentication flows. The operator does not create the
Keycloak server or database. In full mode, the operator namespace joins ambient mesh before Helm installs the operator, so its Admin API calls satisfy Keycloak's mTLS policy. Sandbox mode leaves that namespace unmeshed. Aspire waits for the database, Keycloak, operator
resources, and Gateway before deploying candidate manifests.

Open `https://localhost:18443/admin/` after the identity-ready resource
is running. The KIND node maps the Gateway's NodePort to loopback only. This
Aspire profile uses `localhost` because nested `.localhost` names are not resolved by
Windows on every developer machine; the GitOps cluster keeps its own hostname. The local CA
is disposable and not automatically trusted by the browser. A one-off browser
exception for this loopback-only session is possible; automated tests instead trust the
current cluster CA and never disable certificate checks. Do not install this disposable
CA in the system-wide trust store. The administrator user is `admin`; its
generated password is stored as the secret Aspire parameter
`Parameters:kind-keycloak-admin-password` in local AppHost user secrets. Do not copy
that password into source, a command line, or logs.

Set `--Kind:Identity=false` only for infrastructure-only tests that intentionally
omit identity. The E2E suite keeps identity enabled and sets
`--Kind:IdentityBuild=false` after its image-build target runs. Both full and sandbox
modes install identity. Full mode additionally protects the Keycloak workload with
ambient mTLS and a namespace allow policy; sandbox mode does not claim mesh security.
To deploy an actual application, set `Kind:Manifests` to an absolute path or a path
relative to the AppHost directory, for example `../../../deployment/local` once that
overlay exists. Aspire applies the real Kustomize overlay or manifest directory
using the cluster-specific kubeconfig and server-side apply. Container images must
already be reachable from that cluster (for example a local registry), or be loaded
with KIND's supported image loading workflow before application deployment.
No alternative application manifests are generated by this foundation.

`tests/e2e/kubernetes/Kind.E2ETests` verifies infrastructure and operator behavior:
it applies an ambient-labelled test namespace and Job, proves a pod executes with
working cluster DNS, then verifies deletion. Full mode additionally checks Cilium
and ztunnel rollout and the pod's ambient redirection annotation. Sandbox mode
checks kindnet rollout and the absence of an Istio namespace; the namespace label
is inert without the mesh. These checks do not prove mTLS/authorization policy. CI
runs this same suite once per mode. It additionally exercises the [common platform](kind-platform.md#behavioral-proof)
with HTTPS/Rollouts, CNPG, authenticated Redis Sentinel and OpenBao fixtures. It does
not claim to test business application deployments. Actual
application E2E must use `deployment/local`, candidate images, rollout readiness
and externally observable assertions when the first application is added.

## Verification

- `bun run test`: unit and architecture checks.
- `bun run test:integration`: focused real-dependency Testcontainers tests.
- `bun run test:e2e`: the default Aspire stack smoke and Sentinel failover.
- `bun run test:kubernetes`: isolated Aspire/KIND lifecycle and workload smoke.
- `bun run coverage`: full unit/integration coverage gate.
- `bun run security:audit`: NuGet and Bun dependency checks.

Validation on 2026-11-14 passed both full and sandbox modes in separate disposable
clusters on the Windows/Podman WSL2 host. Full mode verified named component
rollouts, enabled ambient redirection on the live smoke pod, DNS and Job completion.
Reduced mode verified kindnet and no Istio namespace. Every test cluster was removed;
the existing Docker Sandbox and its persistent cluster were untouched. This proves
the two profiles on WSL2, not a fresh image build or an in-sandbox runtime test.
The expanded platform runs passed in 3m53s (sandbox) and 4m35s (full), including
CA-validated gateway HTTPS, Argo Rollouts, real CNPG persistence, authenticated
Redis Sentinel discovery/read-write and automatic OpenBao initialization/unsealing.
All 23 AppHost unit tests passed. See [validation evidence](validation-foundation.md).

References: [Aspire KIND](https://aspire.dev/integrations/compute/kind/kind-host/),
[Aspire Keycloak](https://aspire.dev/integrations/security/keycloak/),
[KIND quick start](https://kind.sigs.k8s.io/docs/user/quick-start/),
[Cilium on KIND](https://docs.cilium.io/en/stable/installation/kind/),
[Istio ambient prerequisites](https://istio.io/latest/docs/ambient/install/platform-prerequisites/),
[Istio chart registry migration](https://istio.io/latest/blog/2026/retirement-of-gcp/).
