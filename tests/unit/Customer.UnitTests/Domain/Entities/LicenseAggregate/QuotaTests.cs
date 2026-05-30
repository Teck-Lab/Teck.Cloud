using Customer.Domain.Entities.LicenseAggregate;
using ErrorOr;
using SharedKernel.Core.Pricing;
using Shouldly;

namespace Customer.UnitTests.Domain.Entities.LicenseAggregate;

public class QuotaTests
{
    [Fact]
    public void FromTenantPlan_ShouldCreateQuotaWithCorrectValues_ForTrialPlan()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);

        quota.MaxAccessPoints.ShouldBe(2);
        quota.MaxDevices.ShouldBe(10);
        quota.MaxProducts.ShouldBe(100);
        quota.MaxLocations.ShouldBe(1);
    }

    [Fact]
    public void FromTenantPlan_ShouldCreateQuotaWithCorrectValues_ForEnterprisePlan()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Enterprise);

        quota.MaxAccessPoints.ShouldBeNull();
        quota.MaxDevices.ShouldBeNull();
        quota.MaxProducts.ShouldBeNull();
        quota.MaxLocations.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void ValidateUsage_ShouldReturnSuccess_WhenWithinQuota()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 1, Devices = 5, Products = 50, Locations = 1 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void ValidateUsage_ShouldReturnError_WhenAccessPointsExceeded()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 5, Devices = 0, Products = 0, Locations = 0 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Quota.AccessPoints");
    }

    [Fact]
    public void ValidateUsage_ShouldReturnError_WhenDevicesExceeded()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 0, Devices = 15, Products = 0, Locations = 0 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Quota.Devices");
    }

    [Fact]
    public void ValidateUsage_ShouldReturnError_WhenProductsExceeded()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 0, Devices = 0, Products = 150, Locations = 0 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Quota.Products");
    }

    [Fact]
    public void ValidateUsage_ShouldReturnError_WhenLocationsExceeded()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 0, Devices = 0, Products = 0, Locations = 2 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Quota.Locations");
    }

    [Fact]
    public void ValidateUsage_ShouldReturnMultipleErrors_WhenMultipleQuotasExceeded()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);
        var usage = new UsageCounts { AccessPoints = 10, Devices = 20, Products = 200, Locations = 5 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(4);
    }

    [Fact]
    public void ValidateUsage_ShouldReturnSuccess_WhenEnterprisePlanWithHighUsage()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Enterprise);
        var usage = new UsageCounts { AccessPoints = 10000, Devices = 50000, Products = 100000, Locations = 1000 };

        ErrorOr<Success> result = quota.ValidateUsage(usage);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void ValidateUsage_ShouldThrowArgumentNullException_WhenUsageIsNull()
    {
        var quota = Quota.FromTenantPlan(TenantPlan.Trial);

        Should.Throw<ArgumentNullException>(() => quota.ValidateUsage(null!));
    }

    [Fact]
    public void FromTenantPlan_ShouldThrowArgumentNullException_WhenPlanIsNull()
    {
        Should.Throw<ArgumentNullException>(() => Quota.FromTenantPlan(null!));
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenSameValues()
    {
        var quota1 = Quota.FromTenantPlan(TenantPlan.Trial);
        var quota2 = Quota.FromTenantPlan(TenantPlan.Trial);

        quota1.Equals(quota2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenDifferentValues()
    {
        var quota1 = Quota.FromTenantPlan(TenantPlan.Trial);
        var quota2 = Quota.FromTenantPlan(TenantPlan.Enterprise);

        quota1.Equals(quota2).ShouldBeFalse();
    }
}
