using Customer.Application.Licenses.EventHandlers.DomainEvents;
using Customer.Domain.Entities.LicenseAggregate.Events;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Customer.UnitTests.Application.EventHandlers;

public sealed class LicenseExpiredDomainHandlerTests
{
    private readonly IMessageBus _messageBus;
    private readonly LicenseExpiredDomainHandler _sut;

    public LicenseExpiredDomainHandlerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new LicenseExpiredDomainHandler(_messageBus);
    }

    [Fact]
    public async Task Handle_WhenDomainEventReceived_ShouldPublishMappedIntegrationEvent()
    {
        Guid tenantId = Guid.NewGuid();
        Guid licenseId = Guid.NewGuid();
        LicenseExpiredDomainEvent domainEvent = new(licenseId, tenantId.ToString("D"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));

        await _sut.Handle(domainEvent);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<TenantLicenseChangedIntegrationEvent>(x =>
                x.TenantId == tenantId &&
                x.LicenseId == licenseId &&
                x.OldStatus == "Active" &&
                x.NewStatus == "Expired"));
    }

    [Fact]
    public async Task Handle_WhenDomainEventIsNull_ShouldThrowArgumentNullException()
    {
        async Task Action() => await _sut.Handle(null!);

        await Should.ThrowAsync<ArgumentNullException>(Action);
    }
}
