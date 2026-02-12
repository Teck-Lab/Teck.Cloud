using Catalog.Application.Brands.Features.DeleteBrands.V1;
using Shouldly;

namespace Catalog.UnitTests.Application.Brands;

public class DeleteBrandsRequestTests
{
    [Fact]
    public void Can_Create_Request_With_Empty_Ids()
    {
        // Arrange & Act
        var request = new DeleteBrandsRequest();

        // Assert
        request.Ids.ShouldNotBeNull();
        request.Ids.Count.ShouldBe(0);
    }

    [Fact]
    public void Can_Create_Request_With_Ids()
    {
        // Arrange
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var request = new DeleteBrandsRequest { Ids = ids };

        // Assert
        request.Ids.Count.ShouldBe(2);
    }

    [Fact]
    public void Can_Create_Request_With_Single_Id()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ids = new List<Guid> { id };

        // Act
        var request = new DeleteBrandsRequest { Ids = ids };

        // Assert
        request.Ids.Count.ShouldBe(1);
        request.Ids.First().ShouldBe(id);
    }
}
