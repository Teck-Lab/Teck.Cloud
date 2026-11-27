using ArchUnitNET.Loader;
using SharedKernel.Core.Domain;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace Licensing.Architecture.UnitTests;

/// <summary>Applies the shared four-project business-service rules to the Licensing service.</summary>
public sealed class LicensingServiceArchitectureTests
{
    private static readonly ArchUnitNET.Domain.Architecture LicensingArchitecture = new ArchLoader()
        .LoadAssemblies([.. LicensingAssemblies.Layers, typeof(BaseEntity).Assembly])
        .Build();

    /// <summary>Licensing keeps the Domain/Application/Infrastructure/Host direction and slice conventions.</summary>
    [Fact]
    public void LicensingFollowsSharedServiceArchitecture() =>
        SharedArchitectureRules.AssertAll(
            LicensingArchitecture,
            LicensingAssemblies.Domain,
            LicensingAssemblies.Application,
            LicensingAssemblies.Infrastructure,
            LicensingAssemblies.Host,
            LicensingAssemblies.LicensingNamespace + ".Domain",
            LicensingAssemblies.LicensingNamespace + ".Application");
}
