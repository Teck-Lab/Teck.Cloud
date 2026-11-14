using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using SharedKernel.Core.Domain;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Persistence.Caching;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SharedKernel.ArchitectureTests;

/// <summary>Enforces the dependency direction between the shared foundation layers.</summary>
public sealed class LayerTests
{
    private static readonly Architecture Model = new ArchLoader()
        .LoadAssemblies(typeof(BaseEntity).Assembly, typeof(RedisConnection).Assembly, typeof(HostingExtensions).Assembly).Build();

    /// <summary>Domain and application contracts cannot depend on persistence or transport implementations.</summary>
    [Fact]
    public void CoreDoesNotDependOnAdapters() =>
        Types().That().ResideInAssembly(typeof(BaseEntity).Assembly).Should()
            .NotDependOnAny(Types().That().ResideInAssembly(typeof(RedisConnection).Assembly)
                .Or().ResideInAssembly(typeof(HostingExtensions).Assembly)).Check(Model);

    /// <summary>Persistence cannot depend on the HTTP and messaging composition layer.</summary>
    [Fact]
    public void PersistenceDoesNotDependOnInfrastructure() =>
        Types().That().ResideInAssembly(typeof(RedisConnection).Assembly).Should()
            .NotDependOnAny(Types().That().ResideInAssembly(typeof(HostingExtensions).Assembly)).Check(Model);
}
