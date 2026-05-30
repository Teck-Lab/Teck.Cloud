// <copyright file="AddBasketItemEndpointIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA2012

using System.Net;
using System.Net.Http.Json;
using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Basket.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Basket.IntegrationTests.Endpoints.Basket;

public sealed class AddBasketItemEndpointIntegrationTests
{
    [Fact]
    public async Task AddBasketItem_ShouldReturn200_WhenCommandSucceeds()
    {
        // Arrange
        AddItemToBasketCommand? capturedCommand = null;

        AddItemToBasketResponse expectedResponse = new()
        {
            BasketId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalQuantity = 2,
            TotalAmount = 39.98m,
            CurrencyCode = "USD",
            Lines = [],
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<AddItemToBasketCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCommand = callInfo.Arg<AddItemToBasketCommand>();
                return new ValueTask<ErrorOr<AddItemToBasketResponse>>(expectedResponse);
            });

        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        AddBasketItemRequest body = new()
        {
            TenantId = expectedResponse.TenantId,
            CustomerId = expectedResponse.CustomerId,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "/basket/v1/Basket/items");
        request.Content = JsonContent.Create(body);
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedCommand.ShouldNotBeNull();
        capturedCommand.TenantId.ShouldBe(body.TenantId);
        capturedCommand.ProductId.ShouldBe(body.ProductId);
        capturedCommand.Quantity.ShouldBe(2);

        AddItemToBasketResponse? responseBody = await response.Content
            .ReadFromJsonAsync<AddItemToBasketResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.TotalQuantity.ShouldBe(2);
        responseBody.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public async Task AddBasketItem_ShouldReturn400_WhenValidationFails()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        // Empty request — all required fields missing
        using HttpRequestMessage request = new(HttpMethod.Post, "/basket/v1/Basket/items");
        request.Content = JsonContent.Create(new { });
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<AddItemToBasketCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBasketItem_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        AddBasketItemRequest body = new()
        {
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 1,
        };

        // Act — no auth header
        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/basket/v1/Basket/items",
            body,
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

#pragma warning restore CA2012
