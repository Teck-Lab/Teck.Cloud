using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.ValueObjects;
using Customer.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Customer.UnitTests.Domain.Services;

/// <summary>
/// Unit tests to verify Phase 2 improvements to TenantPricingService.
/// </summary>
public class TenantPricingServicePhase2Tests
{
    [Fact]
    public void CalculateMonthlyPrice_WithPremiumSubscription_ShouldUseNewBasePriceProperty()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var subscription = TenantSubscription.CreatePremiumSubscription(subscriptionId, tenantId, startDate).Value;

        // Act
        var monthlyPrice = TenantPricingService.CalculateMonthlyPrice(subscription);

        // Assert
        // Should use subscription.BasePrice (99.99m) instead of subscription.Plan.BasePrice
        var expectedBasePrice = 99.99m;
        var expectedMultiplier = subscription.CalculateDatabaseCostMultiplier();
        var expectedPrice = expectedBasePrice * expectedMultiplier;
        
        monthlyPrice.Should().Be(expectedPrice);
        
        // Verify the pricing service is using the new BasePrice property
        subscription.BasePrice.Should().Be(99.99m);
        subscription.Plan.BasePrice.Should().Be(99.99m); // Should match for backward compatibility
    }

    [Fact]
    public void CalculateUpgradeCostDifference_WithNewSubscriptionType_ShouldCalculateCorrectly()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var currentSubscription = TenantSubscription.CreateSharedSubscription(subscriptionId, tenantId, startDate).Value;
        var targetType = SubscriptionType.Premium;

        // Act
        var costDifference = TenantPricingService.CalculateUpgradeCostDifference(currentSubscription, targetType);

        // Assert
        // Current: Shared (29.99) -> Target: Premium (99.99)
        // Expected difference: (99.99 - 29.99) * 12 months = 840.00
        var currentPrice = 29.99m * currentSubscription.CalculateDatabaseCostMultiplier();
        var targetPrice = 99.99m * currentSubscription.CalculateDatabaseCostMultiplier(); // Same multiplier for comparison
        var expectedDifference = (targetPrice - currentPrice) * 12;
        
        costDifference.Should().Be(expectedDifference);
    }

    [Fact]
    public void GetPricingBreakdown_ShouldUseNewBasePriceFromSubscription()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var subscription = TenantSubscription.CreateEnterpriseSubscription(subscriptionId, tenantId, startDate).Value;

        // Act
        var breakdown = TenantPricingService.GetPricingBreakdown(subscription);

        // Assert
        // Should use the new BasePrice property (299.99m for Enterprise)
        breakdown.BasePrice.Should().Be(299.99m);
        breakdown.Plan.Should().Be(subscription.Plan); // Backward compatibility
        breakdown.TenantId.Should().Be(subscription.TenantId);
        
        // Verify total calculation uses the new base price
        var expectedTotal = 299.99m * subscription.CalculateDatabaseCostMultiplier();
        breakdown.TotalMonthlyPrice.Should().Be(expectedTotal);
    }

    [Fact]
    public void PricingService_ShouldMaintainBackwardCompatibility()
    {
        // Arrange
        var subscriptionId = TenantSubscriptionId.CreateUnique();
        var tenantId = "tenant-" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow;
        
        var subscription = TenantSubscription.CreateBusinessSubscription(subscriptionId, tenantId, startDate).Value;

        // Act
        var newMethodPrice = TenantPricingService.CalculateMonthlyPrice(subscription);
        var breakdown = TenantPricingService.GetPricingBreakdown(subscription);

        // Assert
        // Both methods should give the same result
        newMethodPrice.Should().Be(breakdown.TotalMonthlyPrice);
        
        // Legacy properties should still work
        subscription.Plan.BasePrice.Should().Be(subscription.BasePrice);
        subscription.DatabaseStrategy.Should().Be(subscription.Plan.DatabaseStrategy);
        
        // New properties should work too
        subscription.SubscriptionType.Should().Be(SubscriptionType.Business);
        subscription.BasePrice.Should().Be(149.99m);
        subscription.Description.Should().Be("Business subscription for professional tenants.");
    }
}
