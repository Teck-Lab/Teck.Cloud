# Common KIND platform

The Aspire `kind` profile installs these components in **both full and sandbox modes**.
Only the Cilium/Istio networking layer differs. Start through the existing `bun run dev:kind`
entry point or the IDE; no separate bootstrap script or Terraform is required.
All resources belong to the disposable session cluster. This does not alter the Orca
plugin's persistent workspace cluster or an existing user cluster.

| Component                       | Pinned chart / release            | Purpose                                              |
| ------------------------------- | --------------------------------- | ---------------------------------------------------- |
| Gateway API                     | 1.7.0 experimental CRDs           | Standard APIs plus experimental resources and fields |
| kgateway and its CRDs           | 2.5.2                             | Envoy gateway controller and on-demand proxies       |
| cert-manager                    | 1.21.3                            | Certificate issuance and renewal                     |
| CloudNativePG                   | chart 0.29.1 / operator 1.30.1    | PostgreSQL Cluster reconciliation                    |
| OT-CONTAINER-KIT Redis Operator | 0.27.0                            | Redis replication and Sentinel reconciliation        |
| KubeBao OpenBao Operator        | 0.6.1                             | OpenBao cluster lifecycle and tenant onboarding      |
| Argo Rollouts                   | chart 2.43.12 / controller 1.10.2 | Progressive delivery independent of Argo CD          |

Versions are configured in `KindCommonPlatform.cs` and the Gateway API Kustomization.
Gateway API uses the pinned experimental bundle, which includes the standard APIs
plus experimental resources and fields. Individual features still require controller
support; experimental APIs can change between releases.
CRDs and controllers come from upstream release artifacts/charts. Argo Rollouts uses
its separate chart in [argo-helm](https://argoproj.github.io/argo-helm/); the Argo CD
chart is not an umbrella installer. **Argo CD and Kargo are not installed.** There is
no remote GitOps or promotion connection and no GitHub credentials are required.

## Readiness and ownership

Aspire installs Gateway API with server-side apply and waits for CRD establishment.
kgateway's own CRD chart precedes its controller. Full mode's mesh also waits for
Gateway API. All common controllers wait for the full-mode network when applicable.
A `platform-defaults` resource waits for named controller Deployment rollouts before
applying the local CA. Candidate manifests wait for that barrier. Errors and deadlines
fail startup; they never select a smaller platform silently.

Helm is required in both modes. The Orca image sources now include Helm 4.1.4 and
kubectl 1.36.6, which also stays compatible with its existing Kubernetes 1.35 workspace
cluster. Those image changes require a new kit build/release; reinstalling a plugin does
not replace binaries in an already-running sandbox. The kgateway registry is included
in the image network allowlist.

Chart URLs are pinned and do not require adding global
Helm repository aliases. If an existing Helm configuration references missing repository
index caches, repair that configuration with Helm's supported repository commands; this
AppHost does not rewrite the developer's global Helm configuration.

The platform installs **operators**, not a permanent shared PostgreSQL database, Redis
fleet or OpenBao tenant. Application/environment manifests own their Cluster,
RedisReplication/RedisSentinel, OpenBaoTenant/OpenBaoCluster and Rollout resources.
Redis consumers must use Sentinel rather than a fixed primary address. OpenBao namespaces
must be onboarded before creating their clusters; preserve the operator's admission and
RBAC enforcement. OpenBao needs API-server egress after Service translation as well
as the Service VIP. Supply the actual endpoint IPs in spec.network.apiServerEndpointIPs;
with Cilium also use spec.network.apiServerEgress.mode=Cilium and port 6443. The proof
discovers the owned cluster endpoint dynamically, never a hard-coded developer IP or
an unrestricted egress rule. No root tokens or unseal keys are committed or printed.

## Certificates and gateway access

cert-manager creates a disposable, self-signed root CA in its namespace and exposes the
`teck-local-ca` ClusterIssuer. Use explicit Certificate resources referencing that issuer
and Gateway HTTPS listeners referencing their TLS Secrets. The CA is **not automatically
trusted by Windows/Linux or browsers**. A test/client must explicitly trust its CA
certificate. Never bypass verification or import this disposable CA globally.

kgateway installs the `kgateway` GatewayClass. Actual gateways and routes belong in the
application's local overlay, including customer/admin separation when those gateways are
implemented. The installer does not expose an unauthenticated public gateway or invent
an application deployment. KIND has no cloud load balancer by default: external access
needs a deliberate KIND port mapping or a supported local load-balancer solution.
The platform proof exercises in-cluster HTTPS through the real proxy.

Argo Rollouts blue/green service switching works without Argo CD. Gateway API weighted
canary routing additionally needs the upstream Rollouts Gateway API traffic-router plugin
and corresponding Rollout configuration; that plugin is not yet installed by this profile.

## Behavioral proof

The Kubernetes E2E suite creates actual operator-managed resources in its own test cluster:

- A cert-manager leaf certificate signed by the local CA, a kgateway HTTPS Gateway and
  HTTPRoute, and a blue/green Argo Rollout serving the expected body through TLS validation.
- A CNPG PostgreSQL instance that creates a table, commits a row and reads it back.
- Authenticated Redis replication with three Sentinels; the probe discovers the primary
  through Sentinel and writes/reads a value through that address.
- An OpenBao tenant and OpenBao 2.7.2 Development-profile cluster with target-bound controller JWTs,
  self-initialization, operator-managed TLS and an initialized, unsealed health check.

These fixtures live in the test suite, not application deployment overlays. They prove
operator integration, not application correctness, database disaster recovery, production
hardening, mesh authorization or Redis failover (the separate Aspire Sentinel suite tests
failover). OpenBao's Development profile is for this disposable test only. The operator's
API remains pre-GA; do not treat this local proof as production qualification.

Sources: [kgateway Helm](https://kgateway.dev/docs/envoy/latest/install/helm/),
[cert-manager Helm](https://cert-manager.io/docs/installation/helm/),
[CNPG charts](https://github.com/cloudnative-pg/charts),
[Redis Operator](https://github.com/OT-CONTAINER-KIT/redis-operator),
[OpenBao Operator](https://docs.kubebao.org/docs/get-started/).
