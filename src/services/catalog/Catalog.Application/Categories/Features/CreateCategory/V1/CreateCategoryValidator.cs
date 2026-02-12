using Catalog.Application.Categories.Repositories;
using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Categories.Features.CreateCategory.V1;

/// <summary>
/// Validator for the <see cref="CreateCategoryRequest"/> class.
/// </summary>
public sealed class CreateCategoryValidator : Validator<CreateCategoryRequest>
{
    private readonly ICategoryReadRepository _categoryReadRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCategoryValidator"/> class.
    /// </summary>
    /// <param name="categoryReadRepository">The category read repository.</param>
    public CreateCategoryValidator(ICategoryReadRepository categoryReadRepository)
    {
        _categoryReadRepository = categoryReadRepository;

        RuleFor(category => category.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithName("Name")
            .MustAsync(async (name, ct) =>
            {
                return !await _categoryReadRepository.ExistsAsync(category => category.Name.Equals(name, StringComparison.Ordinal), cancellationToken: ct);
            })
            .WithMessage((_, name) => $"Category with the name '{name}' already Exists.");
    }
}
