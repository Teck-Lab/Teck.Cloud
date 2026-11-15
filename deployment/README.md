# Application deployment manifests

base/ contains reusable application resources. local/, development/, staging/ and
production/ are overlays by environment, never by individual cluster. GitOps references
a versioned overlay and supplies cluster-specific configuration.

Keep application images and manifests tied to the same candidate/release. KIND
tests deploy these manifests with candidate images. Terraform/server provisioning
and shared platform installation belong elsewhere.

The first application manifests are base/identity resources and the local/identity
overlay. Other applications receive manifests when their actual hosts exist. Local
overrides must preserve the behavior the tests claim to verify.

## Local identity

The canonical Keycloak resources live in base/identity; local/identity adds the
session namespace, a CNPG database, Hostzero KeycloakInstance and KeycloakRealm,
cert-manager certificate and kgateway HTTPS route.
The candidate image is built by keycloak-theme:container:build and identified by the
shared Aspire Identity/runtime.json descriptor. KIND loads that exact candidate into
its owned node before applying this overlay. This is a single-instance local topology,
not a production HA or backup policy.

Environment composition supplies identity-realm from the shared realm file and an
identity-bootstrap Secret with generated bootstrap/admin-gateway/customer-gateway
credentials. No checked-in credential or separate hand-maintained realm is used.
CNPG supplies the database application Secret. Shared realm changes affect new imports;
Keycloak does not overwrite an existing realm on restart. Hostzero manages the
imported realm state through the Admin API; the shared import remains the source for
initial clients and authentication flows. Real environments need an
explicit, reviewed migration process rather than assuming import updates existing data.

The local issuer is https://localhost:18443. The loopback-only
Gateway route covers OIDC and the administration UI at /admin/. Keycloak management
health endpoints remain internal to its pod.
Tests trust only the generated local CA and resolve the host to the controller-reported
gateway Service address. They do not disable certificate checks or install trust globally.
Full-mode acceptance enrolls the namespace in ambient and requires STRICT mTLS for
Keycloak with an explicit local namespace allow policy. The separate ambient.yaml
is applied only in full mode; sandbox mode never loads unsupported mesh CRDs. Sandbox-mode acceptance uses the explicit isolated development network;
it makes no ambient protection claim. Production must supply its own hostname, TLS,
workload authorization, image digest, durable database settings and secret lifecycle.

The identity KIND proof currently covers the deployed identity provider and its TLS
boundary. Gateway-to-business-service acceptance is a separate test; controller readiness
or rendering this overlay does not prove the complete application journey.
