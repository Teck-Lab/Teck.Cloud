using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Events;
using ErrorOr;
using Shouldly;

namespace Customer.UnitTests.Domain.Entities.LicenseAggregate;

public class LicenseTests
{
    [Fact]
    public void Create_ShouldReturnLicense_WhenValidInputProvided()
    {
        var args = CreateArgs("tenant-1", null, "Trial", DateTimeOffset.UtcNow.AddDays(14));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBeNull();
        result.Value.TenantId.ShouldBe("tenant-1");
        result.Value.Plan.ShouldBe("Trial");
        result.Value.Status.ShouldBe(LicenseStatus.Trial);
        result.Value.LocationId.ShouldBeNull();
    }

    [Fact]
    public void Create_ShouldRaiseLicenseCreatedDomainEvent()
    {
        var args = CreateArgs("tenant-1", null, "Trial", DateTimeOffset.UtcNow.AddDays(14));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeFalse();
        result.Value.DomainEvents.ShouldNotBeEmpty();
        result.Value.DomainEvents.ShouldContain(e => e is LicenseCreatedDomainEvent);

        var domainEvent = result.Value.DomainEvents.OfType<LicenseCreatedDomainEvent>().First();
        domainEvent.LicenseId.ShouldBe(result.Value.Id);
        domainEvent.TenantId.ShouldBe("tenant-1");
        domainEvent.Plan.ShouldBe("Trial");
        domainEvent.Status.ShouldBe("Trial");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenTenantIdIsEmpty()
    {
        var args = CreateArgs("", null, "Trial", DateTimeOffset.UtcNow.AddDays(14));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.TenantId");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenPlanIsEmpty()
    {
        var args = CreateArgs("tenant-1", null, "", DateTimeOffset.UtcNow.AddDays(14));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.Plan");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenLicenseXmlIsEmpty()
    {
        var args = new LicenseCreateArgs
        {
            TenantId = "tenant-1",
            Plan = "Trial",
            LicenseXml = "",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            PaymentScope = "TenantDefault",
        };

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.LicenseXml");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenExpiresAtIsInThePast()
    {
        var args = CreateArgs("tenant-1", null, "Trial", DateTimeOffset.UtcNow.AddDays(-1));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("License.ExpiresAt");
    }

    [Fact]
    public void Create_ShouldCreateLocationLicense_WhenLocationIdProvided()
    {
        var args = CreateArgs("tenant-1", "loc-1", "Premium", DateTimeOffset.UtcNow.AddYears(1));

        ErrorOr<License> result = License.Create(args);

        result.IsError.ShouldBeFalse();
        result.Value.LocationId.ShouldBe("loc-1");
    }

    [Fact]
    public void Activate_ShouldSetStatusToActive_WhenLicenseIsTrial()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Trial", DateTimeOffset.UtcNow.AddDays(14))).Value;

        license.Activate();

        license.Status.ShouldBe(LicenseStatus.Active);
        license.GracePeriodEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Activate_ShouldRaiseLicenseActivatedDomainEvent()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Trial", DateTimeOffset.UtcNow.AddDays(14))).Value;
        license.ClearDomainEvents();

        license.Activate();

        license.DomainEvents.ShouldContain(e => e is LicenseActivatedDomainEvent);
    }

    [Fact]
    public void Expire_ShouldSetStatusToExpired()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        license.Expire();

        license.Status.ShouldBe(LicenseStatus.Expired);
    }

    [Fact]
    public void Expire_ShouldRaiseLicenseExpiredDomainEvent()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;
        license.ClearDomainEvents();

        license.Expire();

        license.DomainEvents.ShouldContain(e => e is LicenseExpiredDomainEvent);
    }

    [Fact]
    public void EnterGracePeriod_ShouldSetStatusToGrace_AndSetGracePeriodEndsAt()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        license.EnterGracePeriod(TimeSpan.FromDays(3));

        license.Status.ShouldBe(LicenseStatus.Grace);
        license.GracePeriodEndsAt.ShouldNotBeNull();
        license.GracePeriodEndsAt!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Revoke_ShouldSetStatusToRevoked()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        license.Revoke();

        license.Status.ShouldBe(LicenseStatus.Revoked);
        license.GracePeriodEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Renew_ShouldUpdateLicenseXmlAndExpiryAndSetStatusToActive()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;
        license.Expire();
        var newExpiry = DateTimeOffset.UtcNow.AddYears(2);

        license.Renew("<new-xml/>", newExpiry);

        license.LicenseXml.ShouldBe("<new-xml/>");
        license.ExpiresAt.ShouldBe(newExpiry);
        license.Status.ShouldBe(LicenseStatus.Active);
        license.GracePeriodEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Renew_ShouldThrowArgumentException_WhenLicenseXmlIsEmpty()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        Should.Throw<ArgumentException>(() => license.Renew("", DateTimeOffset.UtcNow.AddYears(2)));
    }

    [Fact]
    public void SetPaymentMethod_ShouldUpdatePaymentMethodId()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        license.SetPaymentMethod("pm_123");

        license.PaymentMethodId.ShouldBe("pm_123");
    }

    [Fact]
    public void UpdatePaymentScope_ShouldUpdatePaymentScope()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        license.UpdatePaymentScope("LocationOverride");

        license.PaymentScope.ShouldBe("LocationOverride");
    }

    [Fact]
    public void UpdatePaymentScope_ShouldThrowArgumentException_WhenPaymentScopeIsEmpty()
    {
        var license = License.Create(CreateArgs("tenant-1", null, "Premium", DateTimeOffset.UtcNow.AddYears(1))).Value;

        Should.Throw<ArgumentException>(() => license.UpdatePaymentScope(""));
    }

    private static LicenseCreateArgs CreateArgs(string tenantId, string? locationId, string plan, DateTimeOffset expiresAt)
    {
        return new LicenseCreateArgs
        {
            TenantId = tenantId,
            LocationId = locationId,
            Plan = plan,
            LicenseXml = "<signed-xml/>",
            ExpiresAt = expiresAt,
            PaymentScope = "TenantDefault",
        };
    }
}
