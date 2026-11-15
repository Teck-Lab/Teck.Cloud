namespace Teck.Cloud.AppHost;

/// <summary>Separates identity prerequisites from readiness of the whole local platform.</summary>
/// <param name="Certificates">The disposable local certificate issuer.</param>
/// <param name="Ready">All required platform controllers are ready.</param>
/// <param name="Gateway">The kgateway controller.</param>
/// <param name="Postgres">The CNPG controller.</param>
/// <param name="KeycloakOperator">The Hostzero Keycloak controller.</param>
internal sealed record KindPlatformResources(
    IResourceBuilder<K8sManifestResource> Certificates,
    IResourceBuilder<K8sManifestResource> Ready,
    IResourceBuilder<KindHelmChartResource> Gateway,
    IResourceBuilder<KindHelmChartResource> Postgres,
    IResourceBuilder<KindHelmChartResource> KeycloakOperator);
