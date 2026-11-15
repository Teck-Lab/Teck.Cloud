using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Kind.E2ETests;

/// <summary>Tests the actual Aspire KIND lifecycle and manifest application.</summary>
public sealed class ClusterTests
{
    /// <summary>A disposable cluster schedules a pod with working DNS and is deleted on disposal.</summary>
    /// <returns>The verification task.</returns>
    /// <exception cref="InvalidOperationException">The requested cluster mode is invalid.</exception>
    [Fact]
    public async Task ClusterRunsManifestAndCleansUp()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(35));
        var name = "teck-cloud-test-" + Guid.NewGuid().ToString("N")[..10];
        var manifests = Path.Combine(AppContext.BaseDirectory, "Manifests");
        var mode = Environment.GetEnvironmentVariable("TECK_KIND_MODE");
        mode = string.IsNullOrEmpty(mode) ? "full" : mode;
        string[] expectedCharts = mode switch
        {
            "full" => ["argo-rollouts", "cert-manager", "cilium", "cnpg", "istio-base", "istio-cni", "istiod", "keycloak-operator", "kgateway", "kgateway-crds", "openbao-operator", "redis-operator", "ztunnel"],
            "sandbox" => ["argo-rollouts", "cert-manager", "cnpg", "keycloak-operator", "kgateway", "kgateway-crds", "openbao-operator", "redis-operator"],
            _ => throw new InvalidOperationException("TECK_KIND_MODE must be full or sandbox."),
        };
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Teck_Cloud_AppHost>(
            ["--Profile=kind", "--Kind:IdentityBuild=false", "--TECK_KIND_MODE=" + mode, "--Kind:Name=" + name, "--Kind:Manifests=" + manifests], deadline.Token).ConfigureAwait(true);
        await using (var app = await builder.BuildAsync(deadline.Token).ConfigureAwait(true))
        {
            builder.Resources.OfType<KindHelmChartResource>().Select(chart => chart.Name)
                .Order(StringComparer.Ordinal).ShouldBe(expectedCharts, StringComparer.Ordinal);
            await app.StartAsync(deadline.Token).ConfigureAwait(true);
            var candidateState = await app.ResourceNotifications.WaitForResourceAsync("candidate", [KnownResourceStates.Running, KnownResourceStates.FailedToStart], deadline.Token).ConfigureAwait(true);
            candidateState.ShouldBe(KnownResourceStates.Running);
            var cluster = app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<KindClusterResource>().Single();
            _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "wait", "--for=jsonpath={.status.active}=1", "job/cluster-smoke", "-n", "teck-smoke", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
            _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "wait", "--for=condition=Ready", "pod", "-l", "job-name=cluster-smoke", "-n", "teck-smoke", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
            if (string.Equals(mode, "full", StringComparison.Ordinal))
            {
                _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "rollout", "status", "daemonset/cilium", "-n", "kube-system", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
                _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "rollout", "status", "daemonset/ztunnel", "-n", "istio-system", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
                var pods = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "get", "pods", "-n", "teck-smoke", "-o", "json"], deadline.Token).ConfigureAwait(true);
                using var document = JsonDocument.Parse(pods);
                var pod = document.RootElement.GetProperty("items").EnumerateArray().Single();
                pod.GetProperty("metadata").GetProperty("annotations").GetProperty("ambient.istio.io/redirection").GetString().ShouldBe("enabled");
            }
            else
            {
                var namespaces = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "get", "namespaces", "-o", "name"], deadline.Token).ConfigureAwait(true);
                namespaces.ShouldNotContain("istio-system");
                _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "rollout", "status", "daemonset/kindnet", "-n", "kube-system", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
            }

            _ = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "wait", "--for=condition=complete", "job/cluster-smoke", "-n", "teck-smoke", "--timeout=120s"], deadline.Token).ConfigureAwait(true);
            var logs = await RunAsync("kubectl", ["--kubeconfig", cluster.KubeconfigPath, "logs", "job/cluster-smoke", "-n", "teck-smoke"], deadline.Token).ConfigureAwait(true);
            logs.ShouldContain("teck-kind-ready");
            await PlatformProof.VerifyAsync(cluster.KubeconfigPath, mode, deadline.Token).ConfigureAwait(true);
            await IdentityDeploymentProof.VerifyAsync(cluster.KubeconfigPath, mode, deadline.Token).ConfigureAwait(true);
            await KindProtectedPathProof.VerifyAsync(name, cluster.KubeconfigPath, mode, deadline.Token).ConfigureAwait(true);
            await app.StopAsync(deadline.Token).ConfigureAwait(true);
        }

        using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        cleanup.CancelAfter(TimeSpan.FromSeconds(30));
        while ((await RunAsync("kind", ["get", "clusters"], cleanup.Token).ConfigureAwait(true)).Split('\n', StringSplitOptions.TrimEntries).Contains(name, StringComparer.Ordinal))
        {
            await Task.Delay(250, cleanup.Token).ConfigureAwait(true);
        }
    }

    internal static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        _ = process.Start();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0, await error.ConfigureAwait(true));
            return await output.ConfigureAwait(true);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
