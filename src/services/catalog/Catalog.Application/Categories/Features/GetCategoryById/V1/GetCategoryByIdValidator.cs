using FastEndpoints;
using FluentValidation;

namespace Catalog.Application.Categories.Features.GetCategoryById.V1;

/// <summary>
/// Validator for the <see cref="GetCategoryByIdRequest"/> request.
/// </summary>
public sealed class GetCategoryByIdValidator : Validator<GetCategoryByIdRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCategoryByIdValidator"/> class.
    /// </summary>
    public GetCategoryByIdValidator()
    {
        RuleFor(category => category.Id)
            .NotEmpty();
    }
}
