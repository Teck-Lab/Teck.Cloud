using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Products.ReadModels;
using MemoryPack;
using Shouldly;

namespace Catalog.UnitTests.Application.ReadModels;

public sealed class MemoryPackReadModelSerializationTests
{
    [Fact]
    public void BrandReadModel_ShouldRoundTrip_WithMemoryPack()
    {
        // Arrange
        var model = new BrandReadModel
        {
            Id = Guid.NewGuid(),
            Name = "Brand A",
            Description = "Desc",
            Website = "https://example.com",
        };

        // Act
        byte[] bytes = MemoryPackSerializer.Serialize(model);
        BrandReadModel? restored = MemoryPackSerializer.Deserialize<BrandReadModel>(bytes);

        // Assert
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(model.Id);
        restored.Name.ShouldBe(model.Name);
        restored.Description.ShouldBe(model.Description);
        restored.Website.ShouldBe(model.Website);
    }

    [Fact]
    public void CategoryReadModel_ShouldRoundTrip_WithMemoryPack()
    {
        // Arrange
        var model = new CategoryReadModel
        {
            Id = Guid.NewGuid(),
            Name = "Category A",
            Description = "Desc",
            ParentId = Guid.NewGuid(),
            ParentName = "Parent",
            ImageUrl = new Uri("https://example.com/image.png"),
        };

        // Act
        byte[] bytes = MemoryPackSerializer.Serialize(model);
        CategoryReadModel? restored = MemoryPackSerializer.Deserialize<CategoryReadModel>(bytes);

        // Assert
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(model.Id);
        restored.Name.ShouldBe(model.Name);
        restored.Description.ShouldBe(model.Description);
        restored.ParentId.ShouldBe(model.ParentId);
        restored.ParentName.ShouldBe(model.ParentName);
        restored.ImageUrl.ShouldBe(model.ImageUrl);
    }

    [Fact]
    public void ProductReadModel_ShouldRoundTrip_WithMemoryPack()
    {
        // Arrange
        var model = new ProductReadModel
        {
            Id = Guid.NewGuid(),
            Name = "Product A",
            Description = "Desc",
            Sku = "SKU-1",
            BrandId = Guid.NewGuid(),
            BrandName = "Brand A",
            CategoryId = Guid.NewGuid(),
            CategoryName = "Category A",
            SupplierId = Guid.NewGuid(),
            SupplierName = "Supplier A",
            ImageUrl = new Uri("https://example.com/product.png"),
        };

        // Act
        byte[] bytes = MemoryPackSerializer.Serialize(model);
        ProductReadModel? restored = MemoryPackSerializer.Deserialize<ProductReadModel>(bytes);

        // Assert
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(model.Id);
        restored.Name.ShouldBe(model.Name);
        restored.Description.ShouldBe(model.Description);
        restored.Sku.ShouldBe(model.Sku);
        restored.BrandId.ShouldBe(model.BrandId);
        restored.BrandName.ShouldBe(model.BrandName);
        restored.CategoryId.ShouldBe(model.CategoryId);
        restored.CategoryName.ShouldBe(model.CategoryName);
        restored.SupplierId.ShouldBe(model.SupplierId);
        restored.SupplierName.ShouldBe(model.SupplierName);
        restored.ImageUrl.ShouldBe(model.ImageUrl);
    }
}
