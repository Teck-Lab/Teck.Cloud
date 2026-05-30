using System.Net;
using System.Net.Http.Json;
using Billing.Application.Billing.Features.GetPaginatedBillingTransactions.V1;
using Billing.IntegrationTests.TestSupport;
using ErrorOr;
using Mediator;
using NSubstitute;
using SharedKernel.Core.Pagination;
using Shouldly;
#pragma warning disable CA2012

namespace Billing.IntegrationTests.Endpoints.Billing;

public sealed class GetPaginatedBillingTransactionsEndpointIntegrationTests
{
    [Fact]
    public async Task GetPaginatedBillingTransactions_ShouldReturn200_WithPagedResponse_WhenMediatorReturnsData()
    {
        // Arrange
        GetPaginatedBillingTransactionsQuery? capturedQuery = null;

        PagedList<GetPaginatedBillingTransactionsResponse> expectedResponse = new(
            items:
            [
                new GetPaginatedBillingTransactionsResponse
                {
                    Id = Guid.NewGuid(),
                    TenantId = Guid.NewGuid(),
                    CorrelationId = Guid.NewGuid(),
                    Amount = 12.50m,
                    Currency = "USD",
                    PaymentMethodId = "pm_test_1",
                    ExternalChargeId = "ch_test_1",
                    StatusName = "Succeeded",
                    Description = "Monthly plan charge",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
            ],
            totalItems: 1,
            page: 1,
            size: 10);

        ISender sender = Substitute.For<ISender>();
        sender
            .Send(Arg.Any<GetPaginatedBillingTransactionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedQuery = callInfo.Arg<GetPaginatedBillingTransactionsQuery>();
                return new ValueTask<ErrorOr<PagedList<GetPaginatedBillingTransactionsResponse>>>(expectedResponse);
            });

        await using TestBillingApiHost host = await TestBillingApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/billing/v1/Billing/Transactions?page=1&size=10");
        request.WithAuthenticatedUser().WithScopes("billing-transaction:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        capturedQuery.ShouldNotBeNull();
        capturedQuery.Page.ShouldBe(1);
        capturedQuery.Size.ShouldBe(10);

        PagedList<GetPaginatedBillingTransactionsResponse>? responseBody = await response.Content
            .ReadFromJsonAsync<PagedList<GetPaginatedBillingTransactionsResponse>>(cancellationToken: TestContext.Current.CancellationToken);

        responseBody.ShouldNotBeNull();
        responseBody.TotalItems.ShouldBe(1);
        responseBody.Items.Count.ShouldBe(1);
        responseBody.Items[0].Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task GetPaginatedBillingTransactions_ShouldReturn400_WhenValidationFails()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBillingApiHost host = await TestBillingApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Get, "/billing/v1/Billing/Transactions?page=0&size=10");
        request.WithAuthenticatedUser().WithScopes("billing-transaction:list");

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await sender.DidNotReceive().Send(Arg.Any<GetPaginatedBillingTransactionsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPaginatedBillingTransactions_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBillingApiHost host = await TestBillingApiHost.StartAsync(sender);

        // Act
        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/billing/v1/Billing/Transactions?page=1&size=10", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

#pragma warning restore CA2012
