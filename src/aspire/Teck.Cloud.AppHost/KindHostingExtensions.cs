using Microsoft.Extensions.DependencyInjection;

namespace Teck.Cloud.AppHost;

/// <summary>Uses the upstream Aspire integration to own a disposable local cluster.</summary>
internal static class KindHostingExtensions
{
    internal static bool IsSandboxMode(string? value) => value switch
    {
        null or "" or "full" => false,
        "sandbox" => true,
        _ => throw new InvalidOperationException("TECK_KIND_MODE must be full or sandbox."),
    };

    /// <summary>Maps the local identity gateway on KIND's existing control-plane node.</summary>
    /// <param name="config">The cluster configuration with its default node.</param>
    internal static void ConfigureIdentityPortMapping(KindConfigModel config)
    {
        var controlPlane = config.Nodes.Single(node => string.Equals(node.Role, "control-plane", StringComparison.Ordinal));
        controlPlane.ExtraPortMappings ??= [];
        controlPlane.ExtraPortMappings.Add(new KindPortMappingModel
        {
            ContainerPort = 30443,
            HostPort = 18443,
            ListenAddress = "127.0.0.1",
        });
    }

    /// <summary>Adds a session cluster and, when supplied, the actual candidate manifest overlay.</summary>
    /// <param name="builder">The application builder.</param>
    /// <exception cref="DirectoryNotFoundException">The requested manifest directory is missing.</exception>
    /// <exception cref="InvalidOperationException">The cluster mode is invalid.</exception>
    internal static void AddDeploymentCluster(this IDistributedApplicationBuilder builder)
    {
        var sandbox = IsSandboxMode(builder.Configuration["TECK_KIND_MODE"]);
        var name = builder.Configuration["Kind:Name"] ?? ("teck-cloud-dev-" + Guid.NewGuid().ToString("N")[..10]);
        var identity = builder.Configuration["Kind:Identity"] switch
        {
            null or "" or "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException("Kind:Identity must be true or false."),
        };
        var runtime = builder.Configuration["ASPIRE_CONTAINER_RUNTIME"] ?? "docker";
        var kindProvider = Environment.GetEnvironmentVariable("KIND_EXPERIMENTAL_PROVIDER");
        if (string.Equals(runtime, "podman", StringComparison.Ordinal) && string.IsNullOrEmpty(kindProvider))
        {
            Environment.SetEnvironmentVariable("KIND_EXPERIMENTAL_PROVIDER", "podman");
        }
        else if (!string.IsNullOrEmpty(kindProvider) && !string.Equals(kindProvider, runtime, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("KIND_EXPERIMENTAL_PROVIDER must match ASPIRE_CONTAINER_RUNTIME.");
        }

        var cluster = builder.AddKindCluster(name)
            .WithNodeImage("kindest/node:v1.37.0@sha256:a1ed56cfb0e7b93589bdf97c8cd566405a265939e3620fc4f5de89adff580ae5")
            .WithClusterLifetime(ClusterLifetime.Session);
        if (identity)
        {
            _ = cluster.WithKindConfig(ConfigureIdentityPortMapping);
        }

        var gatewayApi = cluster.AddManifest("gateway-api", Path.Combine(builder.AppHostDirectory, "Kubernetes", "gateway-api")).WithServerSideApply();
        var network = sandbox ? null : AddFullPlatform(cluster, gatewayApi);
        var platform = KindCommonPlatform.Add(cluster, builder.AppHostDirectory, gatewayApi, network);
        var identityReady = identity ? KindIdentityHostingExtensions.Add(builder, cluster, platform, sandbox) : null;

        var manifests = builder.Configuration["Kind:Manifests"];
        if (!string.IsNullOrWhiteSpace(manifests))
        {
            var absolutePath = Path.GetFullPath(manifests, builder.AppHostDirectory);
            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException("The configured candidate manifest directory does not exist.");
            }

            var candidate = cluster.AddManifest("candidate", absolutePath).WithServerSideApply();
            _ = AfterDeployment(candidate, identityReady ?? platform);
        }
    }

    internal static IResourceBuilder<T> AfterDeployment<T, TDependency>(IResourceBuilder<T> resource, IResourceBuilder<TDependency> dependency, params string[] workloads)
        where T : IResource
        where TDependency : IResource
    {
        // Toolkit 13.6 rejects sibling WaitFor annotations under the same KIND parent.
        // Its chart/manifest installers publish this supported event before touching the API.
        return resource.OnBeforeResourceStarted(async (target, args, cancellationToken) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(25));
            var notifications = args.Services.GetRequiredService<ResourceNotificationService>();
            try
            {
                await WaitForPlatformAsync(notifications, dependency.Resource, workloads, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                await notifications.PublishUpdateAsync(target, snapshot => snapshot with { State = KnownResourceStates.FailedToStart }).ConfigureAwait(false);
                throw new TimeoutException($"KIND platform: {target.Name} could not start because {dependency.Resource.Name} was not ready. Inspect its resource logs.", exception);
            }
            catch (Exception)
            {
                await notifications.PublishUpdateAsync(target, snapshot => snapshot with { State = KnownResourceStates.FailedToStart }).ConfigureAwait(false);
                throw;
            }
        });
    }

    private static IResourceBuilder<KindHelmChartResource> AddFullPlatform(IResourceBuilder<KindClusterResource> cluster, IResourceBuilder<K8sManifestResource> gatewayApi)
    {
        _ = cluster.WithKindConfig(config =>
        {
            config.Networking ??= new KindNetworkingModel();
            config.Networking.DisableDefaultCNI = true;
        });
        var cilium = cluster.AddHelmChart("cilium", "oci://quay.io/cilium/charts/cilium")
            .WithChartVersion("1.20.4")
            .WithNamespace("kube-system")
            .WithHelmValue("operator.replicas", "1")
            .WithHelmValue("ipam.mode", "kubernetes")
            .WithHelmValue("kubeProxyReplacement", "false")
            .WithHelmValue("cni.exclusive", "false")
            .WithHelmValue("bpf.masquerade", "false");
        var istioBase = AfterDeployment(AddIstioChart(cluster, "istio-base", "base"), cilium, "daemonset/cilium", "daemonset/cilium-envoy", "deployment/cilium-operator");
        _ = AfterDeployment(istioBase, gatewayApi);
        var istiod = AfterDeployment(AddIstioChart(cluster, "istiod", "istiod").WithHelmValue("profile", "ambient"), istioBase);
        var cni = AfterDeployment(AddIstioChart(cluster, "istio-cni", "cni").WithHelmValue("profile", "ambient"), istiod, "deployment/istiod");
        return AfterDeployment(AddIstioChart(cluster, "ztunnel", "ztunnel"), cni, "daemonset/istio-cni-node");
    }

    private static IResourceBuilder<KindHelmChartResource> AddIstioChart(IResourceBuilder<KindClusterResource> cluster, string name, string chart)
    {
        const string version = "1.32.0";
        return cluster.AddHelmChart(name, $"oci://ghcr.io/istio/release/charts/{chart}")
            .WithChartVersion(version)
            .WithNamespace("istio-system");
    }

    private static async Task WaitForPlatformAsync(ResourceNotificationService notifications, IResource dependency, string[] workloads, CancellationToken cancellationToken)
    {
        var state = await notifications.WaitForResourceAsync(dependency.Name, [KnownResourceStates.Running, KnownResourceStates.FailedToStart], cancellationToken).ConfigureAwait(false);
        if (!string.Equals(state, KnownResourceStates.Running, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"KIND dependency {dependency.Name} failed; inspect its resource logs.");
        }

        // The upstream label-based health check considers zero matches healthy.
        // Named rollouts fail on missing objects and prove actual readiness.
        foreach (var workload in workloads)
        {
            await KindPlatformReadiness.WaitForRolloutAsync((KindHelmChartResource)dependency, workload, cancellationToken).ConfigureAwait(false);
        }
    }
}
