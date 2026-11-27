using System.Globalization;
using System.Net.Sockets;
using Commerce.IntegrationTests.Infrastructure;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using JasperFx.CodeGeneration.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;
using Xunit;

namespace Commerce.IntegrationTests.Tests;

/// <summary>Real hosts start only from the committed static Wolverine handler registries.</summary>
/// <param name="keycloak">The shared disposable identity provider.</param>
/// <remarks>
/// The filtered run asserts the explicitly requested <c language="csharp">DOTNET_ENVIRONMENT</c>/<c language="csharp">ASPNETCORE_ENVIRONMENT</c>
/// profile (the framework Production default when neither is set). A disposable-copy negative control is
/// selected with <c language="csharp">COMMERCE_STATIC_NEGATIVE_CASE</c> = <c language="csharp">registry</c> (the unique
/// <c language="csharp">GeneratedHandlerRegistry.cs</c> removed) or <c language="csharp">adapter</c> (the generated handler adapter removed and
/// its single <c language="csharp">DynamicDependency</c> line removed), <c language="csharp">COMMERCE_STATIC_NEGATIVE_HOST</c> = <c language="csharp">Orders</c> or
/// <c language="csharp">Licensing</c>, plus the absolute <c language="csharp">TECK_COMMERCE_STATIC_ROOT</c> and untouched same-revision
/// <c language="csharp">TECK_COMMERCE_STATIC_PRISTINE_ROOT</c> copies. Unset means the positive startup run.
/// </remarks>
[Collection(CommerceIntegrationSuite.Name)]
public sealed class CommerceStaticStartupTests(CommerceKeycloakFixture keycloak)
{
    /// <summary>Both hosts attach their pre-generated registry without an assembly scan or dynamic code.</summary>
    /// <returns>The startup verification task.</returns>
    [Fact]
    public async Task StartsWithGeneratedRegistries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = CommerceStaticCase.ExpectedProfile();
        var negative = CommerceStaticCase.ReadNegative();
        if (negative is null)
        {
            var stack = await keycloak.StackAsync(cancellationToken).ConfigureAwait(true);
            AssertHost(stack.Orders, stack.OrdersLogs, CommerceStaticCase.Orders, profile);
            AssertHost(stack.Licensing, stack.LicensingLogs, CommerceStaticCase.Licensing, profile);
            return;
        }

        await AssertNegativeAsync(keycloak, negative, profile, cancellationToken).ConfigureAwait(true);
    }

    private static void AssertHost(CommerceHost host, HostLogCapture logs, CommerceStaticHost expected, string profile)
    {
        var because = expected.Name + " startup verification for profile " + profile;
        host.IsRunning.ShouldBeTrue(because + " must be running");
        host.Application.Environment.EnvironmentName.ShouldBe(profile, because + " hosting environment");
        var runtime = host.Application.Services.GetRequiredService<IWolverineRuntime>();
        runtime.Options.ApplicationAssembly.ShouldBe(expected.OwningType.Assembly, because + " application assembly");
        runtime.Options.CodeGeneration.TypeLoadMode.ShouldBe(TypeLoadMode.Static, because + " type load mode");
        runtime.Options.CodeGeneration.SourceCodeWritingEnabled.ShouldBeFalse(because + " must not write generated sources");
        runtime.Options.ServiceLocationPolicy.ShouldBe(ServiceLocationPolicy.NotAllowed, because + " service location policy");
        AssertServiceLocationAllowlist(runtime, expected, because);
        AssertRegistryAttached(logs, because);
    }

    private static void AssertServiceLocationAllowlist(IWolverineRuntime runtime, CommerceStaticHost host, string because)
    {
        var sources = runtime.Options.CodeGeneration.Sources.OfType<LazyServiceLocationVariableSource>().ToArray();
        sources.Length.ShouldBe(host.AllowedServiceLocations.Count, because + " must register exactly the approved service-location sources");
        foreach (var type in host.AllowedServiceLocations)
        {
            sources.Count(source => source.Matches(type)).ShouldBe(1, because + " must allow exactly one source for " + type.FullName);
        }

        foreach (var source in sources)
        {
            host.AllowedServiceLocations.Count(source.Matches).ShouldBe(1, because + " has an unmatched service-location source");
        }
    }

    private static void AssertRegistryAttached(HostLogCapture logs, string because)
    {
        var matches = logs.Messages(1)
            .Where(message => message.Contains(CommerceStaticCase.RegistryLogPrefix, StringComparison.Ordinal))
            .ToArray();
        matches.Length.ShouldBe(1, because + " must attach the pre-generated registry exactly once on the initial start");
        HandlerCount(matches[0]).ShouldBeGreaterThan(0, because + " registry must list handler types");
        logs.AllMessages().ShouldNotContain(
            message => message.Contains(CommerceStaticCase.RegistryFallbackMessage, StringComparison.Ordinal),
            because + " must never fall back to an assembly scan");
    }

    private static int HandlerCount(string message)
    {
        var start = message.IndexOf(CommerceStaticCase.RegistryLogPrefix, StringComparison.Ordinal)
            + CommerceStaticCase.RegistryLogPrefix.Length;
        var end = message.IndexOf(" handler types", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "the registry log must report a handler count: " + message);
        return int.TryParse(message[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : throw new InvalidOperationException("Unparsable handler count in log: " + message);
    }

    private static async Task AssertNegativeAsync(
        CommerceKeycloakFixture keycloak,
        NegativeSelection selection,
        string profile,
        CancellationToken cancellationToken)
    {
        var host = selection.Host;
        var other = CommerceStaticCase.Other(host);
        var registryLoss = string.Equals(selection.Kind, CommerceStaticCase.RegistryKind, StringComparison.Ordinal);
        var pristine = GeneratedHostTree.Read(selection.PristineRoot, host);
        var current = GeneratedHostTree.Read(selection.Root, host, removedRegistryPath: registryLoss ? pristine.RegistryPath : null);
        var pristineOther = GeneratedHostTree.Read(selection.PristineRoot, other);
        var currentOther = GeneratedHostTree.Read(selection.Root, other);
        GeneratedStartupControls.AssertUnchanged(pristineOther, currentOther, other.Name + " generated bytes for profile " + profile + " case " + selection.Kind);

        AdapterLoss? adapter = null;
        if (registryLoss)
        {
            GeneratedStartupControls.AssertRegistryRemoved(pristine, current, host);
        }
        else
        {
            adapter = GeneratedStartupControls.AssertAdapterRemoved(pristine, current, host);
        }

        var stack = await CommerceStack.StartDependenciesAsync(keycloak, cancellationToken).ConfigureAwait(true);
        await using var stackLease = stack.ConfigureAwait(false);
        await StartHostAsync(stack, other, cancellationToken).ConfigureAwait(true);
        AssertHostHeldBy(stack, other, profile);

        if (adapter is null)
        {
            var failure = await Should.ThrowAsync<InvalidOperationException>(() => StartHostAsync(stack, host, cancellationToken)).ConfigureAwait(true);
            failure.Message.StartsWith(CommerceStaticCase.RegistryGuardPrefix + " " + host.OwningAssemblyName, StringComparison.Ordinal).ShouldBeTrue(
                host.Name + " registry guard message for profile " + profile + " case " + selection.Kind + ": " + failure.Message);
            failure.Message.Contains(other.OwningAssemblyName, StringComparison.Ordinal).ShouldBeFalse(
                host.Name + " registry guard must name only its own host for profile " + profile + ": " + failure.Message);
        }
        else
        {
            var failure = await Should.ThrowAsync<MissingPreBuiltTypesException>(() => StartHostAsync(stack, host, cancellationToken)).ConfigureAwait(true);
            failure.Message.Contains(host.OwningAssemblyName, StringComparison.Ordinal).ShouldBeTrue(
                host.Name + " missing pre-built types must name the owning assembly for profile " + profile + ": " + failure.Message);
            (failure.Message.Contains(adapter.Name, StringComparison.Ordinal)
                || failure.Message.Contains(adapter.FullName, StringComparison.Ordinal)).ShouldBeTrue(
                host.Name + " missing pre-built types must name the missing chain for profile " + profile + ": " + failure.Message);
        }

        await AssertNotListeningAsync(stack, host, cancellationToken).ConfigureAwait(true);
    }

    private static void AssertHostHeldBy(CommerceStack stack, CommerceStaticHost expected, string profile) =>
        AssertHost(
            ReferenceEquals(expected, CommerceStaticCase.Orders) ? stack.Orders : stack.Licensing,
            ReferenceEquals(expected, CommerceStaticCase.Orders) ? stack.OrdersLogs : stack.LicensingLogs,
            expected,
            profile);

    private static Task StartHostAsync(CommerceStack stack, CommerceStaticHost host, CancellationToken cancellationToken) =>
        ReferenceEquals(host, CommerceStaticCase.Orders)
            ? stack.StartOrdersHostAsync(cancellationToken)
            : stack.StartLicensingHostAsync(cancellationToken);

    private static async Task AssertNotListeningAsync(CommerceStack stack, CommerceStaticHost host, CancellationToken cancellationToken)
    {
        var orders = ReferenceEquals(host, CommerceStaticCase.Orders);
        await AssertClosedAsync(host, orders ? stack.OrdersAddress : stack.LicensingAddress, cancellationToken).ConfigureAwait(true);
        if (orders)
        {
            await AssertClosedAsync(host, stack.OrdersWebhookAddress, cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task AssertClosedAsync(CommerceStaticHost host, Uri address, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        var refused = false;
        try
        {
            await client.ConnectAsync(address.Host, address.Port, deadline.Token).ConfigureAwait(true);
        }
        catch (SocketException)
        {
            refused = true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            refused = true;
        }

        refused.ShouldBeTrue(host.Name + " must not bind " + address);
    }
}
