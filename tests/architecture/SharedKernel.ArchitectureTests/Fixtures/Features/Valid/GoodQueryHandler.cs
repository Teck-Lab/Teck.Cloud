using SharedKernel.Core.Database;

namespace SharedKernel.ArchitectureTests.Fixtures.Features.Valid;

internal static class GoodQueryHandler
{
    public static Task<int> HandleAsync(GoodQuery query, IGenericReadRepository<FixtureAggregate, Guid> repository)
    {
        _ = query;
        _ = repository;
        return Task.FromResult(1);
    }
}
