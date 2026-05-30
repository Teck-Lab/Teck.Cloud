using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.IntegrationTests.TestHost;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Catalog.IntegrationTests.Endpoints.Brands;

public sealed class CreateBrandEndpointTests
{
    [Fact]
    public async Task CreateBrand_WhenCommandSucceeds_ShouldReturnCreated()
    {
        CreateBrandResponse created = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Brand",
            Description = "Test",
        };

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateBrandCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<CreateBrandResponse>>(created));

        await using CustomWebApplicationFactory host = await CustomWebApplicationFactory.StartAsync(sender);

        using HttpRequestMessage request = new(HttpMethod.Post, "/catalog/v1/Brands")
        {
            Content = JsonContent.Create(new CreateBrandRequest
            {
                Name = "Test Brand",
                Description = "Test",
                Website = "https://example.com",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
    }
}
