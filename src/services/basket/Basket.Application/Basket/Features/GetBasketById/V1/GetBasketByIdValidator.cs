// <copyright file="GetBasketByIdValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Basket.Application.Basket.Features.GetBasketById.V1;

/// <summary>
/// Validator for get basket by id requests.
/// </summary>
public sealed class GetBasketByIdValidator : AbstractValidator<GetBasketByIdRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetBasketByIdValidator"/> class.
    /// </summary>
    public GetBasketByIdValidator()
    {
        this.RuleFor(request => request.BasketId).NotEmpty();
        this.RuleFor(request => request.TenantId).NotEmpty();
        this.RuleFor(request => request.CustomerId).NotEmpty();
    }
}
