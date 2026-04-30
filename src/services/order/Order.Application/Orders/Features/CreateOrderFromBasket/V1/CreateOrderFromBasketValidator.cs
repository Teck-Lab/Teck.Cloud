// <copyright file="CreateOrderFromBasketValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Order.Application.Orders.Features.CreateOrderFromBasket.V1;

/// <summary>
/// Validator for create-order-from-basket request.
/// </summary>
public sealed class CreateOrderFromBasketValidator : AbstractValidator<CreateOrderFromBasketRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrderFromBasketValidator"/> class.
    /// </summary>
    public CreateOrderFromBasketValidator()
    {
        this.RuleFor(request => request.TenantId).NotEmpty();
        this.RuleFor(request => request.CustomerId).NotEmpty();
        this.RuleFor(request => request.BasketId).NotEmpty();
    }
}
