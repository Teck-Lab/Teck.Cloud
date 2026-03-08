using Customer.Infrastructure.Persistence.ReadModels;
using Shouldly;

namespace Customer.UnitTests.Infrastructure.Persistence.ReadModels;

public sealed class TenantDatabaseMetadataReadModelTests
{
    [Fact]
    public void Properties_ShouldRoundTripAssignedValues()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var model = new TenantDatabaseMetadataReadModel
        {
            TenantId = tenantId,
            ServiceName = "catalog",
            ReadDatabaseMode = 1,
            IsDeleted = true,
        };

        // Assert
        model.TenantId.ShouldBe(tenantId);
        model.ServiceName.ShouldBe("catalog");
        model.ReadDatabaseMode.ShouldBe(1);
        model.IsDeleted.ShouldBeTrue();
    }
}
