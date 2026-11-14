using System.ComponentModel;
using System.Diagnostics;

namespace Teck.Cloud.AppHost;

/// <summary>Runs noninteractive prerequisite probes without echoing runtime configuration.</summary>
internal static class PreflightCommand
{
    internal static async Task<string> RunAsync(string executable, string[] arguments, string guidance, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(guidance + " The required executable could not be started.", exception);
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var stdout = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
            return process.ExitCode == 0
                ? stdout.Trim()
                : throw new InvalidOperationException(guidance + " The prerequisite probe failed; its output is withheld because runtime configuration can contain credentials.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(guidance + " The prerequisite probe exceeded 20 seconds.", exception);
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
