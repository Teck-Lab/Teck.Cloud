using ArchUnitNET.Loader;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace SharedKernel.ArchitectureTests;

/// <summary>Proves the ported rules detect prohibited patterns in compiled fixtures.</summary>
public sealed class CqrsRuleTests
{
    /// <summary>Command handlers cannot use a read repository.</summary>
    [Fact]
    public void CommandReadRepositoryIsRejected()
    {
        _ = typeof(BadCommand);
        var failure = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandHandlersShouldNotUseReadRepositories(typeof(CqrsRuleTests).Assembly));
        Assert.Contains(nameof(BadCommand), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Query handlers cannot use a write repository.</summary>
    [Fact]
    public void QueryWriteRepositoryIsRejected()
    {
        _ = typeof(BadQuery);
        var failure = Assert.ThrowsAny<Exception>(() => QueryHandlerRules.QueryHandlersShouldNotUseWriteRepositories(typeof(CqrsRuleTests).Assembly));
        Assert.Contains(nameof(BadQuery), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Queries cannot inject unit of work or EF contexts.</summary>
    [Fact]
    public void QueryMutablePersistenceIsRejected()
    {
        _ = typeof(BadDbQuery);
        var failure = Assert.ThrowsAny<Exception>(() => QueryHandlerRules.QueryHandlersShouldNotUseMutablePersistence(typeof(CqrsRuleTests).Assembly));
        Assert.Contains(nameof(BadDbQuery), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>ArchUnitNET detects mutable commands.</summary>
    [Fact]
    public void MutableCommandsAreRejected()
    {
        _ = typeof(MutableCommand);
        var architecture = new ArchLoader().LoadAssemblies(typeof(CqrsRuleTests).Assembly).Build();
        var failure = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandsShouldBeImmutable(architecture));
        Assert.Contains(nameof(MutableCommand), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>ArchUnitNET detects mutable queries.</summary>
    [Fact]
    public void MutableQueriesAreRejected()
    {
        _ = typeof(MutableQuery);
        var architecture = new ArchLoader().LoadAssemblies(typeof(CqrsRuleTests).Assembly).Build();
        var failure = Assert.ThrowsAny<Exception>(() => QueryHandlerRules.QueriesShouldNotModifyState(architecture));
        Assert.Contains(nameof(MutableQuery), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>ArchUnitNET detects aggregate roots that bypass the shared entity base.</summary>
    [Fact]
    public void AggregateWithoutEntityBaseIsRejected()
    {
        _ = typeof(FixtureAggregate);
        var architecture = new ArchLoader().LoadAssemblies(typeof(CqrsRuleTests).Assembly).Build();
        var failure = Assert.ThrowsAny<Exception>(() => AggregateRootRules.AggregatesShouldInheritFromBaseEntity(architecture));
        Assert.Contains(nameof(FixtureAggregate), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Handler names and slice placement are enforced.</summary>
    [Fact]
    public void MisplacedHandlersAreRejected()
    {
        var assembly = typeof(CqrsRuleTests).Assembly;
        var naming = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandHandlersShouldBeStaticClassesEndingWithHandler(assembly));
        var placement = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandHandlersShouldResideInFeaturesNamespace(assembly));
        Assert.Contains(nameof(BadCommand), naming.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BadCommand), placement.Message, StringComparison.Ordinal);
    }

    /// <summary>Fixture handlers following the convention are discoverable and do not appear as violations.</summary>
    [Fact]
    public void ValidHandlersAreNotReported()
    {
        _ = typeof(GoodCommand);
        _ = typeof(GoodQuery);
        _ = typeof(FixtureAggregate);
        _ = typeof(Fixtures.Features.Valid.GoodCommandHandler);
        _ = typeof(Fixtures.Features.Valid.GoodQueryHandler);
        var assembly = typeof(CqrsRuleTests).Assembly;
        var failure = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandHandlersShouldNotUseReadRepositories(assembly));
        Assert.DoesNotContain(nameof(Fixtures.Features.Valid.GoodCommandHandler), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Fixtures.Features.Valid.GoodQueryHandler), failure.Message, StringComparison.Ordinal);
    }
}
