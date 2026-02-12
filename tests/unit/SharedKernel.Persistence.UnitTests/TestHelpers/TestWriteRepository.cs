using Microsoft.AspNetCore.Http;
using SharedKernel.Persistence.Database.EFCore;
using SharedKernel.Persistence.UnitTests.TestHelpers;

namespace SharedKernel.Persistence.UnitTests.Database.EFCore;

/// <summary>
/// Test repository that inherits from GenericWriteRepository for testing purposes.
/// </summary>
internal sealed class TestWriteRepository : GenericWriteRepository<TestEntity, Guid, TestDbContext>
{
    public TestWriteRepository(TestDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }
}
