using System.Net;
using System.Net.Http.Json;
using Billing.IntegrationTests.TestSupport;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Billing.IntegrationTests.Endpoints.Webhooks;

public sealed class StripeWebhookEndpointIntegrationTests
{
    [Fact]
    public async Task StripeWebhook_ShouldReturn204()
    {
        // Arrange
        ISender sender = Substitute.For<ISender>();
        await using TestBillingApiHost host = await TestBillingApiHost.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/billing/v1/Billing/Webhooks/Stripe")
        {
            Content = JsonContent.Create(new
            {
                stripeSignature = "t=12345,v1=test-signature",
                payload = "{\"id\":\"evt_test\",\"type\":\"payment_intent.succeeded\"}",
            }),
        };

        // Act
        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
