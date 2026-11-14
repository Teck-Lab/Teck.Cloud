using System.Text.Json;
using Shouldly;

namespace Kind.E2ETests;

/// <summary>Proves operators reconcile real resources and traffic reaches a Rollout through TLS.</summary>
internal static class PlatformProof
{
    internal static async Task VerifyAsync(string kubeconfig, string mode, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "PlatformManifests");
        var temporary = Path.Combine(Path.GetTempPath(), "teck-platform-proof-" + Guid.NewGuid().ToString("N") + ".yaml");
        try
        {
            var fixture = await File.ReadAllTextAsync(Path.Combine(directory, "workloads.yaml"), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(temporary, fixture.Replace("GENERATED_TEST_PASSWORD", Guid.NewGuid().ToString("N"), StringComparison.Ordinal), cancellationToken).ConfigureAwait(false);
            _ = await KubectlAsync(["apply", "--server-side", "-f", temporary]).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(temporary);
        }

        _ = await KubectlAsync(["wait", "--for=condition=Ready", "certificate/gateway", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=jsonpath={.status.phase}=Healthy", "rollout/application", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Ready", "cluster.postgresql.cnpg.io/postgres", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["exec", "-n", "platform-proof", "postgres-1", "-c", "postgres", "--", "psql", "-U", "postgres", "-d", "app", "-Atc", "CREATE TABLE proof (value text); INSERT INTO proof VALUES ('cnpg-write-read');"]).ConfigureAwait(false);
        var sql = await KubectlAsync(["exec", "-n", "platform-proof", "postgres-1", "-c", "postgres", "--", "psql", "-U", "postgres", "-d", "app", "-Atc", "SELECT value FROM proof;"]).ConfigureAwait(false);
        sql.ShouldContain("cnpg-write-read", Case.Sensitive);
        _ = await KubectlAsync(["wait", "--for=create", "statefulset/redis-sentinel", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["rollout", "status", "statefulset/redis-sentinel", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["rollout", "status", "statefulset/redis", "-n", "platform-proof", "--timeout=300s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["apply", "-f", Path.Combine(directory, "probe.yaml")]).ConfigureAwait(false);
        await WaitForProbeAsync(kubeconfig, cancellationToken).ConfigureAwait(false);
        var gateway = await KubectlAsync(["logs", "job/platform-probe", "-n", "platform-proof", "-c", "tls"]).ConfigureAwait(false);
        gateway.ShouldContain("teck-gateway-ready", Case.Sensitive);
        var redis = await KubectlAsync(["logs", "job/platform-probe", "-n", "platform-proof", "-c", "redis"]).ConfigureAwait(false);
        redis.ShouldContain("teck-sentinel-ready", Case.Sensitive);

        _ = await KubectlAsync(["wait", "--for=condition=Provisioned", "openbaotenant/proof", "-n", "openbao-operator-system", "--timeout=300s"]).ConfigureAwait(false);
        var endpoint = await KubectlAsync(["get", "endpointslice", "-n", "default", "-l", "kubernetes.io/service-name=kubernetes", "-o", "jsonpath={.items[0].endpoints[0].addresses[0]}"]).ConfigureAwait(false);
        System.Net.IPAddress.TryParse(endpoint, out _).ShouldBeTrue();
        var baoFixture = await File.ReadAllTextAsync(Path.Combine(directory, "openbao.yaml"), cancellationToken).ConfigureAwait(false);
        baoFixture = baoFixture.Replace("KIND_API_ENDPOINT", endpoint, StringComparison.Ordinal);
        if (string.Equals(mode, "full", StringComparison.Ordinal))
        {
            baoFixture = baoFixture.Replace("  network:", "  network:\n    apiServerEgress:\n      mode: Cilium\n      port: 6443", StringComparison.Ordinal);
        }

        try
        {
            await File.WriteAllTextAsync(temporary, baoFixture, cancellationToken).ConfigureAwait(false);
            _ = await KubectlAsync(["apply", "-f", temporary]).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(temporary);
        }

        _ = await KubectlAsync(["wait", "--for=condition=Available", "openbaocluster/bao", "-n", "openbao-proof", "--timeout=600s"]).ConfigureAwait(false);
        var bao = await KubectlAsync(["exec", "-n", "openbao-proof", "bao-0", "-c", "openbao", "--", "bao", "status", "-address=https://127.0.0.1:8200", "-ca-cert=/etc/bao/tls/ca.crt", "-tls-server-name=openbao-cluster-bao.local", "-format=json"]).ConfigureAwait(false);
        using var health = JsonDocument.Parse(bao);
        health.RootElement.GetProperty("initialized").GetBoolean().ShouldBeTrue();
        health.RootElement.GetProperty("sealed").GetBoolean().ShouldBeFalse();

        Task<string> KubectlAsync(string[] arguments)
        {
            return ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, .. arguments], cancellationToken);
        }
    }

    private static async Task WaitForProbeAsync(string kubeconfig, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(150));
        while (true)
        {
            var json = await ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, "get", "job/platform-probe", "-n", "platform-proof", "-o", "json"], deadline.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.GetProperty("status").TryGetProperty("conditions", out var conditions))
            {
                foreach (var condition in conditions.EnumerateArray())
                {
                    if (!string.Equals(condition.GetProperty("status").GetString(), "True", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var type = condition.GetProperty("type").GetString();
                    if (string.Equals(type, "Complete", StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (string.Equals(type, "Failed", StringComparison.Ordinal))
                    {
                        var logs = await ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, "logs", "job/platform-probe", "-n", "platform-proof", "--all-containers=true"], deadline.Token).ConfigureAwait(false);
                        throw new InvalidOperationException("Platform probe failed: " + logs);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token).ConfigureAwait(false);
        }
    }
}
