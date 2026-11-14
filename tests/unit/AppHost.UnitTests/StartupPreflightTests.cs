using System.Net;
using Shouldly;
using Teck.Cloud.AppHost;
using Xunit;

namespace AppHost.UnitTests;

/// <summary>Verifies startup prerequisites without creating resources or requiring a local engine.</summary>
public sealed class StartupPreflightTests
{
    /// <summary>Only tools used by the selected profile are required.</summary>
    /// <param name="profile">The topology to start.</param>
    /// <param name="mode">The KIND networking mode.</param>
    /// <param name="expected">Expected commands in order.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("development", null, "podman info,podman machine,podman machine")]
    [InlineData("sentinel", null, "podman info,podman machine,podman machine")]
    [InlineData("kind", "sandbox", "podman info,kind version,kubectl version,helm version")]
    [InlineData("kind", "full", "podman info,kind version,kubectl version,helm version")]
    public async Task ProfileRequiresOnlyItsOwnTools(string profile, string? mode, string expected)
    {
        var probe = new Probe();
        await StartupPreflight.CheckAsync(profile, "podman", mode, "192.0.2.10", requiresMachine: true, probe.RunAsync, ResolveAsync, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string.Join(',', probe.Commands).ShouldBe(expected);
    }

    /// <summary>Docker does not require Podman and native Linux Podman does not require a machine.</summary>
    /// <param name="runtime">The configured engine.</param>
    /// <param name="machine">Whether the host platform uses a machine.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("docker", true)]
    [InlineData("podman", false)]
    public async Task SelectedRuntimeIsRespected(string runtime, bool machine)
    {
        var probe = new Probe();
        await StartupPreflight.CheckAsync("sentinel", runtime, mode: null, "192.0.2.10", machine, probe.RunAsync, ResolveAsync, TestContext.Current.CancellationToken).ConfigureAwait(true);
        probe.Commands.ShouldHaveSingleItem().ShouldBe(runtime + " info");
    }

    /// <summary>A working engine API cannot conceal a broken Podman file-copy SSH path.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task BrokenMachineStopsBeforeResourceStartup()
    {
        var probe = new Probe { FailOn = "ssh" };
        var error = await Should.ThrowAsync<InvalidOperationException>(() => StartupPreflight.CheckAsync("development", "podman", mode: null, "192.0.2.10", requiresMachine: true, probe.RunAsync, ResolveAsync, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        error.Message.ShouldContain("file copying", Case.Sensitive);
    }

    /// <summary>Unusable announced addresses fail before starting an engine probe.</summary>
    /// <param name="address">A missing or unusable advertised address.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("https://redis.test")]
    [InlineData("redis.test:6379")]
    public async Task InvalidAddressFailsEarly(string? address)
    {
        var probe = new Probe();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => StartupPreflight.CheckAsync("sentinel", "docker", mode: null, address, requiresMachine: false, probe.RunAsync, ResolveAsync, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        probe.Commands.ShouldBeEmpty();
    }

    /// <summary>DNS aliases resolving to loopback cannot become a Sentinel announcement.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task LoopbackDnsFails() => _ = await Should.ThrowAsync<InvalidOperationException>(() => StartupPreflight.CheckAdvertisedHostAsync("localhost", (_, _) => Task.FromResult(new[] { IPAddress.Loopback }), TestContext.Current.CancellationToken)).ConfigureAwait(true);

    /// <summary>Failure to locate an executable produces an actionable, bounded diagnostic.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MissingToolHasActionableDiagnostic()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => PreflightCommand.RunAsync("teck-missing-preflight-tool", [], "Install the required tool.", TestContext.Current.CancellationToken)).ConfigureAwait(true);
        error.Message.ShouldContain("Install the required tool", Case.Sensitive);
    }

    private static Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        host.ShouldNotBeNullOrWhiteSpace();
        return Task.FromResult(new[] { IPAddress.Parse("192.0.2.10") });
    }

    private sealed class Probe
    {
        public List<string> Commands { get; } = [];

        public string? FailOn { get; init; }

        public Task<string> RunAsync(string tool, string[] arguments, string guidance, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            guidance.ShouldNotBeNullOrWhiteSpace();
            Commands.Add(tool + " " + arguments[0]);
            return arguments.Contains(FailOn, StringComparer.Ordinal)
                ? Task.FromException<string>(new InvalidOperationException(guidance))
                : Task.FromResult("fixture-machine");
        }
    }
}
