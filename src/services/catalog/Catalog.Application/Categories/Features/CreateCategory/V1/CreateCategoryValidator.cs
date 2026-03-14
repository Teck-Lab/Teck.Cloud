// <copyright file="CreateCategoryValidator.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Categories.Repositories;
using FluentValidation;

namespace Catalog.Application.Categories.Features.CreateCategory.V1;

/// <summary>
/// Validator for the <see cref="CreateCategoryRequest"/> class.
/// </summary>
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    private readonly ICategoryReadRepository categoryReadRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCategoryValidator"/> class.
    /// </summary>
    /// <param name="categoryReadRepository">The category read repository.</param>
    public CreateCategoryValidator(ICategoryReadRepository categoryReadRepository)
    {
        this.categoryReadRepository = categoryReadRepository;

        this.RuleFor(category => category.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithName("Name")
            .MustAsync(async (name, ct) =>
            {
                return !await this.categoryReadRepository.ExistsAsync(category => category.Name.Equals(name, StringComparison.Ordinal), false, ct).ConfigureAwait(false);
            })
            .WithMessage((_, name) => $"Category with the name '{name}' already Exists.");
    }
}
