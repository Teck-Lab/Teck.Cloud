using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Core.Caching;
using SharedKernel.Core.Domain;
using SharedKernel.Persistence.Database;
using Teck.Cloud.ArchitectureRules;
using Xunit;

namespace SharedKernel.ArchitectureTests;

/// <summary>Proves handlers must use an explicit CQRS persistence path.</summary>
public sealed class CqrsPositiveRuleTests
{
    /// <summary>A command without a write repository is rejected.</summary>
    [Fact]
    public void CommandWithoutWriteRepositoryIsRejected()
    {
        _ = typeof(MissingWriteCommandHandler);
        var failure = Assert.ThrowsAny<Exception>(() => CommandHandlerRules.CommandHandlersShouldUseWriteRepositories(typeof(CqrsPositiveRuleTests).Assembly));
        Assert.Contains(nameof(MissingWriteCommandHandler), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Fixtures.Features.Valid.GoodCommandHandler), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A query without a read repository or read-only cache is rejected.</summary>
    [Fact]
    public void QueryWithoutReadPathIsRejected()
    {
        _ = typeof(MissingReadQueryHandler);
        _ = typeof(CachedQueryHandler);
        _ = typeof(MutableCacheQueryHandler);
        var failure = Assert.ThrowsAny<Exception>(() => QueryHandlerRules.QueryHandlersShouldUseReadRepositoriesOrCaches(typeof(CqrsPositiveRuleTests).Assembly));
        Assert.Contains(nameof(MissingReadQueryHandler), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CachedQueryHandler), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Fixtures.Features.Valid.GoodQueryHandler), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A query cannot inject a cache interface that exposes mutations.</summary>
    [Fact]
    public void QueryWithMutableCacheIsRejected()
    {
        _ = typeof(MutableCacheQueryHandler);
        var failure = Assert.ThrowsAny<Exception>(() => QueryHandlerRules.QueryHandlersShouldNotUseMutablePersistence(typeof(CqrsPositiveRuleTests).Assembly));
        Assert.Contains(nameof(MutableCacheQueryHandler), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CachedQueryHandler), failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A mapped read model needs an explicit ReadModel suffix.</summary>
    [Fact]
    public void AmbiguousReadModelNameIsRejected()
    {
        _ = typeof(AmbiguousProjectionConfiguration);
        _ = typeof(GetOrderByIdReadModelConfiguration);
        var failure = Assert.ThrowsAny<Exception>(() => ReadModelRules.ReadModelsShouldHaveExplicitNames(typeof(CqrsPositiveRuleTests).Assembly));
        Assert.Contains(nameof(AmbiguousProjection), failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(GetOrderByIdReadModel), failure.Message, StringComparison.Ordinal);
    }

    internal static class MissingWriteCommandHandler
    {
        public static int Handle(GoodCommand command)
        {
            _ = command;
            return 1;
        }
    }

    internal static class MissingReadQueryHandler
    {
        public static int Handle(GoodQuery query)
        {
            _ = query;
            return 1;
        }
    }

    internal static class CachedQueryHandler
    {
        public static int Handle(GoodQuery query, IReadCacheService<GetOrderByIdReadModel, Guid> cache)
        {
            _ = query;
            _ = cache;
            return 1;
        }
    }

    internal static class MutableCacheQueryHandler
    {
        public static int Handle(GoodQuery query, IGenericCacheService<GetOrderByIdReadModel, Guid> cache)
        {
            _ = query;
            _ = cache;
            return 1;
        }
    }

    internal sealed class AmbiguousProjection : IReadModel<Guid>
    {
        public Guid Id { get; set; }
    }

    internal sealed class GetOrderByIdReadModel : IReadModel<Guid>
    {
        public Guid Id { get; set; }
    }

    internal sealed class AmbiguousProjectionConfiguration : IReadModelConfiguration<AmbiguousProjection>
    {
        public void Configure(EntityTypeBuilder<AmbiguousProjection> builder) => builder.HasKey(model => model.Id);
    }

    internal sealed class GetOrderByIdReadModelConfiguration : IReadModelConfiguration<GetOrderByIdReadModel>
    {
        public void Configure(EntityTypeBuilder<GetOrderByIdReadModel> builder) => builder.HasKey(model => model.Id);
    }
}
