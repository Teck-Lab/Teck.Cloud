using Catalog.Application.Brands.EventHandlers.DomainEvents;
using Catalog.Domain.Entities.BrandAggregate.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Catalog.UnitTests.Application.Brands;

public class BrandCreatedDomainHandlerTests
{
    private readonly IMessageBus messageBus;
    private readonly ILogger<BrandCreatedDomainHandler> logger;
    private readonly BrandCreatedDomainHandler sut;

    public BrandCreatedDomainHandlerTests()
    {
        this.messageBus = Substitute.For<IMessageBus>();
        this.logger = Substitute.For<ILogger<BrandCreatedDomainHandler>>();
        this.sut = new BrandCreatedDomainHandler(this.messageBus, this.logger);
    }

    [Fact]
    public async Task Handle_ShouldPublishIntegrationEvent_WhenDomainEventProvided()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        BrandCreatedDomainEvent domainEvent = new(brandId, "Contoso");

        // Act
        await this.sut.Handle(domainEvent);

        // Assert
        await this.messageBus.Received(1).PublishAsync(Arg.Is<BrandCreatedIntegrationEvent>(evt => evt.BrandId == brandId));
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentNullException_WhenDomainEventIsNull()
    {
        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => this.sut.Handle(null!));

        // Assert
        exception.ParamName.ShouldBe("domainEvent");
    }
}
