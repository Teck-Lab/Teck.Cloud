using SharedKernel.Persistence.Database.EFCore;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Test read repository for GenericReadRepository testing.
/// </summary>
internal sealed class TestReadRepository : GenericReadRepository<TestReadModel, Guid, TestReadDbContext>
{
    public TestReadRepository(TestReadDbContext dbContext) : base(dbContext)
    {
    }
}
