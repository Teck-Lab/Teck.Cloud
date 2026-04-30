using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Mediator;
using NSubstitute;
using Order.Application.Orders.Features.CreateOrderFromBasket.V1;
using Order.IntegrationTests.TestSupport;
using Shouldly;
#pragma warning disable CA2012

namespace Order.IntegrationTests.Endpoints.Orders;

public sealed class CreateOrderFromBasketEndpointIntegrationTests
{
    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId = Guid.NewGuid(),
                customerId = Guid.NewGuid(),
                basketId = Guid.NewGuid(),
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await sender.DidNotReceive().Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn403_WhenAuthenticatedButMissingScopeClaim()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId = Guid.NewGuid(),
                customerId = Guid.NewGuid(),
                basketId = Guid.NewGuid(),
            }),
        }.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await sender.DidNotReceive().Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn400_WhenPayloadIsInvalid()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
            }),
        }.WithAuthenticatedUser().WithScopes("order:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn200_AndDispatchCommandWithMappedValues()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();
        Guid basketId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        CreateOrderFromBasketCommand? capturedCommand = null;

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<CreateOrderFromBasketCommand>();
                return new ValueTask<ErrorOr<CreateOrderFromBasketResponse>>(new CreateOrderFromBasketResponse
                {
                    OrderId = orderId,
                    BasketId = basketId,
                    TenantId = tenantId,
                    CustomerId = customerId,
                    Status = "Pending",
                    TotalQuantity = 2,
                    TotalAmount = 39.98m,
                    CurrencyCode = "USD",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    Lines =
                    [
                        new CreateOrderLineResponse
                        {
                            ProductId = Guid.NewGuid(),
                            Quantity = 2,
                            UnitPrice = 19.99m,
                            CurrencyCode = "USD",
                            LineTotal = 39.98m,
                        },
                    ],
                });
            });

        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId,
                customerId,
                basketId,
            }),
        }.WithAuthenticatedUser().WithScopes("order:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(tenantId);
        capturedCommand.CustomerId.ShouldBe(customerId);
        capturedCommand.BasketId.ShouldBe(basketId);

        CreateOrderFromBasketResponse? body = await response.Content
            .ReadFromJsonAsync<CreateOrderFromBasketResponse>(cancellationToken: TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.OrderId.ShouldBe(orderId);
        body.BasketId.ShouldBe(basketId);
        body.TenantId.ShouldBe(tenantId);
        body.CustomerId.ShouldBe(customerId);
        body.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn400_WhenCatalogRevalidationFails()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<CreateOrderFromBasketResponse>>(
                Error.Validation("Order.CatalogValidation.price_unavailable", "Catalog revalidation failed at checkout")));

        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId = Guid.NewGuid(),
                customerId = Guid.NewGuid(),
                basketId = Guid.NewGuid(),
            }),
        }.WithAuthenticatedUser().WithScopes("order:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        responseBody.ShouldContain("Order.CatalogValidation.price_unavailable");
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn404_WhenBasketIsNotFound()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<CreateOrderFromBasketResponse>>(
                Error.NotFound("Order.Basket.NotFound", "Basket was not found")));

        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId = Guid.NewGuid(),
                customerId = Guid.NewGuid(),
                basketId = Guid.NewGuid(),
            }),
        }.WithAuthenticatedUser().WithScopes("order:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, responseBody);
        responseBody.ShouldContain("Order.Basket.NotFound");
    }

    [Fact]
    public async Task CreateOrderFromBasket_ShouldReturn400_WhenCatalogTransportFails()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<CreateOrderFromBasketCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<CreateOrderFromBasketResponse>>(
                Error.Unexpected("Order.CatalogValidation.TransportFailure", "Catalog transport failure")));

        await using TestOrderApiHost host = await TestOrderApiHost.StartAsync(sender);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/order/v1/Orders/from-basket")
        {
            Content = JsonContent.Create(new
            {
                tenantId = Guid.NewGuid(),
                customerId = Guid.NewGuid(),
                basketId = Guid.NewGuid(),
            }),
        }.WithAuthenticatedUser().WithScopes("order:create");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, responseBody);
        responseBody.ShouldContain("Order.CatalogValidation.TransportFailure");
    }
}
#pragma warning restore CA2012
