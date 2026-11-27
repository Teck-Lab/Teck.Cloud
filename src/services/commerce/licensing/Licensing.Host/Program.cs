#if COMMERCE_WOLVERINE_CODEGEN
using JasperFx;
using JasperFx.CodeGeneration.Model;
using JasperFx.CodeGeneration.Services;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Licensing.Infrastructure.Database;
using Wolverine;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;
#endif

namespace Teck.Cloud.Commerce.Licensing.Host;

/// <summary>Separate licensing process entry point.</summary>
public static class Program
{
#if COMMERCE_WOLVERINE_CODEGEN
    private static readonly Type[] ServiceLocationAllowlist =
    [
        typeof(OperationContext),
        typeof(ITenantDirectory),
        typeof(OrdersV1.Orders.OrdersClient),
        typeof(LicensingReadDbContext),
        typeof(LicensingWriteDbContext),
        typeof(IUnitOfWork),
    ];

    private static string HostAssemblyName =>
        typeof(LicensingHost).Assembly.GetName().Name ?? "Teck.Cloud.Commerce.Licensing.Host";

    private static string ServiceLocationAttestation =>
        $"Commerce codegen service-location verified: {HostAssemblyName} {nameof(ServiceLocationPolicy.NotAllowed)} {string.Join(", ", ServiceLocationAllowlist.Select(static type => type.FullName ?? type.Name).Order(StringComparer.Ordinal))}";
#endif

    /// <summary>Builds and runs the licensing host.</summary>
    /// <param name="args">Host configuration arguments.</param>
    /// <returns>The process exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return RunAsync(args);
    }

    private static async Task<int> RunAsync(string[] args)
    {
#if COMMERCE_WOLVERINE_CODEGEN
        if (args is not ["codegen", "write"])
        {
            await Console.Error.WriteLineAsync("The Commerce handler generation binary accepts only 'codegen write'.").ConfigureAwait(false);
            return 2;
        }

        var codegenApp = LicensingHost.Build([]);
        await using (codegenApp.ConfigureAwait(false))
        {
            if (!TryVerifyServiceLocation(codegenApp.Services.GetRequiredService<WolverineOptions>()))
            {
                await Console.Error.WriteLineAsync($"Commerce codegen service-location policy invalid: {HostAssemblyName}").ConfigureAwait(false);
                return 1;
            }

            await Console.Out.WriteLineAsync(ServiceLocationAttestation).ConfigureAwait(false);
            return await codegenApp.RunJasperFxCommands(args).ConfigureAwait(false);
        }
#else
        var app = LicensingHost.Build(args);
        await using (app.ConfigureAwait(false))
        {
            await app.RunAsync().ConfigureAwait(false);
        }

        return 0;
#endif
    }

#if COMMERCE_WOLVERINE_CODEGEN

    private static bool TryVerifyServiceLocation(WolverineOptions options)
    {
        if (options.ServiceLocationPolicy != ServiceLocationPolicy.NotAllowed)
        {
            return false;
        }

        var sources = options.CodeGeneration.Sources.OfType<LazyServiceLocationVariableSource>().ToArray();
        return sources.Length == ServiceLocationAllowlist.Length
            && ServiceLocationAllowlist.All(expected => sources.Where(source => source.Matches(expected)).Take(2).Count() == 1)
            && sources.All(source => ServiceLocationAllowlist.Where(source.Matches).Take(2).Count() == 1);
    }
#endif
}
