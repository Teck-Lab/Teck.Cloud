using Catalog.Application.Brands.EventHandlers.DomainEvents;
using Catalog.Domain.Entities.BrandAggregate.Events;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Shouldly;
using Wolverine;

namespace Catalog.UnitTests.Application.Brands;

public class BrandCreatedDomainHandlerTests
{
    private readonly IMessageBus messageBus;
    private readonly IMultiTenantContextAccessor<TenantDetails> tenantContextAccessor;
    private readonly ILogger<BrandCreatedDomainHandler> logger;
    private readonly BrandCreatedDomainHandler sut;

    public BrandCreatedDomainHandlerTests()
    {
        this.messageBus = Substitute.For<IMessageBus>();
        this.tenantContextAccessor = CreateTenantAccessor("tenant-a");
        this.logger = Substitute.For<ILogger<BrandCreatedDomainHandler>>();
        this.sut = new BrandCreatedDomainHandler(this.messageBus, this.logger, this.tenantContextAccessor);
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
        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<BrandCreatedIntegrationEvent>(evt => evt.BrandId == brandId),
            Arg.Is<DeliveryOptions>(options => options.TenantId == "tenant-a"));
    }

    [Fact]
    public async Task Handle_ShouldPublishWithoutTenantOptions_WhenTenantContextMissing()
    {
        // Arrange
        var brandId = Guid.NewGuid();
        BrandCreatedDomainEvent domainEvent = new(brandId, "Contoso");
        BrandCreatedDomainHandler sutWithoutTenant = new(this.messageBus, this.logger);

        // Act
        await sutWithoutTenant.Handle(domainEvent);

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

    private static IMultiTenantContextAccessor<TenantDetails> CreateTenantAccessor(string tenantId)
    {
        var accessor = Substitute.For<IMultiTenantContextAccessor<TenantDetails>>();
        accessor.MultiTenantContext.Returns(
            new MultiTenantContext<TenantDetails>(
                new TenantDetails
                {
                    Id = tenantId,
                    Identifier = tenantId,
                    Name = "Test Tenant",
                    IsActive = true,
                }));

        return accessor;
    }
}
