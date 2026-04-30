// <copyright file="AddBasketItemValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Basket.Application.Basket.Features.AddItemToBasket.V1;

/// <summary>
/// Validator for add basket item requests.
/// </summary>
public sealed class AddBasketItemValidator : AbstractValidator<AddBasketItemRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddBasketItemValidator"/> class.
    /// </summary>
    public AddBasketItemValidator()
    {
        this.RuleFor(request => request.TenantId).NotEmpty();
        this.RuleFor(request => request.CustomerId).NotEmpty();
        this.RuleFor(request => request.ProductId).NotEmpty();
        this.RuleFor(request => request.Quantity).GreaterThan(0);
    }
}
