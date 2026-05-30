// <copyright file="BulkCreateProductsValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Product.Application.Product.Features.BulkCreateProducts.V1;

/// <summary>
/// Validator for <see cref="BulkCreateProductsCommand"/>.
/// </summary>
internal sealed class BulkCreateProductsValidator : AbstractValidator<BulkCreateProductsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkCreateProductsValidator"/> class.
    /// </summary>
    public BulkCreateProductsValidator()
    {
        RuleFor(command => command.CsvText)
            .NotEmpty()
            .WithMessage("CSV text cannot be empty.");
    }
}
