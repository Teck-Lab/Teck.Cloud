// <copyright file="CreateProductValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Repositories;
using Catalog.Application.Categories.Repositories;
using Catalog.Application.Products.Repositories;
using FluentValidation;

namespace Catalog.Application.Products.Features.CreateProduct.V1
{
    /// <summary>
    /// The create product validator.
    /// </summary>
    public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
    {
        private readonly IProductReadRepository productReadRepository;
        private readonly ICategoryReadRepository categoryReadRepository;
        private readonly IBrandReadRepository brandReadRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateProductValidator"/> class.
        /// </summary>
        /// <param name="productReadRepository">The product read repository.</param>
        /// <param name="categoryReadRepository">The category read repository.</param>
        /// <param name="brandReadRepository">The brand read repository.</param>
        public CreateProductValidator(
            IProductReadRepository productReadRepository,
            ICategoryReadRepository categoryReadRepository,
            IBrandReadRepository brandReadRepository)
        {
            this.productReadRepository = productReadRepository;
            this.categoryReadRepository = categoryReadRepository;
            this.brandReadRepository = brandReadRepository;

            this.RuleFor(product => product.Name)
                .NotEmpty()
                .MaximumLength(100)
                .WithName("Name");

            this.RuleFor(product => product.ProductSku)
                .NotEmpty()
                .MaximumLength(50)
                .WithName("ProductSku")
                .MustAsync(async (sku, ct) =>
                {
                    return !await this.productReadRepository.ExistsAsync(product => product.Sku.Equals(sku), false, ct).ConfigureAwait(false);
                })
                .WithMessage((_, productSku) => $"Product with SKU '{productSku}' already exists.");

            this.RuleFor(product => product.GTIN)
                .MaximumLength(50)
                .WithName("GTIN");

            this.RuleFor(product => product.CategoryIds)
                .NotEmpty()
                .MustAsync(async (ids, ct) =>
                {
                    return await this.categoryReadRepository.ExistsByIdAsync(ids, ct).ConfigureAwait(false);
                })
                .WithMessage((_, ids) => "Some category IDs were not found.");

            this.RuleFor(product => product.BrandId)
                .NotEmpty()
                .WithMessage("Brand ID is required.")
                .MustAsync(async (brandId, ct) =>
                {
                    return await this.brandReadRepository.ExistsAsync(brand => brand.Id.Equals(brandId), false, ct).ConfigureAwait(false);
                })
                .WithMessage((_, brandId) => $"Brand with ID '{brandId}' does not exist.");
        }
    }
}
