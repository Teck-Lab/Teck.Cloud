// <copyright file="CreateProductValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Product.Application.Product.Features.CreateProduct.V1;

/// <summary>
/// Validator for <see cref="CreateProductCommand"/>.
/// </summary>
internal sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProductValidator"/> class.
    /// </summary>
    public CreateProductValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Sku)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Barcode)
            .MaximumLength(50)
            .When(command => !string.IsNullOrEmpty(command.Barcode));
    }
}
