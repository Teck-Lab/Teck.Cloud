using Teck.Cloud.Arch.Tests.Rules;
using Xunit;

namespace Teck.Cloud.Arch.Tests;

public class SharedRepositoryTests : SharedTestBase
{
    [Fact]
    public void WriteRepositories_Should_UseEntitiesImplementingIAggregateRoot()
    {
        RepositoryRules.WriteRepositories_Should_UseEntitiesImplementingIAggregateRoot(SharedArchitecture);
    }

    [Fact]
    public void ReadRepositories_Should_UseReadModels()
    {
        RepositoryRules.ReadRepositories_Should_UseReadModels(SharedArchitecture);
    }
}
