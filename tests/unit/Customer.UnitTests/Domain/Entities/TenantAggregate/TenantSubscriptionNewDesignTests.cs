using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Customer.UnitTests.Domain.Entities.TenantAggregate;

/// <summary>
/// Unit tests for the new TenantSubscription design with SubscriptionType.
/// </summary>
public class TenantSubscriptionNewDesignTests
{
    [Fact]
    public void CreateSharedSubscription_ShouldSetCorrectProperties()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;

        // Act
        var result = TenantSubscription.CreateSharedSubscription(subscriptionId, tenantId, startDate);

        // Assert
        result.IsError.Should().BeFalse();
        var subscription = result.Value;
        
        subscription.SubscriptionType.Should().Be(SubscriptionType.Shared);
        subscription.BasePrice.Should().Be(29.99m);
        subscription.Description.Should().Be("Shared subscription for entry-level tenants.");
        subscription.DatabaseStrategyFromType.Should().Be(DatabaseStrategy.Shared);
        
        // Verify backward compatibility
        subscription.Plan.Should().Be(TenantPlan.Shared);
        subscription.DatabaseStrategy.Should().Be(DatabaseStrategy.Shared);
    }

    [Fact]
    public void CreatePremiumSubscription_ShouldSetCorrectProperties()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;

        // Act
        var result = TenantSubscription.CreatePremiumSubscription(subscriptionId, tenantId, startDate);

        // Assert
        result.IsError.Should().BeFalse();
        var subscription = result.Value;
        
        subscription.SubscriptionType.Should().Be(SubscriptionType.Premium);
        subscription.BasePrice.Should().Be(99.99m);
        subscription.Description.Should().Be("Premium subscription with dedicated resources.");
        subscription.DatabaseStrategyFromType.Should().Be(DatabaseStrategy.Dedicated);
        
        // Verify backward compatibility
        subscription.Plan.Should().Be(TenantPlan.Premium);
        subscription.DatabaseStrategy.Should().Be(DatabaseStrategy.Dedicated);
    }

    [Fact]
    public void CreateEnterpriseSubscription_ShouldSetCorrectProperties()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;

        // Act
        var result = TenantSubscription.CreateEnterpriseSubscription(subscriptionId, tenantId, startDate);

        // Assert
        result.IsError.Should().BeFalse();
        var subscription = result.Value;
        
        subscription.SubscriptionType.Should().Be(SubscriptionType.Enterprise);
        subscription.BasePrice.Should().Be(299.99m);
        subscription.Description.Should().Be("Enterprise subscription with external database.");
        subscription.DatabaseStrategyFromType.Should().Be(DatabaseStrategy.External);
        
        // Verify backward compatibility
        subscription.Plan.Should().Be(TenantPlan.Enterprise);
        subscription.DatabaseStrategy.Should().Be(DatabaseStrategy.External);
    }

    [Fact]
    public void UpdateSubscriptionType_ShouldUpdateBothNewAndLegacyProperties()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var subscription = TenantSubscription.CreateSharedSubscription(subscriptionId, tenantId, startDate).Value;

        // Act
        var result = subscription.UpdateSubscriptionType(SubscriptionType.Premium);

        // Assert
        result.IsError.Should().BeFalse();
        
        // New design properties
        subscription.SubscriptionType.Should().Be(SubscriptionType.Premium);
        subscription.BasePrice.Should().Be(99.99m);
        subscription.Description.Should().Be("Premium subscription with dedicated resources.");
        subscription.DatabaseStrategyFromType.Should().Be(DatabaseStrategy.Dedicated);
        
        // Legacy properties (backward compatibility)
        subscription.Plan.Should().Be(TenantPlan.Premium);
        subscription.DatabaseStrategy.Should().Be(DatabaseStrategy.Dedicated);
    }

    [Fact]
    public void CalculateMonthlyPrice_ShouldUseNewBasePriceProperty()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var subscription = TenantSubscription.CreatePremiumSubscription(subscriptionId, tenantId, startDate).Value;

        // Act
        var monthlyPrice = subscription.CalculateMonthlyPrice();

        // Assert
        // Premium base price (99.99) * database cost multiplier
        var expectedBasePrice = 99.99m;
        var databaseMultiplier = subscription.CalculateDatabaseCostMultiplier();
        var expectedPrice = expectedBasePrice * databaseMultiplier;
        
        monthlyPrice.Amount.Should().Be(expectedPrice);
        monthlyPrice.Currency.Should().Be("EUR");
    }
}
