using Catalog.Application.Brands.Repositories;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Features.Products.GetProductById.V1;
using Catalog.Application.Products.Features.CreateProduct.V1;
using Catalog.Application.Products.Repositories;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Catalog.UnitTests.Application.Products
{
    public class ValidatorTests
    {
        [Fact]
        public async Task CreateProductValidator_Should_Fail_On_Empty_ProductSku()
        {
            var repo = Substitute.For<IProductReadRepository>();
            var categoryRepo = Substitute.For<ICategoryReadRepository>();
            var brandRepo = Substitute.For<IBrandReadRepository>();
            var validator = Factory.CreateValidator<CreateProductValidator>(s =>
            {
                s.AddSingleton(repo);
                s.AddSingleton(categoryRepo);
                s.AddSingleton(brandRepo);
            });
            var req = new CreateProductRequest { ProductSku = "" };
            var result = await validator.ValidateAsync(req, TestContext.Current.CancellationToken);
            result.IsValid.ShouldBeFalse();
        }

        [Fact]
        public void GetProductByIdValidator_Should_Fail_On_Empty_ProductId()
        {
            var validator = new GetProductByIdValidator();
            var req = new GetProductByIdRequest { ProductId = Guid.Empty };
            var result = validator.Validate(req);
            result.IsValid.ShouldBeFalse();
        }

        [Fact]
        public void GetProductByIdValidator_Should_Pass_On_Valid_ProductId()
        {
            var validator = new GetProductByIdValidator();
            var req = new GetProductByIdRequest { ProductId = Guid.NewGuid() };
            var result = validator.Validate(req);
            result.IsValid.ShouldBeTrue();
        }

        [Fact]
        public void GetProductByIdValidator_Should_Fail_On_Default_Guid()
        {
            var validator = new GetProductByIdValidator();
            var req = new GetProductByIdRequest { ProductId = default };
            var result = validator.Validate(req);
            result.IsValid.ShouldBeFalse();
        }

        [Fact]
        public async Task CreateProductValidator_Should_Fail_When_ProductSku_Already_Exists()
        {
            var repo = Substitute.For<IProductReadRepository>();
            var categoryRepo = Substitute.For<ICategoryReadRepository>();
            var brandRepo = Substitute.For<IBrandReadRepository>();
            repo.ExistsAsync(
                Arg.Any<System.Linq.Expressions.Expression<System.Func<Catalog.Application.Products.ReadModels.ProductReadModel, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<System.Threading.CancellationToken>())
                .Returns(true);
            var validator = Factory.CreateValidator<CreateProductValidator>(s =>
            {
                s.AddSingleton(repo);
                s.AddSingleton(categoryRepo);
                s.AddSingleton(brandRepo);
            });
            var req = new CreateProductRequest { ProductSku = "DUPLICATE-SKU" };
            var result = await validator.ValidateAsync(req, TestContext.Current.CancellationToken);
            result.IsValid.ShouldBeFalse();
            result.Errors.ShouldContain(e => e.ErrorMessage.Contains("already exists", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
