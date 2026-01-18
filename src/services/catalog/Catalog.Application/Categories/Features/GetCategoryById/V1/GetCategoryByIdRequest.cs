namespace Catalog.Application.Categories.Features.GetCategoryById.V1;
/// <summary>
/// Request model for getting a category by ID.
/// </summary>
/// <param name="Id">The category ID.</param>
public record GetCategoryByIdRequest(Guid Id);
