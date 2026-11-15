using System.Text.Json;
using Shouldly;

namespace Kind.E2ETests;

/// <summary>Deploys actual protected fixture processes and exercises them entirely inside the owned cluster.</summary>
internal static class KindProtectedPathProof
{
    internal static async Task VerifyAsync(string clusterName, string kubeconfig, string mode, CancellationToken cancellationToken)
    {
        foreach (var image in new[] { "localhost/teck/identity-probe:local", "localhost/teck/identity-cluster-probe:local" })
        {
            await Teck.Cloud.AppHost.KindImageLoader.LoadAsync(image, clusterName, cancellationToken).ConfigureAwait(false);
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "ProtectedPathManifests");
        var redis = await KubectlAsync(["get", "secret/redis-password", "-n", "platform-proof", "-o", "jsonpath={.data.password}"]).ConfigureAwait(false);
        _ = Convert.FromBase64String(redis);
        await ApplyAsync("apiVersion: v1\nkind: Secret\nmetadata:\n  name: identity-path-secrets\n  namespace: identity\ndata:\n  redis-password: " + redis + "\n").ConfigureAwait(false);
        var foundation = await File.ReadAllTextAsync(Path.Combine(directory, "foundation.yaml"), cancellationToken).ConfigureAwait(false);
        await ApplyAsync(foundation.Replace("__IDENTITY_TRANSPORT__", string.Equals(mode, "full", StringComparison.Ordinal) ? "AmbientMesh" : "IsolatedDevelopment", StringComparison.Ordinal)).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Ready", "cluster/identity-records", "-n", "identity", "--timeout=300s"]).ConfigureAwait(false);
        if (string.Equals(mode, "full", StringComparison.Ordinal))
        {
            _ = await KubectlAsync(["apply", "-f", Path.Combine(directory, "ambient.yaml")]).ConfigureAwait(false);
        }

        var identityAddress = Address(await KubectlAsync(["get", "services", "-n", "identity", "-l", "gateway.networking.k8s.io/gateway-name=identity", "-o", "json"]).ConfigureAwait(false));
        var applications = await File.ReadAllTextAsync(Path.Combine(directory, "applications.yaml"), cancellationToken).ConfigureAwait(false);
        await ApplyAsync(applications.Replace("__IDENTITY_GATEWAY_ADDRESS__", identityAddress, StringComparison.Ordinal)).ConfigureAwait(false);
        foreach (var deployment in new[] { "identity-gateway", "identity-service" })
        {
            _ = await KubectlAsync(["rollout", "status", "deployment/" + deployment, "-n", "identity", "--timeout=240s"]).ConfigureAwait(false);
        }

        _ = await KubectlAsync(["wait", "--for=condition=Ready", "certificate/identity-application", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["wait", "--for=condition=Programmed", "gateway/identity-application", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        _ = await KubectlAsync(["rollout", "status", "deployment/identity-application", "-n", "identity", "--timeout=180s"]).ConfigureAwait(false);
        var applicationAddress = Address(await KubectlAsync(["get", "services", "-n", "identity", "-l", "gateway.networking.k8s.io/gateway-name=identity-application", "-o", "json"]).ConfigureAwait(false));
        var job = await File.ReadAllTextAsync(Path.Combine(directory, "probe.yaml"), cancellationToken).ConfigureAwait(false);
        await ApplyAsync(job.Replace("__IDENTITY_GATEWAY_ADDRESS__", identityAddress, StringComparison.Ordinal).Replace("__APPLICATION_GATEWAY_ADDRESS__", applicationAddress, StringComparison.Ordinal)).ConfigureAwait(false);
        await WaitForProofAsync(kubeconfig, cancellationToken).ConfigureAwait(false);
        var rows = await KubectlAsync(["exec", "-n", "identity", "identity-records-1", "-c", "postgres", "--", "psql", "-U", "postgres", "-d", "identityrecords", "-Atc", "SELECT count(*) = 2 AND count(DISTINCT \"TenantId\") = 2 AND bool_and(\"Value\" IN ('alpha-value','beta-value')) FROM identity_records;"]).ConfigureAwait(false);
        rows.Trim().ShouldBe("t");

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

    private static string Address(string json)
    {
        using var document = JsonDocument.Parse(json);
        var address = document.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("spec").GetProperty("clusterIP").GetString();
        _ = address.ShouldNotBeNull();
        Uri.CheckHostName(address).ShouldNotBe(UriHostNameType.Unknown);
        return address;
    }

    private static async Task WaitForProofAsync(string kubeconfig, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(7));
        while (true)
        {
            var state = await ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, "get", "job/identity-protected-path", "-n", "identity", "-o", "json"], deadline.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(state);
            if (document.RootElement.GetProperty("status").TryGetProperty("conditions", out var conditions))
            {
                foreach (var condition in conditions.EnumerateArray())
                {
                    if (!string.Equals(condition.GetProperty("status").GetString(), "True", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var type = condition.GetProperty("type").GetString();
                    if (type is not ("Complete" or "Failed"))
                    {
                        continue;
                    }

                    var logs = await ClusterTests.RunAsync("kubectl", ["--kubeconfig", kubeconfig, "logs", "job/identity-protected-path", "-n", "identity"], deadline.Token).ConfigureAwait(false);
                    type.ShouldBe("Complete", logs);
                    logs.ShouldContain("teck-kind-protected-path-ready", Case.Sensitive);
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token).ConfigureAwait(false);
        }
    }
}
