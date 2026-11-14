using System.Diagnostics;

namespace Teck.Cloud.AppHost;

/// <summary>Waits for named workloads instead of trusting an empty Helm label selector.</summary>
internal static class KindPlatformReadiness
{
    internal static async Task WaitForRolloutAsync(KindHelmChartResource chart, string workload, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(FindKubectl())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "--kubeconfig", chart.Parent.KubeconfigPath, "--namespace", chart.Namespace!, "rollout", "status", workload, "--timeout=300s" })
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
            var stdout = await output.ConfigureAwait(false);
            var stderr = await error.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"KIND platform {chart.Name}/{workload} did not become ready: {stdout} {stderr}");
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

    private static string FindKubectl()
    {
        var executable = OperatingSystem.IsWindows() ? "kubectl.exe" : "kubectl";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(Path.IsPathFullyQualified)
            .Select(directory => Path.Combine(directory, executable))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("KIND platform readiness requires kubectl on an absolute PATH entry.");
    }
}
