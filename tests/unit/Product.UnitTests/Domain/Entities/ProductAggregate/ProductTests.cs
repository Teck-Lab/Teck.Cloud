// <copyright file="ProductTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using ProductEntity = global::Product.Domain.Entities.ProductAggregate.Product;
using ProductCreatedEvent = global::Product.Domain.Entities.ProductAggregate.Events.ProductCreatedEvent;
using Shouldly;

namespace Product.UnitTests.Domain.Entities.ProductAggregate;

public sealed class ProductTests
{
    [Fact]
    public void Create_ShouldReturnProduct_WhenValid()
    {
        ErrorOr<ProductEntity> result = ProductEntity.Create("Wireless Mouse", "WM-001", "123456789");

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Wireless Mouse");
        result.Value.SKU.ShouldBe("WM-001");
        result.Value.Barcode.ShouldBe("123456789");
        result.Value.IsActive.ShouldBeTrue();
        result.Value.Slug.ShouldBe("wireless-mouse");
    }

    [Fact]
    public void Create_ShouldRaiseProductCreatedEvent()
    {
        ErrorOr<ProductEntity> result = ProductEntity.Create("Keyboard", "KB-001");

        result.IsError.ShouldBeFalse();
        result.Value.DomainEvents.ShouldContain(e => e is ProductCreatedEvent);
    }

    [Fact]
    public void Create_ShouldReturnError_WhenNameEmpty()
    {
        ErrorOr<ProductEntity> result = ProductEntity.Create("", "SKU-001");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Product.EmptyName");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenSkuEmpty()
    {
        ErrorOr<ProductEntity> result = ProductEntity.Create("Mouse", "");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Product.EmptySKU");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenNameTooLong()
    {
        string longName = new string('x', 201);
        ErrorOr<ProductEntity> result = ProductEntity.Create(longName, "SKU-001");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Product.NameTooLong");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenSkuTooLong()
    {
        string longSku = new string('x', 101);
        ErrorOr<ProductEntity> result = ProductEntity.Create("Mouse", longSku);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Product.SkuTooLong");
    }

    [Fact]
    public void Create_ShouldSetSlugFromName()
    {
        ErrorOr<ProductEntity> result = ProductEntity.Create("My Product Name", "SKU-001");

        result.IsError.ShouldBeFalse();
        result.Value.Slug.ShouldBe("my-product-name");
    }

    [Fact]
    public void Create_ShouldReturnError_WhenBarcodeTooLong()
    {
        string longBarcode = new string('x', 51);
        ErrorOr<ProductEntity> result = ProductEntity.Create("Mouse", "SKU-001", longBarcode);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Product.BarcodeTooLong");
    }
}
