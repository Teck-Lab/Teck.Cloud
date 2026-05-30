// <copyright file="BulkCreateProducts.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ErrorOr;
using Product.Application.Product.Abstractions;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;

namespace Product.Application.Product.Features.BulkCreateProducts.V1;

/// <summary>
/// Command to bulk-create products from CSV text.
/// </summary>
/// <param name="CsvText">Raw CSV text with header row.</param>
public sealed record BulkCreateProductsCommand(string CsvText)
    : ICommand<ErrorOr<BulkCreateProductsResponse>>;

/// <summary>
/// Response for a bulk product creation.
/// </summary>
/// <param name="CreatedCount">Number of products successfully created.</param>
/// <param name="TotalRows">Total data rows processed.</param>
/// <param name="Errors">List of per-row errors.</param>
public sealed record BulkCreateProductsResponse(
    int CreatedCount,
    int TotalRows,
    IReadOnlyList<BulkCreateError> Errors);

/// <summary>
/// Describes a single row-level error during bulk creation.
/// </summary>
/// <param name="Row">The 1-based row number.</param>
/// <param name="Message">The error message.</param>
public sealed record BulkCreateError(int Row, string Message);

/// <summary>
/// Handler for <see cref="BulkCreateProductsCommand"/>.
/// </summary>
internal sealed class BulkCreateProductsCommandHandler(
    IProductWriteRepository writeRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<BulkCreateProductsCommand, ErrorOr<BulkCreateProductsResponse>>
{
    private readonly IProductWriteRepository writeRepository = writeRepository;
    private readonly IUnitOfWork unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<BulkCreateProductsResponse>> Handle(
        BulkCreateProductsCommand request,
        CancellationToken cancellationToken)
    {
        List<BulkCreateError> errors = [];
        List<Domain.Entities.ProductAggregate.Product> productsToCreate = [];
        int totalRows = 0;

        using StringReader reader = new(request.CsvText);
        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
        };

        using CsvReader csv = new(reader, config);
        await csv.ReadAsync().ConfigureAwait(false);
        csv.ReadHeader();

        int rowNumber = 1;
        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            rowNumber++;
            totalRows++;

            string? name = csv.GetField("name") ?? csv.GetField(0);
            string? sku = csv.GetField("sku") ?? csv.GetField(1);
            string? barcode = csv.GetField("barcode") ?? csv.GetField(2);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku))
            {
                errors.Add(new BulkCreateError(rowNumber, "Name and SKU are required."));
                continue;
            }

            bool exists = await this.writeRepository
                .ExistsBySkuAsync(sku, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                errors.Add(new BulkCreateError(rowNumber, $"SKU '{sku}' already exists."));
                continue;
            }

            ErrorOr<Domain.Entities.ProductAggregate.Product> created = Domain.Entities.ProductAggregate.Product.Create(name, sku, barcode);
            if (created.IsError)
            {
                errors.Add(new BulkCreateError(rowNumber, created.FirstError.Description));
                continue;
            }

            productsToCreate.Add(created.Value);
        }

        if (productsToCreate.Count != 0)
        {
            foreach (Domain.Entities.ProductAggregate.Product product in productsToCreate)
            {
                await this.writeRepository
                    .AddAsync(product, cancellationToken)
                    .ConfigureAwait(false);
            }

            await this.unitOfWork
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new BulkCreateProductsResponse(
            productsToCreate.Count,
            totalRows,
            errors);
    }
}
