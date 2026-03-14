using Customer.Application.Tenants.ReadModels;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Application.ReadModels;

public class TenantDatabaseInfoReadModelTests
{
    [Fact]
    public void Properties_ShouldRoundTripAssignedValues()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        const string strategy = nameof(DatabaseStrategy.Dedicated);
        const string provider = nameof(DatabaseProvider.PostgreSQL);

        // Act
        var model = new TenantDatabaseInfoReadModel
        {
            TenantId = tenantId,
            DatabaseStrategy = strategy,
            DatabaseProvider = provider,
            HasReadReplicas = true,
        };

        // Assert
        model.TenantId.ShouldBe(tenantId);
        model.DatabaseStrategy.ShouldBe(strategy);
        model.DatabaseProvider.ShouldBe(provider);
        model.HasReadReplicas.ShouldBeTrue();
    }
}
