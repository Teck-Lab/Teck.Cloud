using Catalog.Application.Categories.Repositories;
using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Categories.Features.CreateCategory.V1;

/// <summary>
/// Validator for the <see cref="CreateCategoryRequest"/> class.
/// </summary>
public sealed class CreateCategoryValidator : Validator<CreateCategoryRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCategoryValidator"/> class.
    /// </summary>
    public CreateCategoryValidator()
    {
        RuleFor(category => category.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithName("Name")
            .MustAsync(async (name, ct) =>
            {
                // For per-request checks, use Resolve<T>() inside the rule
                var repo = Resolve<ICategoryReadRepository>();
                return !await repo.ExistsAsync(category => category.Name.Equals(name, StringComparison.Ordinal), cancellationToken: ct);
            })
            .WithMessage((_, name) => $"Category with the name '{name}' already Exists.");
    }
}
