// <copyright file="GetBasketByIdEndpointIntegrationTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA2012

using System.Net;
using System.Net.Http.Json;
using Basket.Application.Basket.Features.AddItemToBasket.V1;
using Basket.Application.Basket.Features.GetBasketById.V1;
using Basket.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Basket.IntegrationTests.Endpoints.Basket;

public sealed class GetBasketByIdEndpointIntegrationTests
{
    [Fact]
    public async Task GetBasketById_ShouldReturn200_WhenQuerySucceeds()
    {
        // Arrange
        GetBasketByIdQuery? capturedQuery = null;
        Guid basketId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();

        AddItemToBasketResponse expectedResponse = new()
        {
            BasketId = basketId,
            TenantId = tenantId,
            CustomerId = customerId,
            TotalQuantity = 3,
            TotalAmount = 59.97m,
            CurrencyCode = "USD",
            Lines = [],
        };

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetBasketByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetBasketByIdQuery>();
                return new ValueTask<ErrorOr<AddItemToBasketResponse>>(expectedResponse);
            });

        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/basket/v1/Basket/{basketId}?TenantId={tenantId}&CustomerId={customerId}");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.BasketId.ShouldBe(basketId);

        AddItemToBasketResponse? responseBody = await response.Content
            .ReadFromJsonAsync<AddItemToBasketResponse>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.BasketId.ShouldBe(basketId);
        responseBody.TotalQuantity.ShouldBe(3);
    }

    [Fact]
    public async Task GetBasketById_ShouldReturn404_WhenBasketNotFound()
    {
        // Arrange
        Guid basketId = Guid.NewGuid();

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetBasketByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<AddItemToBasketResponse>>(
                Error.NotFound("Basket.NotFound", "Basket not found.")));

        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/basket/v1/Basket/{basketId}?TenantId={Guid.NewGuid()}&CustomerId={Guid.NewGuid()}");
        request.WithAuthenticatedUser();

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBasketById_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBasketApiHost host = await TestBasketApiHost.StartAsync(sender);

        Guid basketId = Guid.NewGuid();

        // Act — no auth header
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/basket/v1/Basket/{basketId}?TenantId={Guid.NewGuid()}&CustomerId={Guid.NewGuid()}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

#pragma warning restore CA2012
