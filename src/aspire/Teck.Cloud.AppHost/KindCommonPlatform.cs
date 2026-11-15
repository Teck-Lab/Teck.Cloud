namespace Teck.Cloud.AppHost;

/// <summary>Installs the same application-facing APIs and operators in both KIND modes.</summary>
internal static class KindCommonPlatform
{
    internal static KindPlatformResources Add(
        IResourceBuilder<KindClusterResource> cluster,
        string directory,
        IResourceBuilder<K8sManifestResource> gatewayApi,
        IResourceBuilder<KindHelmChartResource>? network)
    {
        var certificates = Chart("cert-manager", "oci://quay.io/jetstack/charts/cert-manager", "v1.21.3", "cert-manager")
            .WithHelmValue("crds.enabled", "true");
        var gatewayCrds = Chart("kgateway-crds", "oci://cr.kgateway.dev/kgateway-dev/charts/kgateway-crds", "v2.5.2", "kgateway-system");
        _ = KindHostingExtensions.AfterDeployment(gatewayCrds, gatewayApi);
        var gateway = Chart("kgateway", "oci://cr.kgateway.dev/kgateway-dev/charts/kgateway", "v2.5.2", "kgateway-system");
        _ = KindHostingExtensions.AfterDeployment(gateway, gatewayCrds);
        var postgres = Chart("cnpg", "oci://ghcr.io/cloudnative-pg/charts/cloudnative-pg", "0.29.1", "cnpg-system")
            .WithHelmValue("fullnameOverride", "cnpg");
        var redis = Chart("redis-operator", "oci://ghcr.io/ot-container-kit/charts/redis-operator", "0.27.0", "redis-operator");
        var bao = Chart("openbao-operator", "oci://ghcr.io/kubebao/charts/openbao-operator", "0.6.1", "openbao-operator-system");
        IResourceBuilder<K8sManifestResource>? ambientOperatorNamespace = null;
        if (network is not null)
        {
            ambientOperatorNamespace = cluster.AddManifestFromContent(
                "keycloak-operator-ambient",
                "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: keycloak-operator\n  labels:\n    istio.io/dataplane-mode: ambient\n").WithServerSideApply();
            _ = KindHostingExtensions.AfterDeployment(ambientOperatorNamespace, network, "daemonset/ztunnel");
        }

        var keycloakOperator = Chart("keycloak-operator", "oci://ghcr.io/hostzero-gmbh/charts/keycloak-operator", "0.14.0", "keycloak-operator");
        if (ambientOperatorNamespace is not null)
        {
            _ = KindHostingExtensions.AfterDeployment(keycloakOperator, ambientOperatorNamespace);
        }

        var rollouts = Chart("argo-rollouts", "oci://ghcr.io/argoproj/argo-helm/argo-rollouts", "2.43.12", "argo-rollouts");

        // Certificates can be issued as soon as cert-manager is ready.
        var defaults = cluster.AddManifest("platform-defaults", Path.Combine(directory, "Kubernetes", "certificates")).WithServerSideApply();
        _ = KindHostingExtensions.AfterDeployment(defaults, certificates, "deployment/cert-manager", "deployment/cert-manager-webhook", "deployment/cert-manager-cainjector");

        // Candidate manifests still require every controller, while identity needs only its own prerequisites.
        var ready = cluster.AddManifestFromContent(
            "platform-ready",
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: teck-platform-ready\ndata:\n  owner: aspire\n").WithServerSideApply();
        _ = KindHostingExtensions.AfterDeployment(ready, defaults);
        _ = KindHostingExtensions.AfterDeployment(ready, gateway, "deployment/kgateway");
        _ = KindHostingExtensions.AfterDeployment(ready, postgres, "deployment/cnpg");
        _ = KindHostingExtensions.AfterDeployment(ready, redis, "deployment/redis-operator");
        _ = KindHostingExtensions.AfterDeployment(ready, bao, "deployment/openbao-operator-controller", "deployment/openbao-operator-provisioner");
        _ = KindHostingExtensions.AfterDeployment(ready, keycloakOperator, "deployment/keycloak-operator");
        _ = KindHostingExtensions.AfterDeployment(ready, rollouts, "deployment/argo-rollouts");
        return new KindPlatformResources(defaults, ready, gateway, postgres, keycloakOperator);

        IResourceBuilder<KindHelmChartResource> Chart(string name, string chart, string version, string ns)
        {
            return cluster.AddHelmChart(name, chart).WithChartVersion(version).WithNamespace(ns);
        }
    }
}
