using SharedKernel.Core.Database;

namespace SharedKernel.ArchitectureTests.Fixtures.Features.Valid;

internal static class GoodCommandHandler
{
    public static int Handle(GoodCommand command, IGenericWriteRepository<FixtureAggregate, Guid> repository, IUnitOfWork unitOfWork)
    {
        _ = command;
        _ = repository;
        _ = unitOfWork;
        return 1;
    }
}
