using Customer.Application.Tenants.EventHandlers.DomainEvents;
using Customer.Domain.Entities.TenantAggregate.Events;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Customer.UnitTests.Application.EventHandlers;

public class TenantCreatedDomainEventHandlerTests
{
    private readonly IMessageBus _messageBus;
    private readonly TenantCreatedDomainHandler _sut;

    public TenantCreatedDomainEventHandlerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new TenantCreatedDomainHandler(_messageBus);
    }

    [Fact]
    public async Task Handle_ShouldPublishIntegrationEvent_WhenDomainEventReceived()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainEvent = CreateDomainEvent(tenantId, "test-tenant", "Test Tenant", "Shared", "PostgreSQL");

        // Act
        await _sut.Handle(domainEvent);

        // Assert
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<TenantCreatedIntegrationEvent>(e =>
                e.TenantId == tenantId &&
                e.Identifier == "test-tenant" &&
                e.Name == "Test Tenant"),
            Arg.Is<DeliveryOptions>(options => options.TenantId == tenantId.ToString("D")));
    }

    [Fact]
    public async Task Handle_ShouldMapDatabaseStrategyCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainEvent = CreateDomainEvent(tenantId, "test-tenant", "Test Tenant", "Dedicated", "PostgreSQL");

        // Act
        await _sut.Handle(domainEvent);

        // Assert
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<TenantCreatedIntegrationEvent>(e => e.DatabaseStrategy == "Dedicated"),
            Arg.Is<DeliveryOptions>(options => options.TenantId == tenantId.ToString("D")));
    }

    [Fact]
    public async Task Handle_ShouldMapDatabaseProviderCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainEvent = CreateDomainEvent(tenantId, "test-tenant", "Test Tenant", "Shared", "SqlServer");

        // Act
        await _sut.Handle(domainEvent);

        // Assert
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<TenantCreatedIntegrationEvent>(e => e.DatabaseProvider == "SqlServer"),
            Arg.Is<DeliveryOptions>(options => options.TenantId == tenantId.ToString("D")));
    }

    [Fact]
    public async Task Handle_ShouldPublishEventWithAllProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainEvent = CreateDomainEvent(tenantId, "my-tenant", "My Tenant Name", "External", "MySQL");

        TenantCreatedIntegrationEvent? capturedEvent = null;
        DeliveryOptions? capturedOptions = null;
        await _messageBus.PublishAsync(
            Arg.Do<TenantCreatedIntegrationEvent>(e => capturedEvent = e),
            Arg.Do<DeliveryOptions>(options => capturedOptions = options));

        // Act
        await _sut.Handle(domainEvent);

        // Assert
        capturedEvent.ShouldNotBeNull();
        capturedEvent!.TenantId.ShouldBe(tenantId);
        capturedEvent.Identifier.ShouldBe("my-tenant");
        capturedEvent.Name.ShouldBe("My Tenant Name");
        capturedEvent.DatabaseStrategy.ShouldBe("External");
        capturedEvent.DatabaseProvider.ShouldBe("MySQL");
        capturedOptions.ShouldNotBeNull();
        capturedOptions!.TenantId.ShouldBe(tenantId.ToString("D"));
    }

    private static TenantCreatedDomainEvent CreateDomainEvent(
        Guid tenantId,
        string identifier,
        string name,
        string databaseStrategy,
        string databaseProvider)
    {
        return new TenantCreatedDomainEvent(new TenantCreatedEventDetails
        {
            TenantId = tenantId,
            Identifier = identifier,
            Name = name,
            DatabaseStrategy = databaseStrategy,
            DatabaseProvider = databaseProvider,
        });
    }
}
