using System.Diagnostics;

namespace Teck.Cloud.AppHost;

/// <summary>Loads local images into an owned KIND cluster using the selected container engine.</summary>
internal static class KindImageLoader
{
    internal static Task LoadAsync(string image, string clusterName, CancellationToken cancellationToken) =>
        LoadAsync(image, clusterName, Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") ?? "docker", cancellationToken);

    internal static async Task EnsureIdentityImageAsync(string image, string clusterName, string kubeconfig, string runtime, CancellationToken cancellationToken)
    {
        var imageId = (await RunAsync(runtime, ["image", "inspect", "--format", "{{.Id}}", image], input: null, cancellationToken).ConfigureAwait(false)).Trim();
        if (string.IsNullOrWhiteSpace(imageId))
        {
            throw new InvalidOperationException("The local Keycloak image has no image ID.");
        }

        var kubectl = new[] { "--kubeconfig", kubeconfig };
        var recordedId = (await RunAsync("kubectl", [.. kubectl, "get", "configmap", "teck-keycloak-image", "-n", "identity", "--ignore-not-found", "-o", "jsonpath={.data.imageId}"], input: null, cancellationToken).ConfigureAwait(false)).Trim();
        if (string.Equals(recordedId, imageId, StringComparison.Ordinal))
        {
            return;
        }

        await LoadAsync(image, clusterName, runtime, cancellationToken).ConfigureAwait(false);
        var existingDeployment = (await RunAsync("kubectl", [.. kubectl, "get", "deployment", "keycloak", "-n", "identity", "--ignore-not-found", "-o", "name"], input: null, cancellationToken).ConfigureAwait(false)).Trim();
        if (!string.IsNullOrEmpty(existingDeployment))
        {
            _ = await RunAsync("kubectl", [.. kubectl, "rollout", "restart", "deployment/keycloak", "-n", "identity"], input: null, cancellationToken).ConfigureAwait(false);
        }

        var state = await RunAsync("kubectl", [.. kubectl, "create", "configmap", "teck-keycloak-image", "-n", "identity", "--from-literal=imageId=" + imageId, "--dry-run=client", "-o", "yaml"], input: null, cancellationToken).ConfigureAwait(false);
        _ = await RunAsync("kubectl", [.. kubectl, "apply", "-f", "-"], state, cancellationToken).ConfigureAwait(false);
    }

    private static async Task LoadAsync(string image, string clusterName, string runtime, CancellationToken cancellationToken)
    {
        if (!string.Equals(runtime, "podman", StringComparison.Ordinal))
        {
            _ = await RunAsync("kind", ["load", "docker-image", image, "--name", clusterName], input: null, cancellationToken).ConfigureAwait(false);
            return;
        }

        // KIND's docker-image command still calls the Docker CLI even with its Podman provider.
        var archive = Path.Combine(Path.GetTempPath(), $"teck-kind-image-{Guid.NewGuid():N}.tar");
        try
        {
            _ = await RunAsync("podman", ["save", "--output", archive, image], input: null, cancellationToken).ConfigureAwait(false);
            _ = await RunAsync("kind", ["load", "image-archive", archive, "--name", clusterName], input: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    private static async Task<string> RunAsync(string executable, string[] arguments, string? input, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
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
            var standardOutput = await output.ConfigureAwait(false);
            var standardError = await error.ConfigureAwait(false);
            return process.ExitCode == 0
                ? standardOutput
                : throw new InvalidOperationException($"KIND image step {executable} failed: {standardError}");
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
