#nullable enable
using Catalog.Application.Promotions.Repositories;
using Catalog.Application.Promotions.ReadModels;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence.Repositories.Read;
using Catalog.IntegrationTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Catalog.IntegrationTests.Infrastructure.Promotions
{
    [Collection("SharedTestcontainers")]
    public class PromotionReadRepositoryIntegrationTests : BaseReadRepoTestFixture<ApplicationReadDbContext>
    {
        private IPromotionReadRepository _readRepository = null!;

        public PromotionReadRepositoryIntegrationTests(SharedTestcontainersFixture sharedFixture) : base(sharedFixture)
        {
        }

        protected override ApplicationReadDbContext CreateReadDbContext(DbContextOptions<ApplicationReadDbContext> options)
            => new ApplicationReadDbContext(options);

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            var httpContextAccessor = new HttpContextAccessor();
            _readRepository = new PromotionReadRepository(ReadDbContext);
        }

        [Fact]
        public async Task ReadModel_GetById_ReturnsSeededModel()
        {
            // Arrange: seed a read model directly
            var promo = new PromotionReadModel
            {
                Id = Guid.NewGuid(),
                Name = "TestPromo",
                Description = "desc",
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddDays(10),
                IsActive = true
            };
            ReadDbContext.Promotions.Add(promo);
            await ReadDbContext.SaveChangesAsync(CancellationToken.None);

            // Act
            var read = await _readRepository.GetByIdAsync(promo.Id, CancellationToken.None);

            // Assert
            read.ShouldNotBeNull();
            read!.Name.ShouldBe("TestPromo");
        }
    }
}
