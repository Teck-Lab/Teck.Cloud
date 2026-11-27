using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.MultiTenancy;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>One real commerce process composed through its production Build API.</summary>
internal sealed class CommerceHost : IAsyncDisposable
{
    private readonly Func<string[], WebApplication> _build;
    private string[] _args;
    private WebApplication? _application;

    private CommerceHost(Func<string[], WebApplication> build, string[] args)
    {
        _build = build;
        _args = args;
    }

    internal Uri? HttpAddress { get; private set; }

    internal Uri? GrpcAddress { get; private set; }

    internal bool IsRunning => _application is not null;

    internal WebApplication Application => _application ?? throw new InvalidOperationException("The host is not running.");

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static async Task<CommerceHost> StartAsync(
        Func<string[], WebApplication> build,
        string[] args,
        Uri? httpAddress,
        Uri? grpcAddress,
        CancellationToken cancellationToken)
    {
        var host = new CommerceHost(build, args)
        {
            HttpAddress = httpAddress,
            GrpcAddress = grpcAddress,
        };
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_application is not null)
        {
            throw new InvalidOperationException("The host is already running.");
        }

        var application = _build(_args);
        try
        {
            await MigrateAsync(application, cancellationToken).ConfigureAwait(false);
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            EnsureListened(application, HttpAddress, GrpcAddress);
            _application = application;
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_application is null)
        {
            return;
        }

        var application = _application;
        _application = null;
        await application.StopAsync(cancellationToken).ConfigureAwait(false);
        await application.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Restarts the process composition against the same durable dependencies.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    internal async Task RestartAsync(CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restarts with a changed configuration, preserving the same listeners.</summary>
    /// <param name="args">The new production settings.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    internal async Task RestartAsync(string[] args, CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        _args = args;
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MigrateAsync(WebApplication application, CancellationToken cancellationToken)
    {
        foreach (var name in new[] { "Orders.Infrastructure", "Licensing.Infrastructure" })
        {
            try
            {
                _ = Assembly.Load(name);
            }
            catch (FileNotFoundException)
            {
                // The host has not composed this service assembly in the current lane.
            }
        }

        var contexts = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .Where(type => !type.IsAbstract
                && typeof(DbContext).IsAssignableFrom(type)
                && type.Name.EndsWith("WriteDbContext", StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        foreach (var type in contexts)
        {
            var scope = application.Services.CreateAsyncScope();
            await using var configuredScope = scope.ConfigureAwait(false);
            if (scope.ServiceProvider.GetService(type) is not DbContext context)
            {
                continue;
            }

            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void EnsureListened(WebApplication application, Uri? httpAddress, Uri? grpcAddress)
    {
        var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
            ?? Array.Empty<string>();
        var bound = addresses.Select(static value => new Uri(value)).ToArray();
        foreach (var expected in new[] { httpAddress, grpcAddress })
        {
            if (expected is not null && !bound.Any(actual => actual.Port == expected.Port))
            {
                throw new InvalidOperationException(
                    "The host did not bind the fixture address " + expected + "; actual: " + string.Join(", ", addresses));
            }
        }
    }
}
