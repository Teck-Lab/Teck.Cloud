// <copyright file="CreateProduct.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Product.Application.Product.Abstractions;
using Product.Domain.Entities.ProductAggregate.Errors;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Product.Application.Product.Features.CreateProduct.V1;

/// <summary>
/// Command to create a new product.
/// </summary>
/// <param name="Name">Product display name.</param>
/// <param name="Sku">Stock-keeping unit code.</param>
/// <param name="Barcode">Optional product barcode.</param>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string? Barcode)
    : ICommand<ErrorOr<CreateProductResponse>>;

/// <summary>
/// Response for a successful product creation.
/// </summary>
/// <param name="ProductId">The created product identifier.</param>
public sealed record CreateProductResponse(Guid ProductId);

/// <summary>
/// Handler for <see cref="CreateProductCommand"/>.
/// </summary>
internal sealed class CreateProductCommandHandler(
    IProductWriteRepository writeRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, ErrorOr<CreateProductResponse>>
{
    private readonly IProductWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<CreateProductResponse>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        bool exists = await this.writeRepository
            .ExistsBySkuAsync(request.Sku, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return ProductErrors.DuplicateSku;
        }

        ErrorOr<Domain.Entities.ProductAggregate.Product> created = Domain.Entities.ProductAggregate.Product.Create(
            request.Name,
            request.Sku,
            request.Barcode);

        if (created.IsError)
        {
            return created.Errors;
        }

        await this.writeRepository
            .AddAsync(created.Value, cancellationToken)
            .ConfigureAwait(false);
        await this.unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CreateProductResponse(created.Value.Id);
    }
}
