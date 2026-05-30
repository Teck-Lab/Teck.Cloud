using Ardalis.SmartEnum;
using Customer.Domain.Entities.LicenseAggregate;
using Shouldly;

namespace Customer.UnitTests.Domain.Entities.LicenseAggregate;

public class LicenseStatusTests
{
    [Theory]
    [InlineData("Trial", true, false)]
    [InlineData("Active", true, false)]
    [InlineData("Expired", false, true)]
    [InlineData("Grace", false, true)]
    [InlineData("Revoked", false, true)]
    public void Status_ShouldHaveCorrectIsUsableAndIsExpired(
        string statusName,
        bool expectedUsable,
        bool expectedExpired)
    {
        var status = LicenseStatus.FromName(statusName, false);

        status.IsUsable.ShouldBe(expectedUsable);
        status.IsExpired.ShouldBe(expectedExpired);
    }

    [Fact]
    public void Trial_ShouldHaveValue0()
    {
        LicenseStatus.Trial.Value.ShouldBe(0);
    }

    [Fact]
    public void Active_ShouldHaveValue1()
    {
        LicenseStatus.Active.Value.ShouldBe(1);
    }

    [Fact]
    public void Expired_ShouldHaveValue2()
    {
        LicenseStatus.Expired.Value.ShouldBe(2);
    }

    [Fact]
    public void Grace_ShouldHaveValue3()
    {
        LicenseStatus.Grace.Value.ShouldBe(3);
    }

    [Fact]
    public void Revoked_ShouldHaveValue4()
    {
        LicenseStatus.Revoked.Value.ShouldBe(4);
    }

    [Fact]
    public void FromName_ShouldThrow_WhenInvalidName()
    {
        Should.Throw<SmartEnumNotFoundException>(() => LicenseStatus.FromName("Invalid", false));
    }
}
