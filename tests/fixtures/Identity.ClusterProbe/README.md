# In-cluster protected-path probe

This is an acceptance client, never an application deployment. The KIND suite
loads this image and the existing `Identity.TestHost` image into its owned cluster.
It applies the test manifests in `Kind.E2ETests/ProtectedPathManifests` after the
canonical identity overlay and platform operators are healthy.

The Job provisions disposable organizations/users through the same linked
`IdentityProvisioner` source used by Aspire E2E, then uses headless Chromium to
complete the real authorization-code/PKCE BFF flow. It verifies tenant-specific
writes/reads through native token exchange, UMA, actual gRPC and tenant-filtered
EF contexts; denied writes, forged tenant headers and organization disablement
must fail or remain isolated. The suite separately reads CNPG to verify exactly
two retained rows from distinct tenant UUIDs. Full mode also applies STRICT mesh
mTLS and permits only the gateway service account to call the gRPC service.

Traffic stays inside the cluster through kgateway HTTPS Services, with canonical
public DNS names mapped to their owned Service IPs. The cluster CA is trusted by
.NET, Node and Chromium's NSS store. There is no port forwarding and no certificate
validation bypass. Admin API and service backchannels use the private Keycloak
Service; the public issuer stays `https://localhost:18443/realms/teck-local`.
The normal identity HTTPRoute still excludes `/admin`.

All credentials are generated for the owned cluster and supplied through Secrets.
The probe writes only a fixed success marker. Its fixture-only bootstrap admin
credential is never given to the gateway/service, which use the canonical
read-only tenant reader. Cleanup is the owning KIND test's cluster disposal.

Build through `bun nx run Identity.ClusterProbe:container:build`; the Kubernetes
suite declares this image and `Identity.TestHost` as dependencies. This fixture
proves shared infrastructure, not nonexistent production business services.
