// <copyright file="CreateProduct.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.Mappings;
using Catalog.Application.Products.Responses;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Catalog.Domain.Entities.CategoryAggregate.Specifications;
using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Catalog.Application.Products.Features.CreateProduct.V1
{
    /// <summary>
    /// Create product command.
    /// </summary>
    public sealed record CreateProductCommand(
        string Name,
        string? Description,
        string ProductSku,
        string? GTIN,
        Guid? BrandId,
        IReadOnlyCollection<Guid> CategoryIds,
        bool IsActive) : ICommand<ErrorOr<ProductResponse>>;

    /// <summary>
    /// Create Product command handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CreateProductCommandHandler"/> class.
    /// </remarks>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="productWriteRepository">The product repository.</param>
    /// <param name="categoryWriteRepository">The category repository.</param>
    internal sealed class CreateProductCommandHandler(
        IUnitOfWork unitOfWork,
        IProductWriteRepository productWriteRepository,
        ICategoryWriteRepository categoryWriteRepository) : ICommandHandler<CreateProductCommand, ErrorOr<ProductResponse>>
    {
        /// <summary>
        /// The unit of work.
        /// </summary>
        private readonly IUnitOfWork unitOfWork = unitOfWork;

        /// <summary>
        /// The product repository.
        /// </summary>
        private readonly IProductWriteRepository productWriteRepository = productWriteRepository;
        private readonly ICategoryWriteRepository categoryWriteRepository = categoryWriteRepository;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<ProductResponse>>]]></returns>
        public async ValueTask<ErrorOr<ProductResponse>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            CategoriesByIdsSpecification spec = new CategoriesByIdsSpecification(request.CategoryIds);

            var categories = await this.categoryWriteRepository.ListAsync(spec, true, cancellationToken).ConfigureAwait(false);
            List<Domain.Entities.CategoryAggregate.Category> categoryList = categories.ToList();

            ErrorOr<Product> productToAdd = Product.Create(
                request.Name,
                request.Description,
                request.ProductSku,
                request.GTIN,
                categoryList,
                request.IsActive,
                request.BrandId); // Brand will be handled by domain logic or mapping

            if (productToAdd.IsError)
            {
                return productToAdd.Errors;
            }

            await this.productWriteRepository.AddAsync(productToAdd.Value, cancellationToken).ConfigureAwait(false);

            var result = await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (result == 0)
            {
                return Domain.Entities.ProductAggregate.Errors.ProductErrors.NotCreated;
            }

            return ProductMappings.ProductToProductResponse(productToAdd.Value);
        }
    }
}
