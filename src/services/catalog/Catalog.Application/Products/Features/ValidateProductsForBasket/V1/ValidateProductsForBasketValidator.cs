// <copyright file="ValidateProductsForBasketValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Products.Features.ValidateProductsForBasket.V1;

/// <summary>
/// Validator for basket product validation requests.
/// </summary>
public sealed class ValidateProductsForBasketValidator : AbstractValidator<ValidateProductsForBasketRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateProductsForBasketValidator"/> class.
    /// </summary>
    public ValidateProductsForBasketValidator()
    {
        this.RuleFor(request => request.Items)
            .NotNull()
            .NotEmpty();

        this.RuleForEach(request => request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(line => line.ProductId)
                    .NotEmpty();

                item.RuleFor(line => line.Quantity)
                    .GreaterThan(0);
            });
    }
}
