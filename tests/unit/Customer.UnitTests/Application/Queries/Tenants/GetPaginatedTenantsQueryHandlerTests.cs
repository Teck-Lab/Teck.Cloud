using Customer.Application.Tenants.Features.GetPaginatedTenants.V1;
using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class GetPaginatedTenantsQueryHandlerTests
{
    private readonly ITenantReadRepository tenantReadRepository;
    private readonly GetPaginatedTenantsQueryHandler handler;

    public GetPaginatedTenantsQueryHandlerTests()
    {
        this.tenantReadRepository = Substitute.For<ITenantReadRepository>();
        this.handler = new GetPaginatedTenantsQueryHandler(this.tenantReadRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedPagedTenants_WhenTenantsExist()
    {
        // Arrange
        GetPaginatedTenantsQuery query = new(1, 10, "acme", "Business", true);

        PagedList<TenantReadModel> pagedTenants = new(
            [
                new TenantReadModel
                {
                    Id = Guid.NewGuid(),
                    Identifier = "acme",
                    Name = "Acme Corp",
                    Plan = "Business",
                    DatabaseStrategy = "Dedicated",
                    IsActive = true,
                },
            ],
            totalItems: 1,
            page: 1,
            size: 10);

        this.tenantReadRepository
            .GetPagedTenantsAsync(query.Page, query.Size, query.Keyword, query.Plan, query.IsActive, Arg.Any<CancellationToken>())
            .Returns(pagedTenants);

        // Act
        var result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(1);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Identifier.ShouldBe("acme");
        result.Value.Items[0].Plan.ShouldBe("Business");

        await this.tenantReadRepository.Received(1)
            .GetPagedTenantsAsync(query.Page, query.Size, query.Keyword, query.Plan, query.IsActive, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPagedTenants_WhenNoTenantsExist()
    {
        // Arrange
        GetPaginatedTenantsQuery query = new(1, 10, null, null, null);

        this.tenantReadRepository
            .GetPagedTenantsAsync(query.Page, query.Size, query.Keyword, query.Plan, query.IsActive, Arg.Any<CancellationToken>())
            .Returns(new PagedList<TenantReadModel>([], 0, 1, 10));

        // Act
        var result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TotalItems.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }
}