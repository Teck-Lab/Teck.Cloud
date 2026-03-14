// <copyright file="GetCategoryByIdValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FluentValidation;

namespace Catalog.Application.Categories.Features.GetCategoryById.V1;

/// <summary>
/// Validator for the <see cref="GetCategoryByIdRequest"/> request.
/// </summary>
public sealed class GetCategoryByIdValidator : AbstractValidator<GetCategoryByIdRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCategoryByIdValidator"/> class.
    /// </summary>
    public GetCategoryByIdValidator()
    {
        this.RuleFor(category => category.Id)
            .NotEmpty();
    }
}
