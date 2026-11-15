using SharedKernel.Core.Database;

namespace SharedKernel.ArchitectureTests;

internal static class BadQuery
{
    public static int Handle(GoodQuery query, IGenericWriteRepository<FixtureAggregate, Guid> repository, IUnitOfWork unitOfWork)
    {
        _ = query;
        _ = repository;
        _ = unitOfWork;
        return 1;
    }
}
