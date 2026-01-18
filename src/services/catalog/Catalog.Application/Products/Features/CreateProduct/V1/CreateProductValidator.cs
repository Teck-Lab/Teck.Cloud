using Catalog.Application.Brands.Repositories;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Products.Repositories;
using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Products.Features.CreateProduct.V1
{
    /// <summary>
    /// The create product validator.
    /// </summary>
    public sealed class CreateProductValidator : Validator<CreateProductRequest>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateProductValidator"/> class.
        /// </summary>
        public CreateProductValidator()
        {
            RuleFor(product => product.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name");

            RuleFor(product => product.ProductSku)
                .NotEmpty()
                .MaximumLength(50)
                .WithName("ProductSku")
                .MustAsync(async (sku, ct) =>
                {
                    var repo = Resolve<IProductReadRepository>();
                    return !await repo.ExistsAsync(product => product.Sku.Equals(sku), cancellationToken: ct);
                })
                .WithMessage((_, productSku) => $"Product with SKU '{productSku}' already exists.");

            RuleFor(product => product.GTIN)
                .MaximumLength(50)
                .WithName("GTIN");

            RuleFor(product => product.CategoryIds)
                .NotEmpty()
                .MustAsync(async (ids, ct) =>
                {
                    var repo = Resolve<ICategoryReadRepository>();

                    return await repo.ExistsByIdAsync(ids, cancellationToken: ct);
                })
                .WithMessage((_, ids) => "Some category IDs were not found.");

            RuleFor(product => product.BrandId)
                .NotEmpty()
                .WithMessage("Brand ID is required.")
                .MustAsync(async (brandId, ct) =>
                {
                    var repo = Resolve<IBrandReadRepository>();
                    return !await repo.ExistsAsync(brand => brand.Id.Equals(brandId), cancellationToken: ct);
                })
                .WithMessage((_, brandId) => $"Brand with ID '{brandId}' does not exist.");
        }
    }
}
