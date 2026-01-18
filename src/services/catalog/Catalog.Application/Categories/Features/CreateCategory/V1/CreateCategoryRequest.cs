namespace Catalog.Application.Categories.Features.CreateCategory.V1;

/// <summary>
/// Request to create a new category.
/// </summary>
/// <param name="Name">The name of the category.</param>
/// <param name="Description">The description of the category.</param>
public record CreateCategoryRequest(
    string Name,
    string? Description);
