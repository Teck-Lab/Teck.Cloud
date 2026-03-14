using Catalog.Application.Brands.Features.CreateBrand.V1;

namespace Catalog.UnitTests.Application.Brands;

public class RequestTypesTests
{
    [Fact]
    public void CreateBrandRequest_Defaults_Are_Correct()
    {
        var request = new CreateBrandRequest();

        Assert.Equal(string.Empty, request.Name);
        Assert.Null(request.Description);
        Assert.Null(request.Website);
    }

    [Fact]
    public void CreateBrandRequest_CanSet_All_Properties()
    {
        var request = new CreateBrandRequest
        {
            Name = "Brand Name",
            Description = "Brand Description",
            Website = "https://brand.example.com",
        };

        Assert.Equal("Brand Name", request.Name);
        Assert.Equal("Brand Description", request.Description);
        Assert.Equal("https://brand.example.com", request.Website);
    }
}
