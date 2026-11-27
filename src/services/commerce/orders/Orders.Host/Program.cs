#if COMMERCE_WOLVERINE_CODEGEN
using JasperFx;
using JasperFx.CodeGeneration.Model;
using JasperFx.CodeGeneration.Services;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Infrastructure.Database;
using Wolverine;
#endif

namespace Teck.Cloud.Commerce.Orders.Host;

/// <summary>Process entrypoint; host composition lives in <see cref="OrdersHost"/>.</summary>
public static class Program
{
#if COMMERCE_WOLVERINE_CODEGEN
    private const string ServiceLocationInvalidPrefix = "Commerce codegen service-location policy invalid:";

    private const string ServiceLocationVerifiedPrefix = "Commerce codegen service-location verified:";

    private static readonly Type[] ServiceLocationAllowlist =
    [
        typeof(OperationContext),
        typeof(ITenantDirectory),
        typeof(OrdersReadDbContext),
        typeof(OrdersWriteDbContext),
        typeof(IUnitOfWork),
    ];

    private static string HostAssemblyName => typeof(OrdersHost).Assembly.GetName().Name ?? nameof(OrdersHost);

    /// <summary>Runs the isolated handler generation command.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>The process exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return GenerateAsync(args);
    }

    private static async Task<int> GenerateAsync(string[] args)
    {
        if (args is not ["codegen", "write"])
        {
            await Console.Error.WriteLineAsync("The Commerce handler generation build accepts only 'codegen write'.").ConfigureAwait(false);
            return 1;
        }

        var app = OrdersHost.Build([]);
        try
        {
            var mismatch = DescribeServiceLocationMismatch(app.Services);
            if (mismatch is not null)
            {
                await Console.Error.WriteLineAsync(ServiceLocationInvalidPrefix + " " + HostAssemblyName + " " + mismatch).ConfigureAwait(false);
                return 1;
            }

            var names = string.Join(", ", ServiceLocationAllowlist.Select(type => type.FullName).Order(StringComparer.Ordinal));
            await Console.Out.WriteLineAsync(ServiceLocationVerifiedPrefix + " " + HostAssemblyName + " " + nameof(ServiceLocationPolicy.NotAllowed) + " " + names).ConfigureAwait(false);
            return await app.RunJasperFxCommands(args).ConfigureAwait(false);
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string? DescribeServiceLocationMismatch(IServiceProvider services)
    {
        var options = services.GetRequiredService<WolverineOptions>();
        if (options.ServiceLocationPolicy != ServiceLocationPolicy.NotAllowed)
        {
            return "expected " + nameof(ServiceLocationPolicy.NotAllowed) + " but found " + options.ServiceLocationPolicy + ".";
        }

        var sources = options.CodeGeneration.Sources.OfType<LazyServiceLocationVariableSource>().ToArray();
        return sources.Length == ServiceLocationAllowlist.Length
            && ServiceLocationAllowlist.All(type => sources.Where(source => source.Matches(type)).Take(2).Count() == 1)
            && sources.All(source => ServiceLocationAllowlist.Where(source.Matches).Take(2).Count() == 1)
            ? null
            : "the scoped service-location sources do not match the host allowlist.";
    }
#else
    /// <summary>Runs the service or applies migrations.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>The process exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return RunAsync(args);
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (Array.Exists(args, argument => string.Equals(argument, "--migrate", StringComparison.Ordinal)))
        {
            await OrdersHost.MigrateAsync(args).ConfigureAwait(false);
            return 0;
        }

        var app = OrdersHost.Build(args);
        await using (app.ConfigureAwait(false))
        {
            await app.RunAsync().ConfigureAwait(false);
        }

        return 0;
    }
#endif
}
