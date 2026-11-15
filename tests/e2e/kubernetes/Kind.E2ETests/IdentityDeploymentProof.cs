using System.Text.Json;
using Shouldly;

namespace Kind.E2ETests;

/// <summary>Deploys the canonical identity overlay and verifies its real TLS gateway.</summary>
internal static class IdentityDeploymentProof
{
    internal static async Task VerifyAsync(string kubeconfig, string mode, CancellationToken cancellationToken)
    {
        _ = await KubectlAsync(["wait", "--for=condition=Ready", "cluster/identity-db", "-n", "identity", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["rollout", "status", "deployment/keycloak", "-n", "identity", "--timeout=450s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Ready", "keycloakinstance/local", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Ready", "keycloakrealm/local", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Programmed", "gateway/identity", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        var gatewayJson = await KubectlAsync(["get", "services", "-n", "identity", "-l", "gateway.networking.k8s.io/gateway-name=identity", "-o", "json"]).ConfigureAwait(false);
        using var gateway = JsonDocument.Parse(gatewayJson);
        var address = gateway.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("spec").GetProperty("clusterIP").GetString();
        _ = address.ShouldNotBeNull();

        // KIND has no cloud LoadBalancer address; use the labeled gateway Service in-cluster.
        Uri.CheckHostName(address).ShouldNotBe(UriHostNameType.Unknown);
        var probe = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "IdentityManifests", "probe.yaml"), cancellationToken).ConfigureAwait(false);
        await ApplyAsync(probe.Replace("__GATEWAY_ADDRESS__", address, StringComparison.Ordinal)).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Complete", "job/identity-probe", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        var logs = await KubectlAsync(["logs", "job/identity-probe", "-n", "identity"]).ConfigureAwait(false);
        logs.ShouldContain("teck-identity-tls-ready");
        if (string.Equals(mode, "full", StringComparison.Ordinal))
        {
            _ = await KubectlAsync(["apply", "-f", Path.Combine(AppContext.BaseDirectory, "IdentityManifests", "unmeshed.yaml")]).ConfigureAwait(false);
            _ = await KubectlAsync(["wait", "--for=condition=Complete", "job/plaintext-rejected", "-n", "identity-unmeshed", "--timeout=90s"]).ConfigureAwait(false);
            var rejected = await KubectlAsync(["logs", "job/plaintext-rejected", "-n", "identity-unmeshed"]).ConfigureAwait(false);
            rejected.ShouldContain("teck-plaintext-rejected");
        }

        Task<string> KubectlAsync(string[] arguments)
        {
            return ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, .. arguments], cancellationToken);
        }

        async Task ApplyAsync(string yaml)
        {
            var temporary = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(temporary, yaml, cancellationToken).ConfigureAwait(false);
                _ = await KubectlAsync(["apply", "-f", temporary]).ConfigureAwait(false);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
