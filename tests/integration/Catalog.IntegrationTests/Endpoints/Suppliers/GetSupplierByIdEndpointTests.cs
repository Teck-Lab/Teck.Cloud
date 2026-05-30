using System.Net;
using Catalog.Application.Suppliers.Features.GetSupplierById.V1;
using Catalog.IntegrationTests.TestHost;
using ErrorOr;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Catalog.IntegrationTests.Endpoints.Suppliers;

public sealed class GetSupplierByIdEndpointTests
{
    [Fact]
    public async Task GetSupplierById_WhenQuerySucceeds_ShouldReturnOk()
    {
        Guid supplierId = Guid.NewGuid();
        GetByIdSupplierResponse expected = new()
        {
            Id = supplierId,
            Name = "Supplier A",
        };

        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetSupplierByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<GetByIdSupplierResponse>>(expected));

        await using CustomWebApplicationFactory host = await CustomWebApplicationFactory.StartAsync(sender);

        HttpResponseMessage response = await host.Client.GetAsync(
            $"/catalog/v1/Suppliers/{supplierId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSupplierById_WhenSupplierMissing_ShouldReturnNotFound()
    {
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetSupplierByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ErrorOr<GetByIdSupplierResponse>>(
                Error.NotFound("Supplier.NotFound", "Supplier not found")));

        await using CustomWebApplicationFactory host = await CustomWebApplicationFactory.StartAsync(sender);

        HttpResponseMessage response = await host.Client.GetAsync(
            $"/catalog/v1/Suppliers/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
