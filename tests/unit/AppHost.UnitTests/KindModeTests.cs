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
    [InlineData("full", 13)]
    [InlineData("sandbox", 8)]
    public void ModeControlsPlatform(string mode, int expectedCharts)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--TECK_KIND_MODE=" + mode, "--Kind:Identity=false"],
            DisableDashboard = true,
        });
        builder.AddDeploymentCluster();
        builder.Resources.OfType<KindClusterResource>().Count().ShouldBe(1);
        var ambientOperator = builder.Resources.OfType<K8sManifestResource>().Any(manifest => string.Equals(manifest.Name, "keycloak-operator-ambient", StringComparison.Ordinal));
        ambientOperator.ShouldBe(string.Equals(mode, "full", StringComparison.Ordinal));
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

    /// <summary>The ordinary KIND profile includes the themed identity overlay and Hostzero operator.</summary>
    /// <exception cref="InvalidOperationException">The repository root is unavailable.</exception>
    [Fact]
    public void DefaultKindProfileIncludesIdentity()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Directory.Packages.props")))
        {
            repository = repository.Parent;
        }

        var root = repository?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            ProjectDirectory = Path.Combine(root, "src", "aspire", "Teck.Cloud.AppHost"),
            Args = ["--Profile=kind"],
            DisableDashboard = true,
        });
        builder.AddDeploymentCluster();
        builder.Resources.OfType<KindHelmChartResource>().Select(chart => chart.Name).ShouldContain("keycloak-operator", StringComparer.Ordinal);
        builder.Resources.OfType<K8sManifestResource>().Select(manifest => manifest.Name).ShouldContain("identity", StringComparer.Ordinal);
        builder.Resources.OfType<K8sManifestResource>().Select(manifest => manifest.Name).ShouldContain("identity-ready", StringComparer.Ordinal);
        builder.Resources.OfType<ParameterResource>().Single(parameter => string.Equals(parameter.Name, "kind-keycloak-admin-password", StringComparison.Ordinal)).Secret.ShouldBeTrue();
    }

    /// <summary>The identity port mapping preserves KIND's one default control-plane node.</summary>
    [Fact]
    public void IdentityPortMappingKeepsOneNode()
    {
        var config = new KindConfigModel();
        config.Nodes.Add(new KindNodeModel { Role = "control-plane" });
        KindHostingExtensions.ConfigureIdentityPortMapping(config);
        config.Nodes.Count.ShouldBe(1);
        var mapping = config.Nodes.Single().ExtraPortMappings.ShouldNotBeNull().Single();
        mapping.ContainerPort.ShouldBe(30443);
        mapping.HostPort.ShouldBe(18443);
        mapping.ListenAddress.ShouldBe("127.0.0.1");
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
