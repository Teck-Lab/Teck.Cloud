using Teck.Cloud.Arch.Tests.Rules;
using Xunit;

namespace Teck.Cloud.Arch.Tests;

public class SharedCqrsTests : SharedTestBase
{
    [Fact]
    public void QueryHandlers_Should_UseReadRepositories()
    {
        QueryHandlerRules.QueryHandlersShouldUseReadRepositories(SharedArchitecture);
    }

    [Fact]
    public void QueryHandlers_Should_NotUseWriteRepositories()
    {
        QueryHandlerRules.QueryHandlersShouldNotUseWriteRepositories(SharedArchitecture);
    }

    [Fact]
    public void CommandHandlers_Should_UseWriteRepositories()
    {
        CommandHandlerRules.CommandHandlersShouldUseWriteRepositories(SharedArchitecture);
    }

    [Fact]
    public void QueryHandlers_Should_BeSealed()
    {
        QueryHandlerRules.QueryHandlersShouldBeSealed(SharedArchitecture);
    }

    [Fact]
    public void CommandHandlers_Should_BeSealed()
    {
        CommandHandlerRules.CommandHandlersShouldBeSealed(SharedArchitecture);
    }
}
