using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Shouldly;
using Teck.Cloud.AppHost;
using Xunit;

namespace AppHost.UnitTests;

/// <summary>Checks profile composition without starting a container engine.</summary>
public sealed class KindModeTests
{
    /// <summary>Only the full mode installs Cilium and Istio.</summary>
    /// <param name="mode">The explicit environment/configuration value.</param>
    /// <param name="expectedCharts">The expected platform chart count.</param>
    [Theory]
    [InlineData("full", 12)]
    [InlineData("sandbox", 7)]
    public void ModeControlsPlatform(string mode, int expectedCharts)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--TECK_KIND_MODE=" + mode],
            DisableDashboard = true,
        });
        builder.AddDeploymentCluster();
        builder.Resources.OfType<KindClusterResource>().Count().ShouldBe(1);
        var charts = builder.Resources.OfType<KindHelmChartResource>().ToArray();
        charts.Length.ShouldBe(expectedCharts);
        charts.Select(chart => chart.Name).ShouldContain("cert-manager", StringComparer.Ordinal);
        charts.Select(chart => chart.Name).ShouldContain("argo-rollouts", StringComparer.Ordinal);
        charts.Select(chart => chart.Name).ShouldNotContain("argocd", StringComparer.Ordinal);
        charts.Select(chart => chart.Name).ShouldNotContain("kargo", StringComparer.Ordinal);
        if (string.Equals(mode, "full", StringComparison.Ordinal))
        {
            var cilium = charts.Single(chart => string.Equals(chart.Name, "cilium", StringComparison.Ordinal));
            cilium.Values["cni.exclusive"].ShouldBe("false");
            cilium.Values["bpf.masquerade"].ShouldBe("false");
        }
    }

    /// <summary>Absent configuration means full; only an explicit sandbox value reduces it.</summary>
    /// <param name="value">The supplied setting.</param>
    /// <param name="sandbox">Whether reduced mode is expected.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("full", false)]
    [InlineData("sandbox", true)]
    public void ModeDefaultsToFull(string? value, bool sandbox) => KindHostingExtensions.IsSandboxMode(value).ShouldBe(sandbox);

    /// <summary>A typo must not silently disable the mesh or networking checks.</summary>
    [Fact]
    public void UnknownModeFails() => Should.Throw<InvalidOperationException>(() => KindHostingExtensions.IsSandboxMode("sandbbox"));
}
