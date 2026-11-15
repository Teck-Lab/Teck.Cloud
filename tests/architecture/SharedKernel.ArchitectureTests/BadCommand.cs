using SharedKernel.Core.Database;

namespace SharedKernel.ArchitectureTests;

internal static class BadCommand
{
    public static int Handle(GoodCommand command, IGenericReadRepository<FixtureAggregate, Guid> repository)
    {
        _ = command;
        _ = repository;
        return 1;
    }
}
