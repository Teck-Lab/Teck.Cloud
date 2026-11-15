using System.Diagnostics;

namespace Teck.Cloud.AppHost;

/// <summary>Loads a local image into an owned KIND cluster using the selected container engine.</summary>
internal static class KindImageLoader
{
    internal static async Task LoadAsync(string image, string clusterName, CancellationToken cancellationToken)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME"), "podman", StringComparison.Ordinal))
        {
            await RunAsync("kind", ["load", "docker-image", image, "--name", clusterName], cancellationToken).ConfigureAwait(false);
            return;
        }

        // KIND's docker-image command still calls the Docker CLI even with its Podman provider.
        var archive = Path.Combine(Path.GetTempPath(), $"teck-kind-image-{Guid.NewGuid():N}.tar");
        try
        {
            await RunAsync("podman", ["save", "--output", archive, image], cancellationToken).ConfigureAwait(false);
            await RunAsync("kind", ["load", "image-archive", archive, "--name", clusterName], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    private static async Task RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
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
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            var standardError = await error.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"KIND image load step {executable} failed: {standardError}");
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
