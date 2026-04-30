using Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;
using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using ErrorOr;
using NSubstitute;
using Shouldly;

namespace Customer.UnitTests.Application.Queries.Tenants;

public sealed class GetCurrentTenantDatabaseInfoQueryHandlerTests
{
    private readonly ITenantReadRepository tenantReadRepository;
    private readonly GetCurrentTenantDatabaseInfoQueryHandler handler;

    public GetCurrentTenantDatabaseInfoQueryHandlerTests()
    {
        this.tenantReadRepository = Substitute.For<ITenantReadRepository>();
        this.handler = new GetCurrentTenantDatabaseInfoQueryHandler(this.tenantReadRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedDatabaseInfo_WhenTenantExists()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetCurrentTenantDatabaseInfoQuery query = new(tenantId, "customer");

        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns(new TenantDatabaseInfoReadModel
            {
                TenantId = tenantId,
                Identifier = "tenant-alpha",
                DatabaseStrategy = "Dedicated",
                DatabaseProvider = "PostgreSQL",
                HasReadReplicas = true,
            });

        // Act
        ErrorOr<GetCurrentTenantDatabaseInfoResponse> result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TenantId.ShouldBe(tenantId);
        result.Value.Identifier.ShouldBe("tenant-alpha");
        result.Value.DatabaseStrategy.ShouldBe("Dedicated");
        result.Value.DatabaseProvider.ShouldBe("PostgreSQL");
        result.Value.HasReadReplicas.ShouldBeTrue();
        result.Value.ServiceName.ShouldBe("customer");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTenantDoesNotExist()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        GetCurrentTenantDatabaseInfoQuery query = new(tenantId, "customer");

        this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(tenantId, "customer", Arg.Any<CancellationToken>())
            .Returns((TenantDatabaseInfoReadModel?)null);

        // Act
        ErrorOr<GetCurrentTenantDatabaseInfoResponse> result = await this.handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Tenant.NotFound");
    }
}
