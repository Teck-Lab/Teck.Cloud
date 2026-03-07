using Customer.Application.Tenants.EventHandlers.DomainEvents;
using Customer.Domain.Entities.TenantAggregate.Events;
using NSubstitute;
using SharedKernel.Events;
using Wolverine;
using Shouldly;

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
        await _messageBus.Received(1).PublishAsync(Arg.Is<TenantCreatedIntegrationEvent>(e =>
            e.TenantId == tenantId &&
            e.Identifier == "test-tenant" &&
            e.Name == "Test Tenant"));
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
        await _messageBus.Received(1).PublishAsync(Arg.Is<TenantCreatedIntegrationEvent>(e =>
            e.DatabaseStrategy == "Dedicated"));
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
        await _messageBus.Received(1).PublishAsync(Arg.Is<TenantCreatedIntegrationEvent>(e =>
            e.DatabaseProvider == "SqlServer"));
    }

    [Fact]
    public async Task Handle_ShouldPublishEventWithAllProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var domainEvent = CreateDomainEvent(tenantId, "my-tenant", "My Tenant Name", "External", "MySQL");

        TenantCreatedIntegrationEvent? capturedEvent = null;
        await _messageBus.PublishAsync(Arg.Do<TenantCreatedIntegrationEvent>(e => capturedEvent = e));

        // Act
        await _sut.Handle(domainEvent);

        // Assert
        capturedEvent.ShouldNotBeNull();
        capturedEvent!.TenantId.ShouldBe(tenantId);
        capturedEvent.Identifier.ShouldBe("my-tenant");
        capturedEvent.Name.ShouldBe("My Tenant Name");
        capturedEvent.DatabaseStrategy.ShouldBe("External");
        capturedEvent.DatabaseProvider.ShouldBe("MySQL");
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
