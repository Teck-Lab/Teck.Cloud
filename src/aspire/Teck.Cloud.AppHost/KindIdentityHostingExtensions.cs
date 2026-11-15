using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Teck.Cloud.AppHost;

/// <summary>Loads the themed Keycloak image and applies the canonical local identity overlay to KIND.</summary>
internal static class KindIdentityHostingExtensions
{
    internal static IResourceBuilder<K8sManifestResource> Add(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<KindClusterResource> cluster,
        KindPlatformResources platform,
        IResourceBuilder<KindHelmChartResource>? network,
        string runtime,
        bool sandbox)
    {
        var root = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".."));
        var overlay = Path.Combine(root, "deployment", "local", "identity");
        var buildImage = builder.Configuration["Kind:IdentityBuild"] switch
        {
            null or "" or "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException("Kind:IdentityBuild must be true or false."),
        };
        using var identityDescriptor = JsonDocument.Parse(File.ReadAllText(Path.Combine(builder.AppHostDirectory, "Identity", "runtime.json")));
        var image = identityDescriptor.RootElement.GetProperty("image").GetString() ?? throw new InvalidOperationException("The shared Keycloak image is required.");
        var adminPassword = builder.AddParameter("kind-keycloak-admin-password", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true, persist: true);
        var customerSecret = builder.AddParameter("kind-customer-gateway-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);
        var adminSecret = builder.AddParameter("kind-admin-gateway-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);
        var readerSecret = builder.AddParameter("kind-tenant-reader-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true);

        // Prepare the image and bootstrap inputs while the platform charts install.
        var prepared = cluster.AddManifestFromContent(
            "identity-prepared",
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: identity-prepared\n  namespace: identity\ndata:\n  owner: aspire\n").WithServerSideApply();
        _ = prepared.OnBeforeResourceStarted(async (_, _, cancellationToken) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(20));
            var kubeconfig = cluster.Resource.KubeconfigPath;
            if (buildImage)
            {
                await RunAsync("node", [Path.Combine(root, "src", "apps", "keycloak-theme", "image", "build.mjs"), "--if-changed"], root, input: null, deadline.Token, containerRuntime: runtime).ConfigureAwait(false);
            }

            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "apply", "-f", Path.Combine(overlay, "namespace.yaml")], root, input: null, deadline.Token).ConfigureAwait(false);
            await KindImageLoader.EnsureIdentityImageAsync(image, cluster.Resource.Name, kubeconfig, runtime, deadline.Token).ConfigureAwait(false);

            var realm = await File.ReadAllBytesAsync(Path.Combine(builder.AppHostDirectory, "Identity", "Realms", "teck-local-realm.json"), deadline.Token).ConfigureAwait(false);
            var bootstrap = await CreateBootstrapAsync(
                realm,
                adminPassword.Resource,
                customerSecret.Resource,
                adminSecret.Resource,
                readerSecret.Resource,
                deadline.Token).ConfigureAwait(false);
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "apply", "-f", "-"], root, bootstrap, deadline.Token).ConfigureAwait(false);
        });

        var resource = cluster.AddManifest("identity", overlay).WithServerSideApply();
        _ = KindHostingExtensions.AfterDeployment(resource, prepared);
        WaitForPrerequisites(resource, platform, network);
        if (!sandbox)
        {
            _ = resource.OnBeforeResourceStarted(async (_, _, cancellationToken) =>
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromMinutes(20));
                var kubeconfig = cluster.Resource.KubeconfigPath;
                await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "label", "namespace", "identity", "istio.io/dataplane-mode=ambient", "--overwrite"], root, input: null, deadline.Token).ConfigureAwait(false);
                await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "apply", "-f", Path.Combine(overlay, "ambient.yaml")], root, input: null, deadline.Token).ConfigureAwait(false);
            });
        }

        return AddReady(cluster, resource, root);
    }

    private static IResourceBuilder<K8sManifestResource> AddReady(
        IResourceBuilder<KindClusterResource> cluster,
        IResourceBuilder<K8sManifestResource> identity,
        string root)
    {
        var ready = cluster.AddManifestFromContent("identity-ready", "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: identity-ready\n  namespace: identity\ndata:\n  owner: aspire\n").WithServerSideApply();
        _ = KindHostingExtensions.AfterDeployment(ready, identity);
        _ = ready.OnBeforeResourceStarted(async (_, _, cancellationToken) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(25));
            var kubeconfig = cluster.Resource.KubeconfigPath;
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "wait", "--for=condition=Ready", "cluster/identity-db", "-n", "identity", "--timeout=300s"], root, input: null, deadline.Token).ConfigureAwait(false);
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "rollout", "status", "deployment/keycloak", "-n", "identity", "--timeout=450s"], root, input: null, deadline.Token).ConfigureAwait(false);
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "wait", "--for=condition=Ready", "keycloakinstance/local", "-n", "identity", "--timeout=180s"], root, input: null, deadline.Token).ConfigureAwait(false);
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "wait", "--for=condition=Ready", "keycloakrealm/local", "-n", "identity", "--timeout=180s"], root, input: null, deadline.Token).ConfigureAwait(false);
            await RunAsync("kubectl", ["--kubeconfig", kubeconfig, "wait", "--for=condition=Programmed", "gateway/identity", "-n", "identity", "--timeout=180s"], root, input: null, deadline.Token).ConfigureAwait(false);
        });
        return ready;
    }

    private static void WaitForPrerequisites(
        IResourceBuilder<K8sManifestResource> resource,
        KindPlatformResources platform,
        IResourceBuilder<KindHelmChartResource>? network)
    {
        _ = KindHostingExtensions.AfterDeployment(resource, platform.Certificates);
        _ = KindHostingExtensions.AfterDeployment(resource, platform.Gateway, "deployment/kgateway");
        _ = KindHostingExtensions.AfterDeployment(resource, platform.Postgres, "deployment/cnpg");
        _ = KindHostingExtensions.AfterDeployment(resource, platform.KeycloakOperator, "deployment/keycloak-operator");
        if (network is not null)
        {
            _ = KindHostingExtensions.AfterDeployment(resource, network, "daemonset/ztunnel");
        }
    }

    private static async Task<string> CreateBootstrapAsync(
        byte[] realm,
        ParameterResource adminPassword,
        ParameterResource customerSecret,
        ParameterResource adminSecret,
        ParameterResource readerSecret,
        CancellationToken cancellationToken)
    {
        var password = await adminPassword.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The KIND Keycloak admin password is unavailable.");
        var customer = await customerSecret.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The KIND customer gateway secret is unavailable.");
        var admin = await adminSecret.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The KIND admin gateway secret is unavailable.");
        var reader = await readerSecret.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The KIND tenant reader secret is unavailable.");
        var output = new StringBuilder("apiVersion: v1\nkind: Secret\nmetadata:\n  name: identity-bootstrap\n  namespace: identity\ntype: Opaque\ndata:\n");
        AddData(output, "KC_BOOTSTRAP_ADMIN_USERNAME", "admin");
        AddData(output, "KC_BOOTSTRAP_ADMIN_PASSWORD", password);
        AddData(output, "TECK_CUSTOMER_GATEWAY_SECRET", customer);
        AddData(output, "TECK_ADMIN_GATEWAY_SECRET", admin);
        AddData(output, "TECK_TENANT_READER_SECRET", reader);
        _ = output.Append("---\napiVersion: v1\nkind: Secret\nmetadata:\n  name: keycloak-admin\n  namespace: identity\ntype: Opaque\ndata:\n");
        AddData(output, "username", "admin");
        AddData(output, "password", password);
        _ = output.Append("---\napiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: identity-realm\n  namespace: identity\nbinaryData:\n  teck-local-realm.json: ").Append(Convert.ToBase64String(realm)).Append('\n');
        return output.ToString();
    }

    private static void AddData(StringBuilder output, string name, string value) =>
        _ = output.Append("  ").Append(name).Append(": ").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value))).Append('\n');

    private static async Task RunAsync(string executable, string[] arguments, string root, string? input, CancellationToken cancellationToken, string? containerRuntime = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (containerRuntime is not null)
        {
            start.Environment["ASPIRE_CONTAINER_RUNTIME"] = containerRuntime;
        }

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
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"KIND identity step {executable} failed. Check the AppHost resource logs without exposing generated credentials.");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
